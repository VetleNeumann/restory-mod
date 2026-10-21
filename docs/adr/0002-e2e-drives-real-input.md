# End-to-end tests drive a virtual Xbox 360 pad, from a separate assembly

The end-to-end harness plays scenarios through a virtual Xbox 360 pad created with the ViGEmBus driver, so Rewired sees a real `Joystick` with the gamepad template, exactly like the player's own pad. The game decides "gamepad in use" only for `ControllerType.Joystick`, and the mod reads R2/L2 raw off the gamepad template of the player's joysticks, so a Rewired `CustomController` would put the game in keyboard-and-mouse mode and never press R2; patching `IPlayerInput` instead would fake each input path by hand and miss exactly the wiring bugs the tests are for. A virtual DualShock 4 was tried first and dropped: Steam Input re-wraps it as an XInput pad that passes no input. For a run the harness assigns the virtual pad to the game's Rewired player and takes the real pad off it, which mutes the player's hands without unplugging anything. The harness lives in its own `RestoryMod.E2E` plugin that only `scripts/dev.sh e2e` deploys (and every other deploy removes), so the shipped mod carries no test code and needs no ViGEm.

## Consequences

Running the suite needs Windows with ViGEmBus installed and Steam Input passing XInput pads through. Button prompts follow the virtual pad (Xbox) unless a scenario sets the "Button prompts" setting.
