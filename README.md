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
  cursor hides until you move the stick. The cursor takes over again once it reaches another row. The list scrolls to keep the highlighted row
  in view. A closed dropdown no longer reopens when you press Cross / A with the cursor elsewhere.

## Configuration

`BepInEx/config/com.zantox.restorymod.cfg` in the game folder. Edits apply while the game runs.

| Section | Key | Default | |
|---|---|---|---|
| Controller | CursorSpeed | 1.0 | Gamepad cursor speed |
| Controller | DropdownModes | true | Gamepad dropdowns |
| PartsBox | Sections | true | Parts box sort and sections |
| Notepad | KeyTogglesAtWorkbench | true | Notepad key at the workbench |

## Development

See [CLAUDE.md](CLAUDE.md) for the dev loop, patch conventions and game-update checks.
