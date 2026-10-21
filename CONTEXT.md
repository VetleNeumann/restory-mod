# ReStory mod

Vocabulary for the mod and the game concepts it touches.

## Language

**Notepad**:
The book-like panel at the right edge of the screen at the workbench, listing the steps for putting a device back together ("How to reassemble") and recipe notes. The game also calls it the Copybook.
_Avoid_: Journal, book

**Reassembly order**:
The order a device model's parts appear in the Notepad's "How to reassemble" steps. It belongs to the model, not to one device, so it is known without a device on the workbench.
_Avoid_: Build order, assembly order

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

**Hover grace**:
With a gamepad, a thing the virtual cursor can act on stays hovered for a short moment after the cursor leaves its hitbox, so pressing the button just after overshooting still acts on it. At the workbench it covers loose parts, screws and screw slots (not attached parts or part ghosts); away from it, objects on the bench (device boxes, packs, cash, decor). It applies only while the cursor is over nothing else it could act on (a blocked part counts as nothing): anything it could act on now wins, including the on-screen panels, and holding the rotate button ends it. It keeps the hover outline on for the same moment. Platformers call the same forgiveness "coyote time".
_Avoid_: Grab grace, coyote grab, sticky cursor (that slows the cursor instead)

**Blocked part**:
An attached part the game won't let the player take off yet and that a press does nothing to, such as a casing still screwed down. A switch is never a blocked part.
_Avoid_: Locked part, stuck part

**Switch**:
A part on a device that a press flips, toggles or pushes, such as a power button; a press works on it even while it can't come off.
_Avoid_: Button (that is a gamepad button), control

**Screw slot**:
The marked spot on a device where a removed screw goes back; pressing it puts the screw back.
_Avoid_: Screw hole, projection, empty slot

**Part ghost**:
The see-through copy of a removed part shown in its empty socket; a press on it does nothing.
_Avoid_: Projection, empty slot

**Ready part**:
A part or screw the hint points out because the player can act on it right now: while taking a device apart, one that isn't a blocked part and stands in the way of a broken, dirty or unknown part (or is one), or a loose part on the bench that is broken or dirty (it needs cleaning or replacing before it goes back, so the hint stays on taking apart); while putting it back together, a screw whose slot can take it now, or a loose part whose socket can take it now.
_Avoid_: Next part, available part, removable part

**Unknown part**:
A part whose condition the player hasn't seen yet: it has never been taken out and has not yet been a ready part; the notepad lists it as unknown. A part counts as seen once the hint shows it as ready (R2) or the player hovers it while it is ready, so its condition shows right before the player would pick it up. Nothing reveals the condition of a part still out of reach.
_Avoid_: Uninspected part, hidden part

**Missing socket**:
A socket that could take a part right now but that no loose part on the bench is left to fill, each loose part counting toward one socket of its kind; the parts box lists the parts that could fill it under "Missing parts". The hint shows its part ghost with the missing look.
_Avoid_: Empty slot, missing part

**Hint**:
The workbench aid that points out every ready part (a pulse on an attached part, screw or loose part, a part ghost in each screw slot a screw can fill), shows missing sockets with the missing look, and follows whether the player is taking the device apart or putting it back together; once no part left on the device needs work or is unknown, it shows putting back even before the first part goes in. R2 shows it again: while held, or until pressed again, as the "R2 hint" setting picks (Hold, Toggle or Off).
_Avoid_: Highlight, drop hint, screw hint (that was its first, screws-only form)

**Hover outline**:
The game's own outline on the one thing under the virtual cursor at the workbench, coloured by what a press would do (orange when it acts) and, on a part, by its condition. It never shows on a blocked part; a switch, or a part still going into its socket, keeps it.
_Avoid_: Highlight (on its own it could mean the hint too), selection outline, hover highlight

**Look**:
One of a fixed set of ways the hint and the hover outline mark a thing: the ready look (the pulse on a ready part, and its ghost where it goes back), the missing look, the screw look (the game's own pulse on a screw or screw slot, hidden or not as a whole, since a screw is too small to be half hidden), and the condition look (burnt, damaged, dirty: the game's own condition colours, on a ready part's pulse and on its hover outline). Each thing gets its own outline, never merged with its neighbours, so overlapping things all stay visible.
_Avoid_: Style, highlight colour, preset

**Missing look**:
The look of a missing socket: a patterned, unmistakable mark that reads as "a part belongs here" without relying on colour.
_Avoid_: Red ghost

**Hidden style**:
How any look is drawn where something stands in front of the thing it marks, whether the device or another marked thing: only its outline shows through, thinner and dimmer than in plain view; the fill never does.
_Avoid_: X-ray, see-through highlight

**Faded**:
Workbench state where the device on the bench shows only as a flat gray see-through silhouette and nothing on it takes a press: the cursor falls through to whatever lies behind it, such as a loose part dropped out of reach, and a held part dropped on it lands on the bench. It can still be rotated. L1 fades the device and brings it back, unless a screen that uses L1 (the Tool window, the inventory) claims it first; leaving the workbench or changing device brings it back too.
_Avoid_: Ghost (a part ghost is something else), hidden (a Notepad state), x-ray

**Mark**:
The visible treatment one thing at the workbench carries at a given moment: a pulse, a part ghost (the ready look or the missing look), a hover outline, or none. A mark on a thing that is there (an attached part, a screw, a loose part) is a pulse; one in an empty socket is a part ghost. A missing socket always carries the missing look, even a battery tube whose entry holds a battery with room behind it. Every thing carries exactly one; where the hint and the hover outline meet on one thing, the hint's mark counts.
_Avoid_: Highlight, outline (when any mark is meant), glow

**Pulse**:
The hint's mark on a thing that is there, an attached part, a screw or a loose part: an outline that runs white to yellow and back.
_Avoid_: Flash, blink, highlight

### Hitboxes

**Hitbox**:
The on-screen area within which the cursor hovers a thing; its size follows how big the thing looks.
_Avoid_: Collider, hit area, click area

**Native hitbox**:
A hitbox as the game itself makes it, before any change by the mod.
_Avoid_: Original hitbox, real hitbox

**Minimum hitbox size**:
The smallest a hitbox may be on screen at the workbench while a gamepad is in use and nothing is held, as a share of screen height. A smaller thing that a press acts on, and whose centre the camera can see, gets an enlarged hitbox.
_Avoid_: Hitbox scale, pick radius (that grows every hitbox, large ones too)

**Enlarged hitbox**:
A native hitbox grown evenly until its narrowest width reaches the minimum hitbox size. It beats the native hitbox of any larger thing under the cursor, and of anything a press does nothing to, but never an on-screen panel; where enlarged hitboxes overlap, the thing whose native hitbox lies nearest the cursor wins.
_Avoid_: Grown hitbox, padded hitbox, assisted hitbox

**Hitbox overlay**:
A debug view at the workbench, off unless switched on in the mod's config, that draws native and enlarged hitboxes over the game (enlarged ones even while enlarging is switched off) and lists what lies under the cursor.
_Avoid_: Debug view, gizmos, wireframe

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
The workbench state where one part sits on the cleaner to be brushed, blown or soldered. Soldering is part of Cleaning, not a separate mode. R2 works the tool exactly as the grab input does (A on Xbox, Cross on PlayStation), as it does when painting, unless the triggers zoom.
_Avoid_: repair mode, repairing

**Tool window**:
The sliding panel of Cleaning tools shown during Cleaning. It lists only tools the player owns, and only the best brush owned.

**Cleaning tool**:
The tool picked in the Tool window, used when Cleaning. L1/R1 step through the Tool window's slots in on-screen order, wrapping at the ends, even mid-action.
_Avoid_: equipment, selected item

### PC

**PC app**:
One program on the PC's taskbar, such as the web browser or the Mail app. The taskbar shows every installed PC app, and at most one is open at a time: opening another closes the one that was open, which starts fresh next time.
_Avoid_: program, window, application (in player-facing text)

**App switch**:
Closing the open PC app and opening its neighbour on the taskbar with one D-pad left/right press, in on-screen order and wrapping at the ends. From the bare desktop or the start menu, right opens the first PC app and left the last. It does nothing while a dropdown is open, while a PC app is being installed, while typing in a text or number field, or while a hack runs, its popups included (every gamepad button then feeds the hack). The taskbar's own highlight is the only cue.
_Avoid_: alt-tab, tab switch (that steps browser tabs)

### Mail

**Mail app**:
The PC app where mail arrives and orders are taken.
_Avoid_: email client, inbox (that is one folder)

**Mail**:
Any message in the Mail app. Every mail is either an order or a letter.
_Avoid_: email, message, letter (when any mail is meant)

**Order**:
A mail offering a repair job. Taking it sends the device for delivery and moves the mail to Orders in progress. It cannot be declined.
_Avoid_: job, request

**Letter**:
A mail that is not an order: story mail, answered with the game's Ok, Yes or No buttons.
_Avoid_: narrative email

**Mail folder**:
One of the lists along the top of the Mail app: Orders, Orders in progress, General, Spam, Recycle bin. Opening a mail folder shows its mail. L1/R1 step between them in on-screen order, wrapping at the ends; the D-pad steps through the mail in the open folder, stopping at the ends.
_Avoid_: tab, inbox, category

**Selected mail**:
The mail shown in the Mail app's reading pane; at most one. Once the player selects any mail, the Mail app keeps one selected until it closes: opening another mail folder selects its top mail, and when the selected mail leaves its folder (an order taken) the mail that takes its place is selected, or the one above it if it was last. A mail folder with no mail has none selected, until mail arrives in it. Selecting by D-pad or by a click starts this alike. The view always scrolls to keep the selected mail in sight.
_Avoid_: open mail (Open is a Notepad state), current mail, highlighted mail

**Button prompts**:
The small button icons the game shows next to an action (Select, Back, and the like). With a PlayStation pad the mod draws them with the game's unused PS5 icons; every other pad gets the game's own A/B/X/Y icons. The "Button prompts" setting picks Auto (follow the pad), PlayStation or Generic.
_Avoid_: glyphs (in player-facing text), hints, button hints
