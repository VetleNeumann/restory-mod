# ReStory mod: controller support, glyphs, rebinding

Research date: 2026-10-18. Target: *ReStory: Chill Electronics Repairs* (Steam app **3812600**), buildid **24885009**, Unity 6000.3.10f1, **Rewired 1.1.58.4.U6000**, Steamworks.NET 20.2.0.
Tags: **UNVERIFIED** = could not confirm from a primary source. **SECONDARY** = only a non-primary source was found. **INFERENCE** = my own reasoning, not a sourced claim.

Paths: `D/` = `~/restory-decompiled/24885009/`. `M/` = `$GameDir/Restory_Data/Managed/`. Facts about serialized assets (Rewired config, glyph tables, remap data) come from reading `resources.assets` and the Addressables bundles with UnityPy plus a type tree generated from `M/`. The extraction scripts were throwaway and are not in the repo.

---

## TL;DR recommendation

**The game already has most of the machinery.** It has a glyph system with Xbox, PlayStation, Steam Deck and generic sprite sets. It has a working remap backend with its own save file, and a manual "gamepad scheme" override that nothing ever sets. It does not have a remap UI, and it never calls Steam Input. So the mod should plug into what exists instead of building a parallel stack.

1. **Controller detection: a `PadKind` resolver, with the pure logic in Core.**
   - **Steam Input first.** After the game's Steam init, call `SteamInput.Init(false)` once. Each time the active controller changes, check `SteamInput.GetControllerForGamepadIndex(0..3)`. A non-zero handle means Steam Input is emulating that pad, and `GetInputTypeForHandle` gives the real hardware (PS4, PS5, Switch Pro, Deck, …).
   - **Then Rewired `Joystick.hardwareTypeGuid`.** This only identifies the pad when Steam Input is off for it.
   - **Then fall back to generic.** Unit-test the classifier in Core, taking `(steamType?, rewiredGuid?, name)` and returning `PadKind`.
2. **Glyphs: feed the game's own glyph system.** The prompt widget `GUI_InputActionElement` already picks sprites from a `ControllerId`. Postfix its private `TryGetControllerId`, or `RewiredControllerIdsDependencyMap.TryGetControllerId(Guid)`, so it returns the id `PadKind` resolved.
   - **Correction (found in play, 2026-10-19):** the game has no PlayStation or Steam Deck art in use. In both template lists every pad's entry is a `GuiReuseGamepadTemplate` pointing at the universal A/B/X/Y template. Its image bundle does ship a full, unused PS5 sprite set (`PS5Cross`, `PS5_Circle`, `PS5_Triangle`, the Square misnamed `PS5_Triangle-1`, D-pad, L1–R2, sticks, Options, Share, plus `_LongAction` variants), which the mod swaps in for PlayStation prompts.
   - Phase 2 adds a Nintendo set (positional A/B/X/Y) and optionally a Steam-API PNG fallback, built from **Kenney Input Prompts (CC0)**.
   - Add a settings row, "Button prompts: Auto / Xbox / PlayStation / Steam Deck", that writes the dormant `GameSettingsManager.GamepadScheme`. The game already honours and saves that field.
3. **Rebinding: reuse the game's backend.** Drive the game's `InputRemapping` and `IInputUserData` (Rewired `InputMapper` plus a dependency map that fans one logical action out to every map category). Persist with `IInputUserData.Save()`, which writes the game's `inputSettings` file and reloads it at boot.
   - Build our own settings sub-panel: rows of (label, glyph) for the 25 logical actions the game's map already covers, for both Keyboard and Joystick.
   - Handle conflicts ourselves (swap or replace). The game's handler blindly **adds** duplicates.
4. **Mod-added actions: BepInEx config, not Rewired.** Store each binding as a Gamepad Template element id plus a keyboard `KeyCode`. Poll it through `IGamepadTemplate`, as `ScrewHintButton` already does. Show it in the same rebinding panel with the same capture flow. Injecting Rewired actions is possible (see Q4), but it is fragile and buys little.
5. **Steam Input: don't fight it.** A mod can't change the partner config. Detect Steam Input per pad and show the right glyphs either way. For unrecognised DirectInput pads, tell users to enable Steam Input for the game; that's the cheap path to "generic controller works".

---

## 1. Steam Input vs Rewired

**How Rewired is configured here.** Read from the `Rewired Input Manager` MonoBehaviour, path id 6016 in `resources.assets`:

| ConfigVar | Value | Meaning |
|---|---|---|
| `windowsStandalonePrimaryInputSource` | 0 | **Raw Input** (enum `RawInput=0, DirectInput=1, XInput=2, WindowsGamingInput=3, SDL2=10, Unity=100`, decompiled `Rewired.Platforms.WindowsStandalonePrimaryInputSource`) |
| `useXInput` | 1 | XInput devices go through XInput |
| `platformVars_windowsStandalone.useWindowsGamingInput` | 0 | WGI off |
| `useEnhancedDeviceSupport` | 1 | Gives native DS4, DualSense and Switch Pro support on Raw Input |
| `useSteamControllerSupport` | 1 | Not documented on the Rewired Editor page (UNVERIFIED semantics) |
| `maxJoysticksPerPlayer` / `autoAssignJoysticks` | 1 / 1 | One pad, auto-assigned to `Player0` |

**What Rewired sees when Steam Input is on.** Per Rewired's [Troubleshooting → Steam](https://guavaman.com/projects/rewired/docs/Troubleshooting.html#steam):
- "Steam takes over what controllers the application can see". "Rewired cannot see the true devices connected to the system, only what Steam exposes to it."
- "Steam-configured controllers become virtual Steam Controllers". The virtual pad "automatically emulates an XInput gamepad (XBox 360) on Windows".
- "When Use XInput is enabled but Use Windows Gaming Input is disabled", which is this game's setup, the pad uses the **Xbox 360** profile.
- So under Steam Input a DualSense is an Xbox 360 pad to Rewired, and `hardwareTypeGuid`, `DualSenseExtension` and the rest are gone.
- Valve says the same thing: Steam-routed input "will appear in your game as regular Xbox controller input" ([getting started](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs)).

**This is happening on this box right now.** `Player.log` contains `Joystick connected ("Controller (XBOX 360 For Windows)")`. Steam's `localconfig.vdf` lists `ControllerTypesUsed "controller_ps5,controller_ps4,"` and has `"UseSteamControllerConfig" "2"` under app 3812600. The value mapping (2 = forced on?) is UNVERIFIED. INFERENCE: Vetle's PlayStation pad reaches the game as Xbox 360, and the game shows Xbox prompts for it.

**What the developer set (fixed, out of mod reach).** From `Steam/appcache/appinfo.vdf` for 3812600:
- `controller_support = partial`, `steamcontrollertemplateindex = 2`. The meaning of index 2 is UNVERIFIED.
- Store categories include DualShock and DualSense support ([appdetails](https://store.steampowered.com/api/appdetails?appids=3812600)).
- Deck Verified tests pass `ControllerGlyphsMatchDeckDevice` and `DefaultControllerConfigFullyFunctional`.
- The PlayStation, Switch and Generic opt-in checkboxes are not visible in appinfo (UNVERIFIED which ones are ticked).
- A mod can't change any of this. Users can override Steam Input per game in Steam. Valve's guidance for XInput games is to opt every type in except Xbox ([gamepad emulation best practices](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)).

**What a mod can control.**
- It can call ISteamInput through the game's own `com.rlabrecque.steamworks.net.dll`. The game initialises SteamAPI through `com.alekrus.universal-platform.steam.dll`, which also pumps `SteamAPI.RunCallbacks`, but it never touches `SteamInput`. The decompiled wrapper only uses SteamUser, SteamUserStats, SteamFriends, SteamApps and SteamUtils.
- In principle it can flip Rewired ConfigVars, such as WGI or `useSteamControllerSupport`, by prefixing `InputManager_Base.Awake`. That runs before Rewired initialises, and the prefab is instantiated by `D/Restory.Infrastructure.ProjectInstallers/RewiredInstaller.cs:24`.
- Don't flip ConfigVars. Changing the input source changes device identity and could orphan saved bindings (INFERENCE).

**Can we tell whether Steam Input is active? Yes, per pad.**
- `SteamInput.GetControllerForGamepadIndex(n)` returns "0 if the Xinput index isn't associated with Steam Input" ([Steamworks.NET isteaminput.cs](https://github.com/rlabrecque/Steamworks.NET/blob/master/com.rlabrecque.steamworks.net/Runtime/autogen/isteaminput.cs)).
- Valve's best-practices page says to call `Init` once, and that `RunFrame` is automatic when the game runs callbacks.
- Mapping a Rewired joystick to its XInput slot isn't exposed by the documented Rewired API (UNVERIFIED). This is single-player, so take the first non-zero slot.
- The same page notes the Raw Input device name encodes the Steam Input handle. Whether Rewired exposes that name through `Joystick.hardwareIdentifier` is UNVERIFIED.

## 2. Controller type detection

| Source | Works when | Notes |
|---|---|---|
| Rewired `Joystick.hardwareTypeGuid` | Steam Input **off** for that pad | The documented way to "identify this Joystick as a recognized type" ([How To's](https://guavaman.com/projects/rewired/docs/HowTos.html#identifying-recognized-controllers)). Under Steam Input it's the Xbox 360 GUID. |
| Rewired `Controller.GetTemplate<IGamepadTemplate>()` | Any recognised gamepad | Gives a positional element id (0–25) for glyph and binding lookup. Unknown controllers have no template ([How To's](https://guavaman.com/projects/rewired/docs/HowTos.html)). |
| Rewired extensions | Native Raw Input + Enhanced Device Support | `DualShock4Extension`, `DualSenseExtension`, `NintendoSwitchProControllerExtension`, `XboxOneGamepadExtension` and `SteamControllerExtension` are all present in `M/Rewired_Core.dll` / `M/Rewired_Windows.dll`. Rewired supports DS4, DualSense and Switch Pro with "Raw Input + Enhanced Device Support" ([Supported Controllers](https://guavaman.com/projects/rewired/docs/SupportedControllers.html)). |
| Rewired `Controller.type` | Always | Only Keyboard, Mouse, Joystick or Custom. The game uses it to split KB/M from pad (`D/Restory.Infrastructure.CommonServices/ControlsManager.cs:43-57`). |
| Steam `GetInputTypeForHandle` | Steam Input **on** | `ESteamInputType` includes PS4, PS5, SwitchPro, SteamDeck, XBox360, XBoxOne and GenericGamepad ([SteamEnums.cs](https://github.com/rlabrecque/Steamworks.NET/blob/master/com.rlabrecque.steamworks.net/Runtime/autogen/SteamEnums.cs)). The SDK revision bundled with 20.2.0 decides which values exist; check at compile time. |

**Hardware GUIDs the game already maps.** These are in `RewiredControllerIdsDependencyMap` (Odin blob, decoded) and were cross-checked against Rewired's hardware maps in `resources.assets`:

| Rewired hardware map | GUID | Game ControllerId |
|---|---|---|
| Sony DualSense | `5286706d-19b4-4a45-b635-207ce78d8394` | SonyDualSenceGamepad |
| Sony DualShock 4 | `cd9718bf-a87a-44bc-8716-60a0def28a9f` | SonyDualSenceGamepad |
| Sony DualShock 3 / 2 | `71dfe6c8-…` / `c3ad3cad-…` | SonyDualSenceGamepad |
| Xbox 360 | `d74a350e-fe8b-4e9e-bbcd-efff16d34115` | XboxOneGamepad |
| Xbox One | `19002688-7406-4f4a-8340-8d25335406c8` | XboxOneGamepad |
| Steam Controller ("Steam-configured controller") | `2694f4b9-9d84-4f55-9ee8-78fbba744b7d` | SteamDeckGamepad |
| Keyboard / Mouse | `ae4830f9-…` / `ad60107c-…` | Keyboard / Mouse |
| *Switch Pro (`7bf3154b-9db8-4d52-950f-cd0eed8a5819`), everything else* | — | **default → UnknownGamepad** |

**Recommendation.** Resolve in this order: Steam handle type → `hardwareTypeGuid` → name heuristics (`"Wireless Controller"`, INFERENCE) → generic. Re-resolve on the Rewired controller-connected and last-active-changed events. The game already observes those (`RewiredControllerConnectedObserver`, `ControlsManager`).

## 3. Glyphs

**What the game has (reuse it).** `D/IAmFuture.Data.Controllers/` holds the glyph classes:
- `GuiXInputGamepadTemplate`, `GuiSonyDualGamepadTemplate` and `GuiUniversalGamepadTemplate` each hold a sprite (and a "press" sprite) per Rewired Gamepad Template element id. `GetElement(int)` maps 4 to A/Cross, 5 to B/Circle, 7 to X/Square, 10–13 to the shoulders, 19–22 to the D-pad, and so on.
- `GuiReuseGamepadTemplate` aliases one set to another ControllerId.
- The bundle `CAB-fa72e6ef…` contains `ControllerTemplateList` and `ControllerTemplateList - Blue`, with templates for **XboxOne, SonyDualSense, SteamDeck and Unknown**. There is no Nintendo set. The XboxOne, SonyDualSense and SteamDeck entries all reuse the Unknown (universal A/B) template, so every pad draws the same art (see the correction in the TL;DR).

**How the game chooses** (`D/Restory.UI.Presenters.Controllers/GUI_InputActionElement.cs`):
1. It finds the first `ActionElementMap` for the action on the current control type (`TryGetElementMap`, :171-206).
2. It picks the ControllerId (`TryGetControllerId`, :208-233) in this order: **`GameSettingsManager.GamepadScheme`** if set, then `RewiredControllerIdsDependencyMap[hardwareTypeGuid]`, then `DefaultGamepadId`.
3. It converts the element to a **Gamepad Template element id** with `IGamepadTemplate.GetElementTargets` (:235-281).
4. The view draws the sprite for that template and element.

`GamepadScheme` is a persisted settings field (`D/Restory.Gameplay.GameSettings/GameSettingsManager.cs:651-660`) with **no writer anywhere in the code** (grep for `.GamepadScheme =`). The UnityEvent setter path is UNVERIFIED. It's a ready-made manual override.

**Rewired's own glyph system.** Rewired ≥ 1.1.56 has `ReInput.glyphs`, `GlyphProvider` and `SpriteGlyphSet` ([Glyphs](https://guavaman.com/projects/rewired/docs/Glyphs.html)). `GlyphHelper` exists in this runtime. The **art is a separate editor-installed pack** that this game doesn't ship (no GlyphProvider or SpriteGlyphSet types in `M/RewiredAssembly.dll`), and the docs state no licence for it. Don't use it.

**Steam glyph API.** Steamworks.NET 20.2.0 exposes these (strings present in `M/com.rlabrecque.steamworks.net.dll`):
- `GetGlyphPNGForActionOrigin(origin, size, flags)`, which returns a "local path to a PNG file".
- `GetGlyphForXboxOrigin`, `GetActionOriginFromXboxOrigin(handle, xboxOrigin)` and `TranslateActionOrigin`.

The **action API is not required**. Valve's emulation best-practices page chains `GetControllerForGamepadIndex` → `GetActionOriginFromXboxOrigin` → glyph. Two caveats:
- It only works while Steam Input is on for that pad.
- Steamworks.NET's doc comment says it "doesn't take into account any remapping a user has made in their configuration". Steam-side remaps make prompts wrong, which Rewired's troubleshooting page also warns about.

Use it only as a fallback for types we have no art for. Load the PNG with `Texture2D.LoadImage`.

**Free packs for a public mod.**
- [Kenney Input Prompts](https://kenney.nl/assets/input-prompts): **CC0**. Covers Xbox 360/One/Series, PS1–PS5, Switch / Switch 2, Steam Deck, Steam Controller, keyboard and mouse, and generic. Version 1.5.
- [Xelu's Free Controller Prompts](https://thoseawesomeguys.com/prompts/): "public domain license under Creative Commons 0 (CC0)". The page doesn't list platforms (UNVERIFIED coverage; check the zip's Readme).
- CC0 covers copyright only. PlayStation shapes and console logos are trademarks, but both packs are widely used in shipped games (INFERENCE). Credit the packs anyway.

**Mapping chain** (one function, used for prompts and the rebind panel):
`actionId` → `player.controllers.maps.ElementMapsWithAction(Joystick, actionId)` → `IGamepadTemplate.GetElementTargets(aem)` → template element id → `PadKind` → sprite.

For mod-added bindings stored as template ids, skip the first two steps. Nintendo note: template ids are **positional**, so id 4 (bottom face) must draw "B" on a Switch Pro. Kenney's Switch set is labelled by button, so map by position.

## 4. Rebinding

**The game ships a backend but no UI.**
- `D/Restory.Remapping/InputRemapping.cs` wraps a Rewired `InputMapper` (:22). `Start` builds a `Context` that replaces the first element map of the logical action (:70-101). On success it calls `IInputUserData.SetInputButtonData` (:117-126).
- **Conflicts:** `ConflictFoundEvent` always answers `ConflictResponse.Add` (:103-114), so duplicates pile up.
- `new InputRemapping` appears nowhere in the code. The only `RemappingButtonsList` asset, `KeyboardRemappingButtonsInfo`, is effectively empty (decoded Odin blob).
- `D/Restory.Remapping/RewiredInputUserData.cs` lives on the Rewired prefab with `loadDataOnStart = 1`:
  - On `ReInput` init it snapshots defaults and calls `Load()` (:370-376).
  - `Save()` / `Load()` / `LoadDefault()` (:383-402) go through `GameSettingsDataSaveLoadSystem` to a per-profile `inputSettings` file (`D/Restory.Gameplay.GameSettings/GameSettingsDataSaveLoadSystem.cs:16,96-111`). That file doesn't exist on this box, which is consistent with no UI.
  - This is **not** Rewired `UserDataStore`. `UserDataStore_PlayerPrefs`, `_File` and `_KeyValue` classes exist in `M/RewiredAssembly.dll`, but whether one sits on the prefab is UNVERIFIED (check `ReInput.userDataStore` in UnityExplorer).
- `ActionsDependencyMap` (Odin) maps **25 logical `InputAction`s** to 44 (category, Rewired action) targets, **keyed by ControllerType Keyboard (0) and Joystick (2)**. The actions are MOVE_*, CAMERA_*, ROTATE, UI_SUBMIT, UI_BACK, UI_LB/RB/LT/RT, the stick clicks, UI_APPLY/RESET, INVENTORY_MENU, GAMEPLAY_MENU and COPYBOOK_*.
- Rewired itself has **18 joystick maps, all authored against the Gamepad Template GUID** `83b427e4-086f-47f3-bb06-be266abd1ca5`. There is no Unknown-Controller map, so unrecognised DirectInput pads probably get no bindings at all. INFERENCE from config; verify with a generic pad. Rewired: unknown controllers are "usable only through user mapping" ([Supported Controllers](https://guavaman.com/projects/rewired/docs/SupportedControllers.html)).

**Recommended flow (gamepad-only safe).**
- Own `InputMapper` with: `options.timeout ≈ 5 s`, `ignoreMouseXAxis/YAxis = true`, `allowAxes` only for axis rows, `checkForConflicts = true`. These Options fields were verified in decompiled `Rewired.InputMapper` ([Input Mapper](https://guavaman.com/projects/rewired/docs/InputMapper.html#options)).
- **Arm only after the confirm button is released**, then poll. This stops the A press that opened the capture from binding itself (INFERENCE; Rewired's docs don't cover it).
- Use `isElementAllowedCallback` to reject the Guide/PS/Home button.
- **Cancel** by timeout, or by holding Start or Back for about 1 s. A tap of Start or Back binds normally.
- **Conflicts:** subscribe to `ConflictFoundEvent`. If `isProtected`, answer Cancel. Otherwise show "Swap / Replace / Cancel" ([Input Mapper → conflicts](https://guavaman.com/projects/rewired/docs/InputMapper.html#conflicts)).
- **Persistence:** after `InputMappedEvent`, call `IInputUserData.SetInputButtonData(...)`, which fans out to every category map through the dependency map, then `Save()`. "Reset to defaults" is `LoadDefault()` + `Save()`.
- **Protect navigation.** Never let UI_SUBMIT, UI_BACK or the D-pad/stick UI axes end up unbound. Refuse the bind or auto-swap. A gamepad-only user can't recover otherwise.
- **UI:** a new sub-page in Settings, with rows spliced into explicit navigation as `CLAUDE.md` describes. Each row shows a label and the current glyph from §3.

**Mod-added actions: Rewired or config?**
- **Rewired injection works.** `ReInput.mapping` has no public `AddAction` (the decompiled `MappingHelper` only has getters), but `Rewired.Data.UserData.AddAction(int categoryId)` exists and `InputManager_Base.userData` is public. Prior art is Outward's SideLoader: it prefixes `InputManager_Base.Initialize`, then calls `userData.AddAction` and `ControllerMap.CreateElementMap` ([CustomKeybindings.cs](https://github.com/CrackerCat/Outward-SideLoader/blob/master/src/Custom/CustomKeybindings.cs), a mirror of sinai-dev's repo).
  - Here `Initialize` is obfuscated. Prefix `InputManager_Base.Awake` instead (private; seen in decompiled `Rewired.InputManager_Base`).
  - Costs: new action ids could collide with a future game update. The game's saved remaps and dependency map don't know about them. Defaults would have to be added to 18 joystick maps by hand.
- **Recommended: BepInEx config.** Store `GamepadTemplateElementId` plus `KeyCode` per mod action, poll through `IGamepadTemplate`, and reuse the same capture UI with a template poll instead of `InputMapper`. It is deterministic, survives game updates, keeps the logic testable in Core, and doesn't pollute the game's save. Downside: Steam Input remaps apply below us, which is fine.

## 5. What the game itself does (file index)

- **Steam:** `M/com.rlabrecque.steamworks.net.dll` (Steamworks.NET 20.2.0) and `M/com.alekrus.universal-platform.steam.dll`, used via `D/Restory.UniversalPlatform/PlatformManager.cs` and `D/Restory.Infrastructure.ProjectInstallers/UniversalPlatformInstaller.cs`. **No `SteamInput` usage**, and no Facepunch.
- **Rewired:** `M/Rewired_Core.dll` (`ReInput.programVersion` = `1.1.58.4.U6000`), `M/Rewired_Windows.dll`, `M/RewiredAssembly.dll`. Native: `Plugins/x86_64/Rewired_DirectInput.dll` and `Rewired_WindowsGamingInput.dll`. Players: `System`, `Player0` (id 1), `Operator` (id 2). 105 actions, 20 map categories.
- **Input services:** `D/Restory.Infrastructure.ProjectInstallers/RewiredInstaller.cs` (binds `ActiveControllerTypeManager`, `IInputUserData`, connect/disconnect observers), `D/Restory.Infrastructure.CommonServices/ControlsManager.cs` (KB/M vs Joystick switch), `D/Restory.Gameplay.GameSettings/ActiveControllerTypeManager.cs`, `D/Restory.Gameplay.PlayerInput/RewiredPlayerInput.cs`.
- **Glyphs:** `D/IAmFuture.Data.Controllers/*` (templates, `ControllerIdsList`, `RewiredControllerIdsDependencyMap`, `GuiControllerSchemeList`), `D/Restory.Infrastructure.CommonInstallers/ControllersInstaller.cs`, `D/Restory.UI.Presenters.Controllers/GUI_InputActionElement.cs`, `D/Restory.UI.Views.Controllers/GUI_InputActionElementView.cs`.
- **Remapping:** `D/Restory.Remapping/*`, `D/Restory.Data.Remapping/*`, `D/Restory.Data.SaveLoad.Containers/InputSaveData.cs`.

## 6. Prior art

- **Rewired's own guidance** is to show glyphs per hardware GUID with a template fallback, and to remap with `InputMapper` ([How To's](https://guavaman.com/projects/rewired/docs/HowTos.html), [Glyphs](https://guavaman.com/projects/rewired/docs/Glyphs.html)). Rewired's `ControlMapper` prefab isn't in this build (no type in `M/RewiredAssembly.dll`).
- **Valve:** for XInput games, use `GetControllerForGamepadIndex` + `GetInputTypeForHandle` for type, and Steam's PNG glyphs as the fallback for unknown types ([best practices](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)).
- **Outward modding (Rewired game):** SideLoader's runtime `UserData.AddAction` injection plus patched control-mapping panels (link above).
- **BepInEx `KeyboardShortcut`** is keyboard-only (`UnityEngine.Input`/`KeyCode`) ([source](https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx/Configuration/KeyboardShortcut.cs)). That's why gamepad bindings need our own template-id config type.
- No ReStory mod touches controllers (see `existing-mods.md`).

---

## Open questions / risks

- **`UseSteamControllerConfig "2"` and `steamcontrollertemplateindex = 2`:** the meanings are UNVERIFIED. Test by toggling Steam Input in the game's Properties → Controller, then watch the Rewired joystick name in `Player.log`.
- **XInput slot ↔ Rewired joystick:** UNVERIFIED. It only matters with more than one pad, and `maxJoysticksPerPlayer = 1`.
- **Does `SteamInput.Init(false)` change behaviour** for a game that never called it? Valve's emulation guide tells XInput games to call it, so it's expected to be safe (INFERENCE). Gate it behind config and test with Steam Input on and off.
- **Remap keying:** the game keys saved remaps by `(playerId, ControllerType, controllerId)`. Whether `controllerId` is the Rewired joystick id (unstable across sessions or pads) is UNVERIFIED. Read `RewiredInputUserData.RewiredGetUserData` before relying on it.
- **Unrecognised DirectInput pads** likely have no bindings. Either document "enable Steam Input", or (bigger job) create a joystick map for unknown controllers at runtime.
- **Glyph-set injection** for Nintendo means creating `GuiBaseGamepadTemplate` / `ControllerId` ScriptableObjects at runtime and inserting them into the cached dictionaries (`controllersCache` in `GuiControllerTemplateList` / `ControllerIdsList`). It's doable with reflection, but list every member in `PatchTargets`.
- **Steam Deck / Proton:** the game already passes Deck glyph verification. How Rewired sees the Deck under Proton (the Steam Controller GUID?) is UNVERIFIED, so don't regress it. Prefer the game's own mapping when `PadKind` is unknown.
- **Trademarks** on platform glyphs: low risk for a free mod (INFERENCE).
