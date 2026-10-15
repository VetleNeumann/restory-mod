#!/usr/bin/env bash
# Dev loop for the ReStory mod: build -> deploy into BepInEx/plugins -> launch via Steam.
#
# Usage: scripts/dev.sh [build|deploy|launch|kill|check|run]   (default: run)
#   build   dotnet build (Debug)
#   deploy  kill game if running, copy plugin output into $GameDir/BepInEx/plugins/RestoryMod
#   launch  start the game through Steam
#   kill    stop a running game (asks first; KILL_GAME=1 skips the prompt)
#   check   report whether the installed game build changed since the last run
#   run     check + build + deploy + launch
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Non-interactive WSL shells (e.g. over ssh) lack the Windows PATH; we need cmd/tasklist/taskkill.
export PATH="$PATH:/mnt/c/Windows/System32"
for exe in cmd.exe tasklist.exe taskkill.exe; do
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
  if game_running; then
    # Force-closing loses unsaved progress, so confirm unless KILL_GAME=1.
    if [[ "${KILL_GAME:-0}" != 1 ]]; then
      if [[ -t 0 ]]; then
        read -r -p "Restory.exe is running. Force-close it (unsaved progress is lost)? [y/N] " answer
        [[ "$answer" == [yY]* ]] || { echo "aborted: game still running" >&2; exit 1; }
      else
        echo "error: Restory.exe is running. Close it, or set KILL_GAME=1 to force-close." >&2
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

cmd_launch() {
  # Run from /mnt/c so cmd.exe does not start in a UNC working directory.
  (cd /mnt/c && cmd.exe /c start "" "steam://rungameid/$APP_ID")
}

case "${1:-run}" in
  build)  cmd_build ;;
  deploy) cmd_deploy ;;
  launch) cmd_launch ;;
  kill)   cmd_kill ;;
  check)  cmd_check ;;
  run)    cmd_check; cmd_build; cmd_deploy; cmd_launch ;;
  *) echo "usage: $0 [build|deploy|launch|kill|check|run]" >&2; exit 2 ;;
esac
