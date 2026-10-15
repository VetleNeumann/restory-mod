# ReStory mod

A BepInEx 5 mod for *ReStory: Chill Electronics Repairs*, mostly about playing comfortably with a gamepad.

## Features

- **Controller cursor speed**: a "Controller sensitivity" slider in the sound settings, under "Dialogue
  volume" (0.2 to 4.0). The game otherwise fixes the gamepad cursor speed.
- **Parts box sections**: while a device is on the workbench, the parts box sorts in "How to reassemble"
  order and splits into *Missing parts* and *Other parts*. Within a step, burnt parts come first, then
  damaged, dirty and perfect.
- **Notepad key at the workbench**: the Notepad key (Square / X on a gamepad, J on a keyboard) switches
  the Notepad between peeking at the screen edge and fully open, so you don't have to move the cursor
  there. It stays open until you press the key again, pin it, or leave the workbench. Away from the
  workbench the key works as in the unmodded game.
- **Gamepad dropdowns**: in every dropdown (settings, parts box filter, parts shop, paint palettes) the
  gamepad drives either the cursor or the list, never both. Use the stick and the cursor clicks (Cross / A)
  where it points; press the D-pad and it moves the highlighted row, Cross / A picks that row, and the
  cursor hides until you move the stick. The cursor takes over again once it reaches another row. The list
  scrolls to keep the highlighted row in view. A closed dropdown no longer reopens when you press Cross / A
  with the cursor elsewhere.
- **Smooth stick scrolling**: the right stick glides scrollable menus, faster the further you tilt it,
  instead of the game's step-pause-step scrolling. Holding it all the way speeds up for long lists. It
  scrolls the menu holding the selection, or the one under the cursor.

## Configuration

`BepInEx/config/com.zantox.restorymod.cfg` in the game folder. Edits apply while the game runs.

| Section | Key | Default | |
|---|---|---|---|
| Controller | CursorSpeed | 1.0 | Gamepad cursor speed |
| Controller | DropdownModes | true | Gamepad dropdowns |
| PartsBox | Sections | true | Parts box sort and sections |
| Notepad | KeyTogglesAtWorkbench | true | Notepad key at the workbench |
| Stick scroll | Enabled | true | Smooth stick scrolling |
| Stick scroll | Speed | 1.5 | View heights per second at full tilt (0.75 to 3) |
| Stick scroll | MaxSpeed / RampTime | 3 / 1 | Holding full tilt speeds up to MaxSpeed over RampTime seconds |
| Stick scroll | CurveExponent | 2 | 1 = linear; higher is gentler near centre (1 to 3) |
| Stick scroll | InnerDeadzone / OuterDeadzone | 0.2 / 0.925 | Tilt where scrolling starts / reaches full speed |
| Stick scroll | Smoothing | 0.07 | Seconds for speed to follow the stick; 0 = instant |
| Stick scroll | Invert | false | Flip direction |

## Development

`scripts/dev.sh run` builds, deploys into the game's `BepInEx/plugins`, relaunches the game through Steam
and brings its window to the front (so a Big Picture session stays on the gamepad). `dotnet test` runs the
unit tests and, with `GamePaths.props` set up, checks every patch target against the installed game.

See [CLAUDE.md](CLAUDE.md) for the full dev loop, patch conventions and game-update checks.
