#!/usr/bin/env bash
# Dev loop for the ReStory mod: build -> deploy into BepInEx/plugins -> launch via Steam.
#
# Usage: scripts/dev.sh [build|deploy|launch|focus|kill|check|run|hot|install-hot]   (default: run)
#   build   dotnet build (Debug)
#   deploy  kill game if running, copy plugin output into $GameDir/BepInEx/plugins/RestoryMod
#   launch  start the game through Steam
#   focus   bring the game window to the front
#   kill    close the game normally; if it hangs, force-close (asks first; KILL_GAME=1 skips the prompt)
#   check   report whether the installed game build changed since the last run
#   run     check + build + deploy + launch
#   hot     check + build, then hot-reload into the running game through ScriptEngine (BepInEx/scripts).
#           If the game is not running, or runs the normal plugins/ install, switches to scripts/ and
#           launches instead; later `hot` runs reload in place without a restart.
#   install-hot  install ScriptEngine (pinned, checksummed) and set it to load + watch scripts/
#   e2e [scenario] [--stop-at N] [key=value...]
#           end-to-end run (default scenario full-repair-r2-hold): backs up the save folder, deploys the
#           mod with the RestoryMod.E2E harness, launches, and prints the report. Exits 1 unless every check
#           passed. --stop-at N replays to just before check N and leaves the game there, the scenario's
#           settings still on (in memory only; restart the game to drop them). Needs ViGEmBus.
#           Sets Steam to Invisible first. Every other deploy removes the harness again.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Non-interactive WSL shells (e.g. over ssh) lack the Windows PATH; we need cmd/tasklist/taskkill.
export PATH="$PATH:/mnt/c/Windows/System32:/mnt/c/Windows/System32/WindowsPowerShell/v1.0"
for exe in cmd.exe tasklist.exe taskkill.exe powershell.exe; do
  command -v "$exe" >/dev/null || { echo "error: $exe not found; is WSL interop enabled?" >&2; exit 1; }
done
APP_ID=3812600
CONFIG="${CONFIG:-Debug}"
PLUGIN_PROJECT="$ROOT/src/RestoryMod/RestoryMod.csproj"
STATE_FILE="$ROOT/.game-build"

game_dir() {
  local props="$ROOT/GamePaths.props"
  if [[ ! -f "$props" ]]; then
    echo "error: $props missing. Copy GamePaths.props.example and set GameDir." >&2
    exit 1
  fi
  sed -n 's:.*<GameDir>\(.*\)</GameDir>.*:\1:p' "$props" | head -n1
}

GAME_DIR="$(game_dir)"
PLUGIN_DEST="$GAME_DIR/BepInEx/plugins/RestoryMod"
# The end-to-end harness: only `e2e` puts it here; every other deploy takes it out.
E2E_PROJECT="$ROOT/src/RestoryMod.E2E/RestoryMod.E2E.csproj"
E2E_DEST="$GAME_DIR/BepInEx/plugins/RestoryMod.E2E"
E2E_DIR="$GAME_DIR/BepInEx/e2e"
# Hot mode: ScriptEngine loads the mod from here instead, and reloads it when a DLL changes.
SCRIPTS_DEST="$GAME_DIR/BepInEx/scripts"
SCRIPT_ENGINE_DLL="$GAME_DIR/BepInEx/plugins/ScriptEngine/ScriptEngine.dll"
SCRIPT_ENGINE_CFG="$GAME_DIR/BepInEx/config/com.bepis.bepinex.scriptengine.cfg"
SCRIPT_ENGINE_VERSION=r11.2
SCRIPT_ENGINE_SHA256=6092e582b3f79c518a549503e5f13d65c5d74f0d6381366dd444e592cde13233
MANIFEST="$GAME_DIR/../../appmanifest_${APP_ID}.acf"

current_build() {
  sed -n 's/.*"buildid"[[:space:]]*"\([0-9]*\)".*/\1/p' "$MANIFEST" 2>/dev/null | head -n1
}

cmd_check() {
  local now last
  now="$(current_build)"
  if [[ -z "$now" ]]; then
    echo "warn: could not read buildid from $MANIFEST" >&2
    return 0
  fi
  last="$(cat "$STATE_FILE" 2>/dev/null || true)"
  if [[ -n "$last" && "$last" != "$now" ]]; then
    echo "!! Game build changed: $last -> $now. Run the TargetCheck tests before trusting patches." >&2
  else
    echo "game build: $now"
  fi
  echo "$now" > "$STATE_FILE"
}

cmd_build() {
  dotnet build "$PLUGIN_PROJECT" -c "$CONFIG"
}

game_running() {
  tasklist.exe /FI "IMAGENAME eq Restory.exe" 2>/dev/null | grep -qi restory.exe
}

cmd_kill() {
  game_running || return 0

  # Ask the game to close normally first (WM_CLOSE), so it can save.
  taskkill.exe /IM Restory.exe >/dev/null 2>&1 || true
  for _ in $(seq 1 20); do
    if ! game_running; then
      # Steam keeps the app marked as running for a moment after the process exits, and drops a
      # steam://rungameid request sent in that window.
      sleep 3
      return 0
    fi
    sleep 0.5
  done

  if game_running; then
    # Force-closing loses unsaved progress, so confirm unless KILL_GAME=1.
    if [[ "${KILL_GAME:-0}" != 1 ]]; then
      if [[ -t 0 ]]; then
        read -r -p "Restory.exe did not close normally. Force-close it (unsaved progress is lost)? [y/N] " answer
        [[ "$answer" == [yY]* ]] || { echo "aborted: game still running" >&2; exit 1; }
      else
        echo "error: Restory.exe did not close normally. Close it, or set KILL_GAME=1 to force-close." >&2
        exit 1
      fi
    fi
    taskkill.exe /IM Restory.exe /F >/dev/null
    # Give Windows a moment to release file locks on the plugin DLL.
    for _ in 1 2 3 4 5 6 7 8 9 10; do game_running || break; sleep 0.5; done
  fi
}

cmd_deploy() {
  if [[ ! -d "$GAME_DIR/BepInEx" ]]; then
    echo "error: BepInEx not installed in $GAME_DIR" >&2
    exit 1
  fi
  cmd_kill
  # A copy in scripts/ too would load the plugin twice (ScriptEngine refuses the duplicate GUID).
  clear_build "$SCRIPTS_DEST"
  copy_build "$PLUGIN_DEST"
  rm -rf "$E2E_DEST"
}

clear_build() {
  rm -f "$1"/RestoryMod*.dll "$1"/RestoryMod*.pdb
}

copy_build() {
  local out="$ROOT/src/RestoryMod/bin/$CONFIG/netstandard2.1"
  mkdir -p "$1"
  # Symbols first: ScriptEngine reloads on a DLL change and reads the matching PDB with it.
  cp -v "$out"/RestoryMod*.pdb "$1/" 2>/dev/null || true
  cp -v "$out"/RestoryMod*.dll "$1/"
}

# Sets `Key = value` in a BepInEx config file, adding the line under its [Section] if missing.
set_cfg() {
  local file="$1" section="$2" key="$3" value="$4"
  if grep -q "^$key = " "$file" 2>/dev/null; then
    sed -i "s|^$key = .*|$key = $value|" "$file"
  elif grep -qF "[$section]" "$file" 2>/dev/null; then
    sed -i "/^\[$section\]/a $key = $value" "$file"
  else
    printf '\n[%s]\n%s = %s\n' "$section" "$key" "$value" >> "$file"
  fi
}

cmd_install_hot() {
  if [[ ! -f "$SCRIPT_ENGINE_DLL" ]]; then
    local tmp zip
    tmp="$(mktemp -d)"
    zip="$tmp/ScriptEngine.zip"
    curl -fsSL -o "$zip" \
      "https://github.com/BepInEx/BepInEx.Debug/releases/download/$SCRIPT_ENGINE_VERSION/ScriptEngine_$SCRIPT_ENGINE_VERSION.zip"
    echo "$SCRIPT_ENGINE_SHA256  $zip" | sha256sum -c --quiet || { rm -rf "$tmp"; exit 1; }
    mkdir -p "$(dirname "$SCRIPT_ENGINE_DLL")"
    # The archive holds one entry, named with Windows separators; read just that entry.
    python3 -I -c 'import sys, zipfile; open(sys.argv[2], "wb").write(zipfile.ZipFile(sys.argv[1]).read("BepInEx\\plugins\\ScriptEngine.dll"))' \
      "$zip" "$SCRIPT_ENGINE_DLL"
    rm -rf "$tmp"
    echo "installed ScriptEngine $SCRIPT_ENGINE_VERSION"
  fi
  mkdir -p "$SCRIPTS_DEST"
  set_cfg "$SCRIPT_ENGINE_CFG" General LoadOnStart true
  set_cfg "$SCRIPT_ENGINE_CFG" AutoReload EnableFileSystemWatcher true
  set_cfg "$SCRIPT_ENGINE_CFG" AutoReload AutoReloadDelay 1
}

hot_mode_active() {
  [[ -f "$SCRIPTS_DEST/RestoryMod.dll" && ! -f "$PLUGIN_DEST/RestoryMod.dll" ]]
}

cmd_hot() {
  # Installs on first use; also re-applies the ScriptEngine settings, should they have been changed.
  cmd_install_hot
  if game_running && hot_mode_active; then
    local log="$GAME_DIR/BepInEx/LogOutput.log" seen
    seen="$(wc -l < "$log")"
    copy_build "$SCRIPTS_DEST"
    # BepInEx buffers its log file, so the reload can take several seconds to show up there.
    echo "waiting for ScriptEngine reload..."
    for _ in $(seq 1 60); do
      local reloaded
      reloaded="$(tail -n +"$((seen + 1))" "$log" | grep -n "Reloaded all plugins" | head -n1 | cut -d: -f1 || true)"
      if [[ -n "$reloaded" ]]; then
        # From the reload on: lines the previous load logged late can still sit between `seen` and it.
        print_mod_startup "$log" "$((seen + reloaded))"
        return 0
      fi
      sleep 0.5
    done
    echo "error: no reload within 30s; check $log" >&2
    return 1
  fi

  # The normal install's DLL is locked while the game runs, so switching modes needs a restart.
  cmd_kill
  clear_build "$PLUGIN_DEST"
  rm -rf "$E2E_DEST"
  copy_build "$SCRIPTS_DEST"
  cmd_launch
}

# Windows refuses focus to a window launched from a background process (this script), so the
# game starts behind Steam Big Picture. A synthetic Alt press lifts that lock for SetForegroundWindow.
cmd_focus() {
  (cd /mnt/c && powershell.exe -NoProfile -Command '
    Add-Type -Namespace Dev -Name W -MemberDefinition @"
      [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
      [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
      [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
      [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
"@
    for ($i = 0; $i -lt 40; $i++) {
      $p = Get-Process Restory -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
      if ($p) {
        [Dev.W]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [Dev.W]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
        [Dev.W]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
        [Dev.W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
        if ([Dev.W]::GetForegroundWindow() -eq $p.MainWindowHandle) { "game window focused"; exit 0 }
      }
      Start-Sleep -Milliseconds 500
    }
    "warn: could not focus the game window"
  ') || true
}

# Prints warnings and the mod's lines from line $2 of log $1 on, once the mod has applied its patches.
# Under ScriptEngine the mod loads after the chainloader finishes, and the log file lags behind.
print_mod_startup() {
  local log="$1" from="$2"
  for _ in $(seq 1 20); do
    # Not a pipe: under pipefail, grep -q quitting early fails tail with SIGPIPE and hides the match.
    grep -q ":RestoryMod\] Patches applied" <(tail -n +"$from" "$log") && break
    sleep 0.5
  done
  tail -n +"$from" "$log" | grep -E "\[(Warning|Error|Fatal) *:|:RestoryMod\]" || true
}

cmd_launch() {
  # Remove the previous session's log so the startup wait below cannot match stale lines.
  local log="$GAME_DIR/BepInEx/LogOutput.log"
  rm -f "$log"

  # Run from /mnt/c so cmd.exe does not start in a UNC working directory.
  (cd /mnt/c && cmd.exe /c start "" "steam://rungameid/$APP_ID")

  echo "waiting for BepInEx startup..."
  for _ in $(seq 1 90); do
    if grep -q "Chainloader startup complete" "$log" 2>/dev/null; then
      print_mod_startup "$log" 1
      cmd_focus
      return 0
    fi
    sleep 1
  done
  echo "warn: no BepInEx startup within 90s; check $log" >&2
}

save_dir() {
  local dirs=(/mnt/c/Users/*/AppData/LocalLow/Mandragora/Restory/SaveData)
  if [[ ${#dirs[@]} -ne 1 || ! -d "${dirs[0]}" ]]; then
    echo "error: expected one ReStory save folder, found: ${dirs[*]}" >&2
    exit 1
  fi
  echo "${dirs[0]}"
}

# The harness blocks every save write, but a copy of the save folder is taken first all the same.
backup_saves() {
  local saves backups stamp
  saves="$(save_dir)"
  backups="$(dirname "$saves")/SaveData-e2e-backups"
  stamp="$(date +%Y%m%d-%H%M%S)"
  mkdir -p "$backups"
  cp -r "$saves" "$backups/$stamp"
  # Keep the five newest.
  ls -1d "$backups"/*/ | head -n -5 | xargs -r rm -rf
  echo "save folder backed up to $backups/$stamp"
}

cmd_e2e() {
  local scenario=full-repair-r2-hold stop_at="" options=()
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --stop-at) stop_at="$2"; shift 2 ;;
      *=*) options+=("$1"); shift ;;
      *) scenario="$1"; shift ;;
    esac
  done

  cmd_check
  # Every run starts the game afresh: go Invisible so friends aren't told each time.
  (cd /mnt/c && cmd.exe /c start "" "steam://friends/status/invisible")
  dotnet build "$E2E_PROJECT" -c "$CONFIG"
  cmd_deploy
  # After deploy has closed the game, so the copy holds what it saved on the way out.
  backup_saves
  local out="$ROOT/src/RestoryMod.E2E/bin/$CONFIG/netstandard2.1"
  mkdir -p "$E2E_DEST" "$E2E_DIR"
  cp "$out"/RestoryMod.E2E.dll "$out"/RestoryMod.E2E.pdb "$out"/RestoryMod.E2E.Core.dll "$out"/Nefarius.ViGEm.Client.dll "$E2E_DEST/"
  rm -f "$E2E_DIR"/result.txt "$E2E_DIR"/log.txt "$E2E_DIR"/*.png
  {
    echo "scenario=$scenario"
    [[ -n "$stop_at" ]] && echo "stopAt=$stop_at"
    for option in "${options[@]}"; do echo "$option"; done
  } > "$E2E_DIR/request.txt"
  cmd_launch

  echo "running $scenario..."
  for _ in $(seq 1 600); do
    [[ -f "$E2E_DIR/result.txt" ]] && break
    game_running || break
    sleep 1
  done
  if [[ ! -f "$E2E_DIR/result.txt" ]]; then
    # Exceptions outside the harness's own handling land only in Unity's Player.log.
    echo "error: no result (game closed, or none within 10 minutes); check $GAME_DIR/BepInEx/LogOutput.log" \
      "and $(dirname "$(save_dir)")/Player.log" >&2
    exit 1
  fi
  # Everything the harness logged, then the failures on their own.
  cat "$E2E_DIR/log.txt"
  if grep -qF "] FAIL #" "$E2E_DIR/result.txt"; then
    echo
    echo "failed checks:"
    grep -F "] FAIL #" "$E2E_DIR/result.txt"
  fi
  # A stop counts as a pass only if no check failed before it.
  grep -qE "RESULT PASSED|STOPPED before" "$E2E_DIR/result.txt" && ! grep -qF "] FAIL #" "$E2E_DIR/result.txt"
}

case "${1:-run}" in
  build)  cmd_build ;;
  deploy) cmd_deploy ;;
  launch) cmd_launch ;;
  focus)  cmd_focus ;;
  kill)   cmd_kill ;;
  check)  cmd_check ;;
  run)    cmd_check; cmd_build; cmd_deploy; cmd_launch ;;
  hot)    cmd_check; cmd_build; cmd_hot ;;
  install-hot) cmd_install_hot ;;
  e2e)    shift; cmd_e2e "$@" ;;
  *) echo "usage: $0 [build|deploy|launch|focus|kill|check|run|hot|install-hot|e2e]" >&2; exit 2 ;;
esac
