#!/usr/bin/env bash
# Dev loop for the ReStory mod: build -> deploy into BepInEx/plugins -> launch via Steam.
#
# Usage: scripts/dev.sh [build|deploy|launch|focus|kill|check|run]   (default: run)
#   build   dotnet build (Debug)
#   deploy  kill game if running, copy plugin output into $GameDir/BepInEx/plugins/RestoryMod
#   launch  start the game through Steam
#   focus   bring the game window to the front
#   kill    close the game normally; if it hangs, force-close (asks first; KILL_GAME=1 skips the prompt)
#   check   report whether the installed game build changed since the last run
#   run     check + build + deploy + launch
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
  local out="$ROOT/src/RestoryMod/bin/$CONFIG/netstandard2.1"
  cmd_kill
  mkdir -p "$PLUGIN_DEST"
  cp -v "$out"/RestoryMod*.dll "$PLUGIN_DEST/"
  cp -v "$out"/RestoryMod*.pdb "$PLUGIN_DEST/" 2>/dev/null || true
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

cmd_launch() {
  # Remove the previous session's log so the startup wait below cannot match stale lines.
  local log="$GAME_DIR/BepInEx/LogOutput.log"
  rm -f "$log"

  # Run from /mnt/c so cmd.exe does not start in a UNC working directory.
  (cd /mnt/c && cmd.exe /c start "" "steam://rungameid/$APP_ID")

  echo "waiting for BepInEx startup..."
  for _ in $(seq 1 90); do
    if grep -q "Chainloader startup complete" "$log" 2>/dev/null; then
      grep -E "\[(Warning|Error|Fatal) *:|:RestoryMod\]" "$log" || true
      cmd_focus
      return 0
    fi
    sleep 1
  done
  echo "warn: no BepInEx startup within 90s; check $log" >&2
}

case "${1:-run}" in
  build)  cmd_build ;;
  deploy) cmd_deploy ;;
  launch) cmd_launch ;;
  focus)  cmd_focus ;;
  kill)   cmd_kill ;;
  check)  cmd_check ;;
  run)    cmd_check; cmd_build; cmd_deploy; cmd_launch ;;
  *) echo "usage: $0 [build|deploy|launch|focus|kill|check|run]" >&2; exit 2 ;;
esac
