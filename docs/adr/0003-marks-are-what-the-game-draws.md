# A mark is what the game draws, not something the mod computes

A thing at the workbench carries one mark, and where the hint and the hover outline meet the hint's mark counts (CONTEXT.md, Mark). That rule has no home in the mod's code on purpose. The hint draws its ghosts on its own outline layer while the hover outline is the game's own preset on the part's view, and the game renders both, so "the hint wins" is simply what ends up on screen. A module that worked out each thing's combined mark would have no caller in the shipped mod, and the only reader that needs the combined mark, the end-to-end harness, has to stay independent of the code it checks. So the harness works out marks itself, from what is drawn: each ghost's colour, width and fill judged against the look table (`LookCheck` against `Looks.Paint`), and the hover outline against the game's own condition presets, with the hint's mark laid over the hover's. It keeps its own copy of the condition-to-preset mapping deliberately: that copy is the check, not duplication.

## Considered options

- A Core module answering "what mark does this thing carry", used by the mod and the harness alike. Rejected: nothing in the mod would ask it, and a harness asking the mod would pass whenever the mod is wrong in the same way.
- The harness reading the mod's record of what it painted (`GamePulse.LookOf`). Replaced for the same reason: painting the wrong colour went unnoticed. With the drawn look checked, painting every condition look in the dirty colour turns five full-repair checks red.
