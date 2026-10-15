# Analog-stick scrolling of menus and lists

Research date: 2026-10-15. Context: a BepInEx mod for *ReStory* (Unity 6000.3, uGUI `ScrollRect`, Rewired input). The right stick scrolls the settings list and the parts-box grid, and it currently feels clunky.
Tags: **UNVERIFIED** = could not confirm from a primary source. **SECONDARY** = only a non-primary source was found. **INFERENCE** = my own reasoning, not a sourced claim.

Source permalinks are pinned to: uGUI `8541553` (main), Unity InputSystem `05e45dd` (develop), Godot `4d10419` (master).

---

## TL;DR recommendation

- **Use two modes.** The right stick scrolls the viewport *continuously*: velocity comes from deflection, in pixels per second, multiplied by `unscaledDeltaTime`. The D-pad and left stick keep moving the *selection* in steps with key repeat, and the viewport follows the selection. This matches the split the platforms make: XY focus navigation for discrete moves, with the scroll view following focus, and analog or "mouse mode" movement for free scrolling ([MS gamepad guidance](https://learn.microsoft.com/en-us/windows/apps/design/input/gamepad-and-remote-interactions), [Apple TV focus](https://developer.apple.com/library/tvos/documentation/General/Conceptual/AppleTV_PG/WorkingwiththeAppleTVRemote.html), [Godot `follow_focus`](https://github.com/godotengine/godot/blob/4d10419cacd6d3009976e8f9fd3abd970fd58018/scene/gui/scroll_container.cpp#L378-L393)).
- **Deadzone: scaled radial**, inner about 0.2 and outer about 0.925. After the deadzone, use **only the axis the list scrolls along**, and only while that axis dominates (|y| > |x|). This keeps a sideways push from scrolling the list.
- **Curve:** square the post-deadzone magnitude (exponent 2), so a light push gives fine control and a full push gives top speed.
- **Speed:** at full deflection, about **1.5 viewport heights per second**, scaled by a user setting with at least a 0.5× to 2× range. Work in pixels and convert to `normalizedPosition` through the hidden length, so a long list doesn't scroll faster than a short one.
- **Feel:** ramp velocity in and out over about 60–80 ms (exponential smoothing). There should be **no** coasting after release. Clamp at the ends, with no elastic bounce, and zero the `ScrollRect` velocity on every write.
- **Stepped repeat (D-pad):** 0.5 s delay then 0.1 s per step, which are Unity's defaults. Optionally speed up to about 0.05 s per step after about 1 s of holding.

---

## 1. Continuous vs stepped scrolling

**Findings**
- Stepped navigation with repeat is the engine default for focus movement. Unity's `InputSystemUIInputModule` turns the move vector into one `MoveDirection`, using the dominant axis, and fires a move event. It waits `moveRepeatDelay` (default **0.5 s**) before the first repeat, then fires every `moveRepeatRate` (default **0.1 s**) ([source L820-L840](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L820-L840), [defaults L945-L952](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L945-L952)). The legacy `StandaloneInputModule` uses the same 0.5 s delay with `inputActionsPerSecond = 10` ([source L56-L59](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/EventSystem/InputModules/StandaloneInputModule.cs#L56-L59)).
- Microsoft's Xbox/TV guidance maps the D-pad and left stick to XY focus navigation, one element per step. It notes that a `ScrollViewer` with no focusable content can't be read by XY navigation alone. For free-form surfaces it recommends **mouse mode**, an analog pointer, instead. It also lists **triggers = page up/down** as a built-in accelerator for `ListView`/`ScrollViewer` ([MS gamepad & remote interactions](https://learn.microsoft.com/en-us/windows/apps/design/input/gamepad-and-remote-interactions)).
- Unreal Common UI has a separate analog-*scroll* path in its analog cursor, configured by `ScrollDeadZone`, `ScrollUpdatePeriod` and `ScrollMultiplier` ([FCommonAnalogCursorSettings](https://dev.epicgames.com/documentation/unreal-engine/API/Plugins/CommonUI/FCommonAnalogCursorSettings)). The API page documents neither semantics nor defaults. Several shipped projects' configs, apparently copied from Lyra, set `ScrollDeadZone=0.2, ScrollUpdatePeriod=0.1, ScrollMultiplier=2.5` ([OpenTournament DefaultInput.ini L114](https://github.com/OpenTournament/OpenTournament/blob/master/Config/DefaultInput.ini#L114)). **SECONDARY**: the engine header is EULA-gated. Judging by the field names, Common UI emits a scroll tick every 0.1 s, scaled by the multiplier. That is a *hybrid*: a periodic step whose size depends on deflection. **UNVERIFIED**.
- Slate's `FNavigationConfig::GetRepeatRateForPressure(InPressure, InRepeats)` makes the **repeat rate depend on stick deflection**. The docs describe it as "if the user moves the stick a little, we would navigate slowly, if they move it a lot, we would repeat the navigation often" ([FNavigationConfig](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Slate/FNavigationConfig), [GetRepeatRateForPressure](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Slate/FNavigationConfig/GetRepeatRateForPressure)). The exact numbers are in EULA-gated source. **UNVERIFIED**.

**Implications for the mod** (INFERENCE)
- Stepped repeat suits short lists and anything that changes selection, like settings rows. Continuous scrolling suits a long grid you skim with your eyes, like the parts box. The hybrid both engines point to is: right stick = continuous viewport scroll; D-pad/left stick = stepped selection with repeat; triggers/bumpers = page jumps.
- Unreal's pressure-scaled repeat is a reasonable fallback if continuous scrolling ever has to snap to rows: make the step interval shrink as deflection grows.

## 2. Deadzones

**Findings**
- Josh Sutphin's article, the de-facto reference, sets out three kinds. **Axial**: each axis is clamped separately, which snaps to the cardinals and suits 4-way movement. **Radial**: the vector magnitude is clamped, but "all the precision that exists inside the dead zone is completely lost" and there is a visible kick at the edge. **Scaled radial**: radial, then rescaled from the deadzone to 1, so there's "no longer a visible edge". It says typical values are "often somewhere between 0.1 to 0.2", and the sample uses 0.25 because OUYA pads needed it ([Game Developer repost, 2013-04-16](https://www.gamedeveloper.com/business/doing-thumbstick-dead-zones-right)).
- XInput's sample uses a **circular** (radial) deadzone and rescales the magnitude to 0–1 past it. Its recommended constants are `XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE 7849` (≈0.24) and `RIGHT_THUMB_DEADZONE 8689` (≈0.265) out of 32767 ([Getting started with XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput)).
- Unity Input System's `StickDeadzoneProcessor` is radial and scaled: magnitudes below `min` become 0, magnitudes above `max` become 1, and `(|v|-min)/(max-min)` in between ([source L30-L52](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Controls/Processors/StickDeadzoneProcessor.cs#L30-L52)). The defaults are **min 0.125, max 0.925** ([InputSettings L783-L784](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/InputSettings.cs#L783-L784)).
- Godot's default action deadzone is **0.2** (`InputMap::DEFAULT_DEADZONE`) ([input_map.h L55](https://github.com/godotengine/godot/blob/4d10419cacd6d3009976e8f9fd3abd970fd58018/core/input/input_map.h#L55)).
- Unreal Enhanced Input offers `Axial`, `Radial` ("Smoothed Radial") and `UnscaledRadial` ([EDeadZoneType](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/EnhancedInput/EDeadZoneType)). Its `UInputModifierDeadZone` has `LowerThreshold`, below which input is ignored, and `UpperThreshold`, above which input clamps to 1 ([UInputModifierDeadZone](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/EnhancedInput/UInputModifierDeadZone)).
- **Rewired**, which the game uses, has `DeadZone2DType.Radial` ("the smoothest stick input") and `Axial` ("better for games that primarily use the four cardinal directions") ([DeadZone2DType](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_DeadZone2DType.htm)). Its per-axis `AxisCalibration.deadZone` zeroes values at or below the threshold ([AxisCalibration](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_AxisCalibration.htm)). `ApplyRadialDeadZone` and `DeadZone2DType` both appear as strings in the game's `Rewired_Core.dll` (local check). Whether ReStory's Rewired setup already applies a radial deadzone to the right stick isn't known. **UNVERIFIED**: read the raw `GetAxis2D` at rest to check.
- Ryan Juckett's pipeline for INVERSUS does radial inner and outer deadzones first. It handles cardinal snapping with separate **angular deadzones** ("four dead zones (up, down, left, right)") that blend, so "the output never pops". It gives no numeric values and says "there likely aren't any recommended values for the outer dead zone" ([Interpreting Analog Sticks, 2016-03-24](https://blog.hypersect.com/interpreting-analog-sticks/)).
- Axis bleed in uGUI: `ScrollRect.OnScroll` on a **vertical-only** ScrollRect *copies x into y* when |x| > |y|. That means a sideways push fed in as a scroll delta scrolls the list ([ScrollRect.cs L662-L679](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L662-L679)). The UI module's navigation instead picks the dominant axis ([L818-L824](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L818-L824)).

**Implications** (INFERENCE)
- Do a scaled radial deadzone on the full 2D vector, inner 0.2 and outer 0.925, then take only y. Treat y as zero unless |y| > |x|: that is a 45° angular gate, the simplest form of Juckett's angular deadzone. Don't call `ScrollRect.OnScroll` with the raw stick vector; write the position yourself.

## 3. Response curves

**Findings**
- Microsoft: after the deadzone you may "optionally apply a non-linear transform". For driving games, "cube the result … more precision in the lower ranges … gamers typically either apply soft force to get subtle movement or apply hard force all the way" ([XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput)).
- Juckett: game sensitivity curves are "more than likely going to start low and curve upward" ([Hypersect](https://blog.hypersect.com/interpreting-analog-sticks/)).
- Rewired's `AxisSensitivityType.Power` raises input to a power. Above 1 it is less sensitive near the centre, below 1 more sensitive ([AxisSensitivityType](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_AxisSensitivityType.htm)). Unreal has a `ResponseCurveExponential` input modifier ([UInputModifierResponseCurveExponential](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/EnhancedInput/UInputModifierResponseCurveExponential)); its default exponent is **UNVERIFIED**.
- Steam Input's developer guide says good results need "deadzones that go to 0 and a linear acceleration curve", and asks that deadzone, acceleration and sensitivity be configurable ([Steam Input getting started](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs)). The context is joystick-as-camera/mouse input.

**Implications** (INFERENCE)
- Exponent 2 is a good default for menu scrolling: a half push gives 25 % speed, which suits reading while scrolling. Exponent 3 suits fine aim. Make it a config value in the range 1–3. Apply the curve to the post-deadzone magnitude, not to each axis separately, so diagonal response stays consistent.

## 4. Speed, frame-rate independence, scaling

**Findings**
- uGUI's own inertia and spring code is frame-rate independent and uses `Time.unscaledDeltaTime`. Velocity decays by `Mathf.Pow(decelerationRate, dt)` ([ScrollRect.cs L860-L889](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L860-L889)). `OnScroll`, by contrast, adds `delta * scrollSensitivity` once per event with **no dt** ([L662-L695](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L662-L695)). If you call it every frame, speed is proportional to frame rate.
- Scroll-wheel step sizes for comparison:
  - Unity's default `scrollSensitivity` is 1.0 ([L277](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L277)). The Input System UI module's `scrollDeltaPerTick` defaults to 6.0, and a value of 1 "behaves the same as the legacy Standalone Input Module" ([L180-L190, L2602](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L2602)).
  - Godot scrolls one wheel tick by **page / 8**, so the step scales with the viewport ([scroll_container.cpp L207-L222](https://github.com/godotengine/godot/blob/4d10419cacd6d3009976e8f9fd3abd970fd58018/scene/gui/scroll_container.cpp#L207-L222), [scroll_bar.h L124](https://github.com/godotengine/godot/blob/4d10419cacd6d3009976e8f9fd3abd970fd58018/scene/gui/scroll_bar.h#L124)).
- The Lyra-derived Common UI analog cursor uses `CursorMaxSpeed=2200`, `CursorAcceleration=1500` and `CursorDeadZone=0.25` (units presumably px/s and px/s²) ([OpenTournament config](https://github.com/OpenTournament/OpenTournament/blob/master/Config/DefaultInput.ini#L114)). **SECONDARY**.
- I found **no primary source that gives a recommended analog scroll speed** in px/s or viewports/s.

**Implications** (INFERENCE)
- Express speed relative to the viewport (Godot's approach) so it holds at every resolution and UI scale. Unity's stepped default is 10 items/s (0.1 s repeat), and a list viewport shows about 6–10 rows, which works out to roughly 1–1.7 viewports/s. **1.5 viewport heights/s at full deflection** matches that. Treat it as the starting point to tune, not a sourced number.
- Per frame: `dy = speedVpPerSec * viewportHeight * curve(mag) * Time.unscaledDeltaTime`. Use **unscaled** time, because pause menus often set `timeScale = 0`, and uGUI itself uses unscaled time.

## 5. Initial delay and key repeat

**Findings**
- Unity's defaults are a **0.5 s** delay and a **0.1 s** repeat. Only one repeat fires per frame ([InputSystemUIInputModule L963-L990](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L963-L990)). The class doc's example sets delay = rate × 1.2 ([L98-L102](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L98-L102)).
- Windows keyboard repeat: the delay setting runs from 0 (≈250 ms) to 3 (≈1 s), and the speed setting from ≈2.5 to ≈30 repeats/s ([SystemParametersInfo SPI_GETKEYBOARDDELAY / SPEED](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa)).
- Unreal Slate scales the repeat rate with stick pressure ([GetRepeatRateForPressure](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Slate/FNavigationConfig/GetRepeatRateForPressure)).
- A handheld-OS menu (muOS) separated "menu repeat delay" (default **208 ms**) from menu acceleration after users complained about the pause before scrolling ([muOS forum](https://community.muos.dev/t/pause-before-menu-acceleration-scroll/906)). **SECONDARY** (community forum).

**Implications** (INFERENCE): keep 0.5 s / 0.1 s for D-pad selection; it's the engine default players already know. Continuous right-stick scrolling needs **no** initial delay, because the deadzone already filters accidental touches.

## 6. Acceleration, ramping, inertia, end clamping

**Findings**
- `ScrollRect` inertia (`decelerationRate` default **0.135**, meaning speed is multiplied by 0.135 each second) only applies after a drag. Velocity is learned from drag motion ([L245-L274, L886-L907](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L886)). `Elastic` movement springs back with `SmoothDamp(elasticity = 0.1 s)`, tripled while `m_Scrolling` ([L871-L882](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L871-L882)). `SetNormalizedPosition` zeroes velocity on that axis ([L1086-L1105](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L1086-L1105)).
- Apple TV's focus engine applies "momentum-based movement after a fast remote swipe". That is touch-surface inertia, not stick behaviour ([Apple TV Programming Guide](https://developer.apple.com/library/tvos/documentation/General/Conceptual/AppleTV_PG/WorkingwiththeAppleTVRemote.html)).
- Accelerating repeat over hold time is common in list UIs. A patent describes speed rising "the longer the button is pressed down" and notes the risk of overshooting on short lists ([US 2006/0290666](https://patents.justia.com/patent/20060290666)). **SECONDARY** (patent, not a shipped spec). A Genesis Plus GX request praises held-scroll that **stops at the end instead of wrapping** ([issue #217](https://bitbucket.org/eke/genesis-plus-gx/issues/217/d-pad-left-right-to-act-as-pgup-pgdown)). **SECONDARY**.
- The Lyra-derived Common UI cursor uses linear acceleration (1500 px/s²) up to a max speed ([config](https://github.com/OpenTournament/OpenTournament/blob/master/Config/DefaultInput.ini#L114)). **SECONDARY**.

**Implications** (INFERENCE)
- With an analog stick the player already controls speed through deflection, so hold-time acceleration is optional. If you add it, apply it only at near-full deflection (>0.9), ramp from 1× to about 2× over about 1 s, and reset as soon as deflection drops.
- Smooth the commanded velocity with `v += (target - v) * (1 - exp(-dt/τ))`, τ ≈ 0.06–0.08 s. This removes the jerk of a stick reading that jumps between frames while still feeling immediate. Use the same τ on release so it stops within about 0.2 s. **Disable `ScrollRect.inertia` coasting from stick input**: at 0.135/s it coasts for seconds, which feels like poor control.
- At the ends, clamp the target position to `[0, hiddenLength]` and set velocity to 0. Don't use elastic overshoot for stick input; it reads as lag.

## 7. Focus/selection interaction and ScrollRect specifics

**Findings**
- The platforms keep the focused item in view automatically:
  - Apple TV: "If the next (updated) focused view is in a scroll view and is offscreen, the scroll view scrolls the content so that the next focused view moves onscreen" ([Apple TV Programming Guide](https://developer.apple.com/library/tvos/documentation/General/Conceptual/AppleTV_PG/WorkingwiththeAppleTVRemote.html)).
  - Godot: `ScrollContainer.follow_focus` calls `ensure_control_visible` on focus change, choosing "the less scroll the better" ([scroll_container.cpp L378-L398](https://github.com/godotengine/godot/blob/4d10419cacd6d3009976e8f9fd3abd970fd58018/scene/gui/scroll_container.cpp#L378-L398)).
  - **uGUI's `ScrollRect` has no equivalent**: ScrollRect.cs implements no select handler, and nothing in it reacts to `EventSystem` selection (checked by grep over the pinned source). Mods must scroll to the selected item themselves.
- Microsoft: "One focus visual should always be visible on the screen" and "there should be a focusable item onscreen at all times". It also describes **focus engagement**: press A to enter a long list, B to leave it, so the list doesn't trap D-pad navigation ([MS gamepad guidance](https://learn.microsoft.com/en-us/windows/apps/design/input/gamepad-and-remote-interactions)).
- Pixel vs normalized: `SetNormalizedPosition` maps `value` across `hiddenLength = content.size - view.size` ([L1086-L1094](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L1086-L1094)). A fixed normalized delta per second therefore scrolls **faster in pixels on longer content** (INFERENCE from the formula). Convert with `Δnorm = Δpx / hiddenLength` (vertical normalized position is 1 at the top). Alternatively, move `content.anchoredPosition` directly in pixels and clamp it.
- `scrollSensitivity` only scales `OnScroll` (wheel/trackpad) deltas ([L277-L285](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L277-L285)). It has no effect on your own position writes.

**Implications** (INFERENCE)
- On a D-pad selection change, scroll the selected row into view with the smallest move (Godot's rule), plus a margin of about half a row so the next row is visible.
- When the right stick moves the selected item out of view, don't drag the selection along every frame, since that fights the player. On the next D-pad or left-stick press, first move the selection to the nearest *visible* item, then step from there. This keeps the guarantee that focus stays visible without coupling the two inputs.
- On the settings page, the explicit-navigation rows mean the selection must not change during a stick scroll; a Slider's left/right input would conflict otherwise (see the project's CLAUDE.md).

## 8. Accessibility

**Findings**
- Xbox Accessibility Guideline 107:
  - "An option to adjust sensitivity of analog controls individually should be provided … Players should be able to increase or decrease sensitivity by **at least 50%** of the default sensitivity."
  - Remapping should include "inverting both the x-axis and y-axis individually for each individual control stick".
  - "UIs throughout the game should support digital and analog navigation."
  - "All interface components should be fully operable with digital input."
  - Avoid mechanics that require holding a button "for an extended period".

  ([XAG 107](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/107))
- Game Accessibility Guidelines (basic): "Include an option to adjust the sensitivity of controls", because "There is no one-size-fits-all sensitivity level" ([GAG](https://gameaccessibilityguidelines.com/include-an-option-to-adjust-the-sensitivity-of-controls/)). The full list has no guideline specifically about axis inversion ([full list](https://gameaccessibilityguidelines.com/full-list/)).
- Steam Input asks for configurable deadzone, acceleration and sensitivity on joysticks ([Steam Input](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs)).

**Implications** (INFERENCE): expose scroll speed with a range of at least 0.5×–2×. The mod's existing controller-sensitivity setting (0.2–4.0) already covers this if it also scales scroll speed; otherwise add a separate "Menu scroll speed". Add a scroll-invert toggle. Expose the deadzone in the config file. Keep D-pad and trigger page-jumps as the digital alternative.

---

## Recommended defaults

| Parameter | Suggested value | Basis |
|---|---|---|
| Deadzone shape | Scaled radial on 2D vector | [Sutphin](https://www.gamedeveloper.com/business/doing-thumbstick-dead-zones-right), [XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput), [Unity StickDeadzone](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Controls/Processors/StickDeadzoneProcessor.cs#L30-L52) |
| Inner deadzone | 0.20 (configurable 0.05–0.4) | Godot 0.2; Common UI ScrollDeadZone 0.2 (SECONDARY); XInput right ≈0.265; Unity 0.125; Sutphin 0.1–0.25 |
| Outer deadzone | 0.925 | [Unity default max](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/InputSettings.cs#L783-L784) |
| Axis gate | Use y only, and only while \|y\| > \|x\| | Dominant-axis rule in [Unity UI module](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L818-L824); angular deadzones in [Juckett](https://blog.hypersect.com/interpreting-analog-sticks/) |
| Response curve | magnitude², exponent configurable 1–3 | [XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput) (cube for driving), [Juckett](https://blog.hypersect.com/interpreting-analog-sticks/), [Rewired Power](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_AxisSensitivityType.htm); exponent 2 is INFERENCE |
| Max speed (full deflection) | 1.5 viewport heights/s | INFERENCE, calibrated to Unity's 10 steps/s and Godot's page-relative wheel step |
| Time base | `Time.unscaledDeltaTime` | [ScrollRect L860](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L860) |
| Units | pixels → `Δnorm = Δpx / (content − viewport)` | [SetNormalizedPosition](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L1086-L1094) |
| Velocity smoothing τ (in and out) | 0.07 s | INFERENCE |
| Inertia after release | Off (stop within ~0.2 s) | INFERENCE; uGUI inertia is drag-only ([L886](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/UI/Core/ScrollRect.cs#L886)) |
| Ends | Clamp, zero velocity, no elastic | INFERENCE; [wrap-avoidance (SECONDARY)](https://bitbucket.org/eke/genesis-plus-gx/issues/217/d-pad-left-right-to-act-as-pgup-pgdown) |
| Hold acceleration (optional) | ×1 → ×2 over 1 s, only when mag > 0.9 | INFERENCE; [Slate pressure-scaled repeat](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Slate/FNavigationConfig/GetRepeatRateForPressure) |
| D-pad repeat delay / rate | 0.5 s / 0.1 s | [Unity defaults](https://github.com/Unity-Technologies/InputSystem/blob/05e45ddbaf18a93245b068d787d74b738b90d01c/Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/UI/InputSystemUIInputModule.cs#L945-L952), [legacy module](https://github.com/Unity-Technologies/uGUI/blob/854155341e6ba568aa84eed807332bb2ae19e294/com.unity.ugui/Runtime/UGUI/EventSystem/InputModules/StandaloneInputModule.cs#L56-L59) |
| Page jump | Triggers = ±1 viewport | [MS accelerators table](https://learn.microsoft.com/en-us/windows/apps/design/input/gamepad-and-remote-interactions) |
| Keep selection visible | Minimal scroll + ½-row margin on selection change | [Godot follow_focus](https://github.com/godotengine/godot/blob/4d10419cacd6d3009976e8f9fd3abd970fd58018/scene/gui/scroll_container.cpp#L393-L398), [Apple TV](https://developer.apple.com/library/tvos/documentation/General/Conceptual/AppleTV_PG/WorkingwiththeAppleTVRemote.html) |
| User speed range | ≥ 0.5×–2× (mod's 0.2–4.0 is fine) | [XAG 107](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/107) |
| Invert | Per-stick toggle | [XAG 107](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/107) |

## Gaps

- No GDC talk or postmortem from Valve, Naughty Dog or Insomniac on menu stick-scroll tuning turned up. Apple's current Game Controller docs give no analog-scroll numbers. Unreal's exact Slate repeat and Common UI scroll logic are in EULA-gated source.
- Juckett's older "Analog input processing" article (ryanjuckett.com) returned 404. The 2016 Hypersect post was used instead.
