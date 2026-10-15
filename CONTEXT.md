# ReStory mod

Vocabulary for the mod and the game concepts it touches.

## Language

**Notepad**:
The book-like panel at the right edge of the screen at the workbench, listing the steps for putting a device back together ("How to reassemble") and recipe notes. The game also calls it the Copybook.
_Avoid_: Journal, book

**Peeking**:
Notepad state where only its edge shows at the right of the screen; hovering it (or holding the game's notepad key) slides it out.
_Avoid_: Minimized, hover mode

**Open**:
Notepad state where it is fully slid out and readable.
_Avoid_: Maximized, expanded

**Hidden**:
Notepad state where it is not on screen at all.

**Pinned**:
Player choice, saved with the game, that keeps the Notepad Open instead of Peeking.

**Notepad key**:
The game's own input for showing and hiding the Notepad: Square / X on a gamepad, J on a keyboard, following any rebinding. The game ignores it at the workbench; the mod makes it switch the Notepad between Peeking and Open there.
_Avoid_: R1, journal button

**Workbench session**:
The span from moving to the workbench view to leaving it, covering both taking a device apart and putting it back together.
_Avoid_: Reassembly mode, disassemble mode

**Stick scroll**:
Moving a scrollable menu's view with the right stick, gliding at a speed that follows how far the stick is tilted. It moves the view, not the selected item; the D-pad and left stick move the selection.
_Avoid_: Wheel scroll, stick navigation

### Sending parts

**Held part**:
A part the player carries with the cursor at the workbench while holding the game's grab input (A on Xbox, Cross on PlayStation); letting go drops it where the cursor points.
_Avoid_: dragged part, selected part

**Carried object**:
Something the player carries away from the workbench while holding the grab input: a box, a device, rubbish, bought parts.
_Avoid_: held object, dragged object

**Rubbish**:
A carried object the game lets the player throw away, such as an empty box.
_Avoid_: trash object, junk

**Bought parts**:
A carried object holding parts bought in the shop; dropped on the parts box, its parts move into it.
_Avoid_: shipment, delivery (those are devices)

**Destination**:
A place that takes a held part or carried object away in one move. At the workbench: the parts box, the washing bin, the tool cup, the trash can or the shredder. Away from it: the parts box (bought parts) and the trash can or shredder (rubbish). A device socket, the bench surface, shelves and other spots the player aims at are not destinations.
_Avoid_: drop target, slot

**Send**:
Moving a held part or carried object straight to a destination with one D-pad press instead of carrying it there. A send the destination would refuse leaves the thing held; only a damaged part sent to the parts box says why, with the game's own message.
_Avoid_: quick drop, throw

**Parts box**:
The player's inventory of loose parts, shown at the workbench.
_Avoid_: inventory (when the workbench box is meant), storage

**Washing bin**:
The unlockable ultrasonic bath that cleans parts. It refuses damaged parts and takes no more once full.
_Avoid_: sonic bath, ultrasonic

**Tool cup**:
The spot right of the bench where a part is cleaned or soldered by hand. It takes only parts that are dirty or need soldering.
_Avoid_: cleaner

**Trash can**:
Where damaged parts and rubbish are thrown away. It takes nothing else.

**Shredder**:
Bought equipment that destroys damaged parts for coins, and rubbish for nothing. It takes nothing else, and once owned it stands in the trash can's place: the workbench has one or the other, never both.

### Gamepad in dropdowns

**Virtual cursor**:
The on-screen mouse pointer the game drives from the left stick when a gamepad is in use.
_Avoid_: mouse (when a gamepad is meant), pointer

**Highlighted row**:
The option in an open dropdown that the D-pad moves between and that a submit would pick.
_Avoid_: selected item, selection

**Pointer mode**:
While a dropdown is open, the state in which the stick moves the virtual cursor and A (Cross on PlayStation) clicks whatever is under it; the stick never moves the highlighted row.
_Avoid_: mouse mode, cursor mode

**Nav mode**:
While a dropdown is open, the state in which the D-pad moves the highlighted row and A submits it; the virtual cursor's clicks are ignored, and it stays hidden until the stick moves.
_Avoid_: D-pad mode, keyboard mode, navigation mode

**Mode switch**:
The change between pointer mode and nav mode, driven by the most recent deliberate input: a D-pad press enters nav mode, and the virtual cursor moving onto a row other than the one it was over when nav mode began enters pointer mode. Scrolling and cursor movement off the list switch nothing.
_Avoid_: input device change (that is the game's own gamepad-versus-keyboard switch)

### Shoulder buttons

**Look direction**:
Which way the player faces when not at the workbench: Main (straight ahead), Left or Right. The game turns to Left or Right when the mouse cursor touches that screen edge, and back to Main when it moves away; L2/R2 step one direction at a time, stopping at Left and Right. Some screens (the PC, the Notepad, the inventory, dialogue, payment) hold the look direction where it is.
_Avoid_: rotate camera, camera angle, side view

**Browser tab**:
One of the sites in the PC's web browser: Marketplace, Home Depot, Parts. L2/R2 step between them, wrapping at the ends.
_Avoid_: page, site, store, shop

**Category bar**:
The row of category toggles that filters a list: on each browser tab (Marketplace and Parts include "All"; Parts includes Licenses) and in the inventory (starting with "All"). L1/R1 step along it in on-screen order, wrapping at the ends.
_Avoid_: filter, tabs, sub-tabs

**Cleaning**:
The workbench state where one part sits on the cleaner to be brushed, blown or soldered. Soldering is part of Cleaning, not a separate mode.
_Avoid_: repair mode, repairing

**Tool window**:
The sliding panel of Cleaning tools shown during Cleaning. It lists only tools the player owns, and only the best brush owned.

**Cleaning tool**:
The tool picked in the Tool window, used when Cleaning. L1/R1 step through the Tool window's slots in on-screen order, wrapping at the ends, even mid-action.
_Avoid_: equipment, selected item
