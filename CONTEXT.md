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
