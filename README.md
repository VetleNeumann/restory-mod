# ReStory mod

A BepInEx 5 mod for *ReStory: Chill Electronics Repairs*, mostly about playing comfortably with a gamepad.

## Features

- **Controller cursor speed**: a "Controller sensitivity" slider in the sound settings, under "Dialogue
  volume" (0.2 to 4.0). The game otherwise fixes the gamepad cursor speed.
- **Controller rotation speed**: a "Rotation sensitivity" slider right under it (0.2 to 4.0) sets how fast
  the right stick spins the device on the workbench. Defaults to 0.4; 1.0 is the game's own speed.
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
- **Shoulder buttons**: away from the workbench, L2/R2 turn the view one step left or right, as moving the
  cursor to the screen edge does (at the workbench they still zoom). In the PC's web browser, L2/R2 switch
  between Marketplace, Home Depot and Parts, and L1/R1 step through the shown tab's categories. L1/R1 also
  step through the inventory's categories, and through the tools in the Tool window while cleaning.
- **Gamepad mail**: in the PC's mail app, L1/R1 step through the mail folders and D-pad up/down through
  the mail in the open folder (hold to repeat). Once you have selected a mail, one stays selected:
  switching folders selects the top mail, taking an order selects the next one, and new mail in an empty
  folder is selected. The list scrolls to keep the selected mail in view.
- **D-pad part sends**: at the workbench, while holding a part with Cross / A, press the D-pad to send it
  without carrying it there: up to the washing bin, right to the tool cup, left to the parts box, down to
  the trash can (or the shredder once you own one). If that place wouldn't take the part (a damaged part
  to the parts box, a clean part to the tool cup, a full or locked washing bin), you keep holding it; a
  damaged part sent left gets the game's own "no point keeping broken parts" message. In the parts box, hold Cross / A on a part and press the D-pad to send it straight
  away, without moving the stick first (a tap still selects the part). Left on a part still showing as
  its parts box icon puts it back. Away from the workbench, while carrying something, D-pad left
  stores bought parts in the parts box and D-pad down throws rubbish (empty boxes and the like) away.
- **Hover grace at the workbench**: with a gamepad, a part, screw, screw slot or bench object stays
  hovered for a moment after the cursor flies past it, so pressing Cross / A just after overshooting
  still grabs or unscrews it ("coyote time"). Anything else the cursor reaches first, a panel, or
  holding rotate cancels it.
- **Screw hints on drop**: when you drop a part you took off a device, the game briefly pulses yellow
  over the screws that are now the last thing holding another part. The pulse now also covers every
  screw that part freed, so none are left out.
- **Tidy screw hints**: every yellow screw pulse (on drop, and the hint button) gives each screw one
  pulse instead of several stacked, brightens again when more screws join while it fades, and drops a
  screw's pulse the moment that screw goes in or comes out.
- **Screw hint button**: at the workbench, the gamepad's Select button (Share / Create on PlayStation,
  View / Back on Xbox) shows the game's own hint (Space on a keyboard), which otherwise has no gamepad
  button. Hold it to keep the pulse lit; it fades once you let go.
- **Every-screw hint**: the game's hint pulses every screw you can unscrew now while you take a device
  apart, and every empty screw slot you can screw into now while you put it back, not only the next
  part's. The game decides which of the two you are doing from your last change to the device. With no
  screw to show, the game's own hint runs.
- **Hitbox overlay** (debug, off by default): at the workbench, draws each thing's hitbox over the game,
  its enlarged hitbox, what the cursor hovers, and a readout of everything under the cursor.

## Configuration

`BepInEx/config/com.zantox.restorymod.cfg` in the game folder. Edits apply while the game runs.

| Section | Key | Default | |
|---|---|---|---|
| Controller | CursorSpeed | 1.0 | Gamepad cursor speed |
| Controller | RotationSpeed | 0.4 | Gamepad speed for spinning the device at the workbench |
| Controller | DropdownModes | true | Gamepad dropdowns |
| Controller | DpadSendParts | true | D-pad part sends |
| Controller | SelectShowsScrewHints | true | Screw hint button |
| Controller | HoverGraceMs | 150 | Hover grace window in ms (0 to 500, 0 = off) |
| Controller | EnlargeSmallHitboxes | false | Minimum hitbox size at the workbench, with a gamepad |
| Controller | MinimumHitboxSize | 4 | Minimum hitbox size in % of screen height (0 to 15) |
| Debug | ShowHitboxes | false | Hitbox overlay at the workbench |
| PartsBox | Sections | true | Parts box sort and sections |
| Notepad | KeyTogglesAtWorkbench | true | Notepad key at the workbench |
| Stick scroll | Enabled | true | Smooth stick scrolling |
| Stick scroll | Speed | 1.5 | View heights per second at full tilt (0.75 to 3) |
| Stick scroll | MaxSpeed / RampTime | 3 / 1 | Holding full tilt speeds up to MaxSpeed over RampTime seconds |
| Stick scroll | CurveExponent | 2 | 1 = linear; higher is gentler near centre (1 to 3) |
| Stick scroll | InnerDeadzone / OuterDeadzone | 0.2 / 0.925 | Tilt where scrolling starts / reaches full speed |
| ShoulderButtons | TriggersLookLeftAndRight | true | L2/R2 turn the view away from the workbench |
| ShoulderButtons | BrowserTabsAndCategories | true | L2/R2 tabs and L1/R1 categories in the web browser |
| ShoulderButtons | InventoryCategories | true | L1/R1 categories in the inventory |
| ShoulderButtons | CleaningTools | true | L1/R1 tools while cleaning |
| ShoulderButtons | MailFolders | true | L1/R1 mail folders in the mail app |
| Mail | DpadSelectsMail | true | D-pad mail selection in the mail app |
| Workbench | DropHintEveryFreedScrew | true | Screw hints on drop |
| Workbench | TidyHintPulse | true | Tidy screw hints |
| Workbench | HintShowsEveryScrew | true | Every-screw hint |
| Stick scroll | Smoothing | 0.07 | Seconds for speed to follow the stick; 0 = instant |
| Stick scroll | Invert | false | Flip direction |

## Development

`scripts/dev.sh run` builds, deploys into the game's `BepInEx/plugins`, relaunches the game through Steam
and brings its window to the front (so a Big Picture session stays on the gamepad). `dotnet test` runs the
unit tests and, with `GamePaths.props` set up, checks every patch target against the installed game.

See [CLAUDE.md](CLAUDE.md) for the full dev loop, patch conventions and game-update checks.
