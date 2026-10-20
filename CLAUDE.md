# ReStory mod

BepInEx 5 + HarmonyX mod for *ReStory: Chill Electronics Repairs* (Steam app 3812600, Unity 6000.3 Mono,
Rewired input, Zenject DI). Personal repo. Game installed on this box; `GamePaths.props` (gitignored) points at it.

## Dev loop

- `scripts/dev.sh run`: closes the game gracefully, builds, deploys to `BepInEx/plugins/RestoryMod/`,
  relaunches via Steam, prints the mod's startup lines, then brings the game window to the front
  (Windows would leave it behind Big Picture). `scripts/dev.sh focus` does only that last step. Force-close asks first (`KILL_GAME=1` to skip).
- `scripts/dev.sh hot`: builds and hot-reloads into the running game through ScriptEngine (pinned r11.2,
  installed on first use), with no restart. The first `hot` after a `run` restarts once to move the mod from
  `plugins/RestoryMod/` to `BepInEx/scripts/`; `run` moves it back. A reload unpatches, runs
  `Plugin.Teardown`, then loads the new copy, which picks up scene-load setup at the end of `Plugin.Awake`.
  A copy hands over only what it saw itself: after the first `hot` from an older build, reload the scene once.
- `scripts/screenshot.sh`: DPI-aware capture of the Windows screen; read the PNG to see the game.
- Log: `$GameDir/BepInEx/LogOutput.log`. Config: `$GameDir/BepInEx/config/com.zantox.restorymod.cfg`;
  the plugin watches it, so hand edits apply live.
- Vetle often tests with a **gamepad only**, no keyboard: every feature must work by D-pad/stick.
  UnityExplorer (F7) is installed with `Hide On Startup = true` and the EventSystem override off.

## Game source

Decompiled `Restory.Assembly.dll` lives in `~/restory-decompiled/<buildid>/` (outside the repo; never commit
decompiled code). Regenerate after an update:
`dotnet ilspycmd -p -o ~/restory-decompiled/<buildid> -r "$GameDir/Restory_Data/Managed" "$GameDir/Restory_Data/Managed/Restory.Assembly.dll"`.
Delegate exploration of it to investigator subagents; it is ~2,700 files.

## After a game update

`dev.sh` warns when the Steam buildid changes. Run `dotnet test`: a red TargetCheck case names the feature
and the target that moved. At runtime the patch that checks that target skips itself until fixed (some
features check per patch, so only that patch goes). A compiled-against member checked only by TargetCheck
fails when the code reaches it instead.

## Adding a patch

1. One patch class per feature; `Prepare()` returns `GameTargets.AllPresent(...)` (the feature's manifest,
   or a subset per patch) so a moved target skips only that feature. Register it in `Plugin.cs`.
2. List every game member the code touches in the feature's manifest in `FeatureManifests` (Core),
   including ones compiled against directly, so TargetCheck covers them; members reached by name also get a
   `PatchTargets` field. `PatchTargets.All` is derived from the manifests.
3. Pure logic (orderings, ranges, rules) goes in `RestoryMod.Core` with unit tests; CI runs only Core.
4. Prefix/postfix only. HarmonyX here is 2.9.0: `[HarmonyOptional]` does not exist.
5. Anything left in the game outside the plugin's own GameObject (cloned or added objects and components,
   rewired navigation, listeners on game objects) gets an undo step in `Plugin.Teardown`, so `dev.sh hot`
   reloads cleanly. See `SettingsRowsPatch`. Setup that runs once per scene (Zenject `Construct`,
   `Initialize`, Awake) also needs a load-time path in `Plugin.Awake`: find live MonoBehaviours
   (`PartsBoxSections.AttachToPanels`), or for plain Zenject classes take the old copy's references from
   `Handoff` (`NotepadKeyListenPatch.Resume`).

## Unity gotchas learned the hard way

- `UnityEngine.Object` has a fake null: compare with `!obj` / `if (obj)`, never `??`, `??=`, `?.`.
- `LayoutGroup` is `[DisallowMultipleComponent]` (subclasses too): a second layout on a GameObject must
  implement `ILayoutGroup`/`ILayoutElement` directly (see `SectionedGridLayout`).
- The parts box rebuilds by `DetachChildren()`, which keeps world scale: reset local scale/rotation when
  re-parenting our objects, or they shrink every refresh.
- Never rebuild the parts box list while a drag/drop is in flight: the game holds a pooled slot
  presenter (`draggedSlot`) across the drag, and a rebuild re-binds it to another part, which the
  game then hides or removes. `PartsBoxSections.TransferInProgress` guards the re-sort poll.
- Settings rows use **explicit** navigation; splice new selectables in. A `Slider` changes value on
  left/right only while its left/right links are empty.
- First-open hitches: do one-time work (object creation, `FindAnyObjectByType`, JIT) at scene load in the
  presenter's Zenject `Construct`. `[Debug] PartsBoxDiagnostics` logs timing.
- Non-interactive WSL shells lack the Windows PATH; `dev.sh` adds System32 itself.

## Vetle's settled preferences

- Controller sensitivity: row directly under "Dialogue volume"; 0.2–4.0 in 0.1 steps, one decimal, no "x".
- Rotation sensitivity (right-stick spin at the workbench): row directly under "Controller sensitivity", same
  range and format; default 0.4 (1.0 = game speed, far too fast).
- Parts box: "Missing parts" / "Other parts" headers as they are now (label + thin rule). Missing is per
  socket, matching the game's notepad. Sort: section, then "How to reassemble" order (only this flips with
  the sort button), then burnt → damaged → dirty → perfect.

## Background

`docs/research/mod-repo-structure.md`: read when changing tooling, CI, loader version or update-resilience
patterns.
`docs/research/controller-support.md`: read when touching button prompts, controller detection, Steam
Input or rebinding (the game ships a remap backend with no UI).

Ideas not started: camera look speed (`CameraRotationController`, legacy `Input.GetAxis`, `rotationSpeed`
100) and shoulder buttons in the PC apps other than the browser and mail.
