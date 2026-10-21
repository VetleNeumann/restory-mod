# TODO: rework highlighting

- Missing-part highlighting isn't intuitive enough.
- Overlapping items: when several highlighted items overlap, you only see one.
- While removing a part from the item on the workbench, keep showing whether it was broken/dirty
  before you grabbed it (same info as the hover highlight).
- Maybe: different highlight color for taking apart vs. putting together.
- Combine with the "before you've picked it up" idea: on the last part, I remove it just to check
  whether it's dirty. It should count as "discovered" so its state shows without picking it up.
- Highlight shape: hint ghosts now take the part's drawn meshes (GhostFit), which fixed cables and
  taking-apart screws. Still to check: GameDuck front case, putting back: the missing-socket ghosts
  (D-pad and A/B membranes) sat shifted from their holes; see whether the prefab fit lines them up.
  Screw slot ghosts (putting back) still use the game's lifted pose, matching its pressable marker.
- Red highlight fix: on SOME items, hovering a part that is blocked for now (can be picked up later)
  highlights it RED. Usually blocked parts don't light up at all. Remove the red case so these
  items behave like the rest: no highlight unless the part can be picked up.
- Hint switches to putting back too early. Atari Lynx: with no part left in the device dirty,
  broken or unknown, the hint showed putting back (screw slots, batteries), but after putting it
  together the game said "Something's not right...". The hint's "done" rule only looks at parts in
  sockets; the game's check (DeviceService.CheckDeviceReadyForShipment) wants every order work
  type done and the device's quality ideal. Find what the hint misses (loose dirty or damaged parts?
  how quality is worked out) and match it. Also seen: a put-back screw slot's ghost looks like the
  screw still being there; consider a distinct look if it still confuses once the switch is right.
- R2 highlight ignores occlusion: it draws through the device even when the part is behind it.
  Show hidden parts differently (dimmer, outline only, etc.) so it reads better.
