# ReStory mod: loader, repo structure, CI, update resilience

Research date: 2026-10-15. Target: *ReStory: Chill Electronics Repairs* (Steam app **3812600**). Unity 6000.3.10f1, Mono backend, Rewired input.
Tags: **UNVERIFIED** = could not confirm from a primary source. **SECONDARY** = only a non-primary source was found. **INFERENCE** = my own reasoning, not a sourced claim.

---

## TL;DR recommendation

- **Loader:** BepInEx **5.4.23.5** (LTS, Doorstop 4.5.0, HarmonyX 2.9.0). Don't use BepInEx 6 (pre-release) or MelonLoader. BepInEx 5's own release notes recommend 5 for new Mono games, and 5.4.23.5 includes a Unity 6 fix.
- **Project:** SDK-style C# project, `netstandard2.1`, created with `dotnet new bepinex5plugin` and using `BepInEx.PluginInfoProps`. Reference `UnityEngine.Modules` **6000.3.10**, which exists on nuget.bepinex.dev. Reference game DLLs (`Restory.Assembly.dll`, `Rewired_Core.dll`, …) through a `$(GameDir)` HintPath. Each machine sets `GameDir` in its own gitignored `GamePaths.props`.
- **Don't publish game DLLs, stripped or not.** No ReStory GameLibs package exists. For a personal mod, CI builds only the Unity-free core and its tests. Full builds happen locally, or later on a self-hosted runner on zantox-desktop.
- **Publicizer:** avoid it at first and use `AccessTools`/`___field` injection. If you do need one, use **Krafs.Publicizer 2.3.2**, which is actively maintained. `BepInEx.AssemblyPublicizer.MSBuild` is still at 0.4.3, with only 0.5.0 betas since.
- **Resilience:** put each feature in its own patch class. Each class has a `Prepare()` that does the `AccessTools` lookups and returns false if they fail. Apply each class separately with `harmony.PatchAll(typeof(X))` inside its own try/catch, then log one applied/skipped summary line. **`[HarmonyOptional]` isn't available**: it was added in HarmonyX 2.12, and BepInEx 5 ships 2.9.0. Use prefix/postfix rather than transpilers. Add a `TargetCheck` test that loads the real `Restory.Assembly.dll` with `MetadataLoadContext` and checks that every patch target still exists. Run it after each game update.
- **Update detection:** compare the local `appmanifest_3812600.acf` buildid, or the MVID of `Restory.Assembly.dll`, against the last-known value in `dev.sh`. Optionally, add a cron workflow that polls `steamcmd +app_info_print 3812600`.

```
restory-mod/
├─ RestoryMod.sln
├─ Directory.Build.props        # LangVersion, Nullable, imports GamePaths.props if it exists
├─ GamePaths.props.example      # committed; copy to GamePaths.props (gitignored)
├─ NuGet.Config                 # nuget.org + https://nuget.bepinex.dev/v3/index.json
├─ src/RestoryMod/              # BepInEx plugin, netstandard2.1, refs game DLLs via $(GameDir)
│  ├─ Plugin.cs  Features/<Feature>/{Feature.cs,Patches.cs}  PatchTargets.cs
├─ src/RestoryMod.Core/         # pure logic (curves, deadzones, config model); NO Unity/Rewired refs
├─ tests/RestoryMod.Core.Tests/ # net8+/xUnit; runs in CI
├─ tests/RestoryMod.TargetCheck/# net8+; MetadataLoadContext vs $(GameDir) DLLs; skips if GameDir absent
├─ scripts/dev.sh               # build → deploy to BepInEx/plugins → launch via steam://
└─ .github/workflows/{ci.yml,game-update.yml}
```
Dev loop: `scripts/dev.sh`, which runs `dotnet build -c Debug`, then `cp` into `$GAME/BepInEx/plugins/RestoryMod/`, then `cd /mnt/c && cmd.exe /c start "" steam://rungameid/3812600`.

---

## 1. Mod loader choice (Unity 6000.3 Mono)

**Findings**
- The latest BepInEx 5 release is **v5.4.23.5**, published 2026-02-08. It bundles Doorstop 4.5.0 and includes "v5: Fix log writer errors for Unity 6" ([release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5), [PR #1264](https://github.com/BepInEx/BepInEx/pull/1264)). BepInEx 5 is in LTS: development moved to the `v5-lts` branch, and `master` is now BepInEx 6 (same release notes).
- The v5-lts README pins UnityDoorstop 4.5.0, HarmonyX **2.9.0**, MonoMod 21.12.13.01 and Cecil 0.10.4 ([README@v5-lts](https://github.com/BepInEx/BepInEx/blob/v5-lts/README.md)). The 2.9.0 pin also appears in [BepInEx.Preloader.csproj@v5-lts](https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx.Preloader/BepInEx.Preloader.csproj).
- **I found no official "supported Unity versions" statement for BepInEx 5.** The evidence that it works on Unity 6:
  - It ships Unity 6 fixes (above).
  - BepInEx 6.0.0-pre.2's release notes say: "If you want to mod new Unity Mono games, BepInEx 5 is recommended" ([releases](https://github.com/BepInEx/BepInEx/releases)).
  - A user report says BIE 5 runs on The Planet Crafter on **Unity 6000.3.0f1**, and that "other mods … work fine" ([yukieiji/UnityExplorer#81](https://github.com/yukieiji/UnityExplorer/issues/81)). This is a user report, not an official statement.
- **Known Unity 6 issues in BepInEx 5:**
  - [#1385](https://github.com/BepInEx/BepInEx/issues/1385) (open, 2026-08): a stack overflow in Unity log redirection on Unity 6. Disabling the log listener only partly helped. The setting is `[Logging] UnityLogListening` ([Chainloader.cs@v5-lts](https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx/Bootstrap/Chainloader.cs)).
  - [#1150](https://github.com/BepInEx/BepInEx/issues/1150): a `MissingMethodException: GetPEKind` on startup. The maintainer said this is caused by a stripped game corlib. The fix is Doorstop's `dll_search_path_override` setting ([doorstop_config.ini@v5-lts](https://github.com/BepInEx/BepInEx/blob/v5-lts/doorstop/doorstop_config.ini)).
- **BepInEx 6 (Unity Mono)** is only available as bleeding-edge builds ([install docs](https://github.com/BepInEx/bepinex-docs/blob/master/articles/user_guide/installation/unity_mono.md)). The latest `BepInEx.Core` on the feed is `6.0.0-be.788` ([feed index](https://nuget.bepinex.dev/v3/package/bepinex.core/index.json)). Issue #1385 also reproduces on 6. The pre-release notes describe pre.2 as aimed at IL2CPP.
- **MelonLoader** v0.7.3 was released 2026-05-14 ([releases](https://github.com/LavaGang/MelonLoader/releases)). It has an open Unity 6 Mono bug, [#1199](https://github.com/LavaGang/MelonLoader/issues/1199), where mod scripts in AssetBundles fail to resolve. It also has a different plugin ecosystem, and I see no advantage to it here.
- **Long-term risk (SECONDARY/UNVERIFIED):** a Unity CoreCLR scripting backend is in Unity 6.7 alpha according to [BepInEx#1367](https://github.com/BepInEx/BepInEx/issues/1367), which is a user report. The UnityExplorer maintainer says Mono is deprecated in Unity 6.8 ([yukieiji/UnityExplorer#104](https://github.com/yukieiji/UnityExplorer/issues/104)). I didn't verify this against Unity's own roadmap. If ReStory ever moves to CoreCLR or IL2CPP, BepInEx 5 stops working.

**Recommendation:** BepInEx 5.4.23.5 x64 from the [GitHub releases](https://github.com/BepInEx/BepInEx/releases). On the first launch, check `BepInEx/LogOutput.log` for #1385 and #1150 symptoms.

## 2. Repo structure

- **Template:** install it with `dotnet new install BepInEx.Templates::2.0.0-be.4 --nuget-source https://nuget.bepinex.dev/v3/index.json`. Then run `dotnet new bepinex5plugin -n RestoryMod -T netstandard2.1 -U 6000.3.10` ([docs: setup](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/1_setup.html), [docs: plugin start](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html), [Templates README](https://github.com/BepInEx/BepInEx.Templates)). The templates need .NET SDK 6 or newer.
- **Generated csproj** ([template source](https://github.com/BepInEx/BepInEx.Templates/blob/master/BepInEx.Templates/templates/BepInEx5.PluginTemplate/BepInEx5.PluginTemplate.csproj)):
  - It's SDK-style, sets `AllowUnsafeBlocks=true`, and uses `RestoreAdditionalProjectSources` to add nuget.org, nuget.bepinex.dev and nuget.samboy.dev.
  - It references `BepInEx.Analyzers 1.*` (PrivateAssets), `BepInEx.Core 5.*`, `BepInEx.PluginInfoProps 2.*`, and `UnityEngine.Modules <-U>` with `IncludeAssets="compile"`.
  - `-U` replaces the `5.6.0` Unity version default ([template.json](https://github.com/BepInEx/BepInEx.Templates/blob/master/BepInEx.Templates/templates/BepInEx5.PluginTemplate/.template.config/template.json)).
- **`BepInEx.PluginInfoProps`** generates `obj/.../MyPluginInfo.cs` with `PLUGIN_GUID`, `PLUGIN_NAME` and `PLUGIN_VERSION`. The values come from `<BepInExPluginGuid>`/`<GUID>`/`AssemblyName`, `<Product>` and `<Version>` ([props source](https://github.com/BepInEx/BepInEx.Templates/blob/master/BepInEx.PluginInfoProps/BepInEx.PluginInfoProps.props), [README](https://github.com/BepInEx/BepInEx.Templates/blob/master/BepInEx.PluginInfoProps/README.md)). This makes the csproj `<Version>` the single source of truth for the version.
- **Target framework:** use `netstandard2.1`.
  - Unity 6000.3 supports two API compatibility levels, **.NET Standard 2.1 (the default)** and .NET Framework 4.8. .NET Standard plugins work under both ([Unity manual](https://docs.unity3d.com/6000.3/Documentation/Manual/dotnet-profile-support.html)).
  - BepInEx's own guidance is to use netstandard2.0 if `Managed/netstandard.dll` exists, and net46 if mscorlib is 4.x or newer ([docs](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html)).
  - The community Lethal Company template (Unity 2022, BIE 5) uses `netstandard2.1` ([csproj](https://github.com/LethalCompanyCommunity/LethalCompanyTemplate/blob/HEAD/LethalCompanyTemplate/LethalCompanyTemplate.csproj)).
  - To check hands-on: does `Restory_Data/Managed/netstandard.dll` exist? If not, fall back to `net472`.
- **`Directory.Build.props`** is imported early by `Microsoft.Common.props`, found by walking up the directory tree, and its filename is case-sensitive on Linux ([MS Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)). Use it for shared properties, plus a conditional import of the machine-local file:
  ```xml
  <Import Project="$(MSBuildThisFileDirectory)GamePaths.props" Condition="Exists('$(MSBuildThisFileDirectory)GamePaths.props')" />
  ```
- **Machine-local game path:** this is established practice. Valheim's [JotunnModStub](https://github.com/Valheim-Modding/JotunnModStub) gitignores `Environment.props`, which holds `VALHEIM_INSTALL`, and derives `VALHEIM_MANAGED=$(VALHEIM_INSTALL)/valheim_Data/Managed` from it ([.gitignore](https://github.com/Valheim-Modding/JotunnModStub/blob/master/.gitignore), [csproj](https://github.com/Valheim-Modding/JotunnModStub/blob/master/JotunnModStub/JotunnModStub.csproj)). For this repo, `GamePaths.props` defines `GameDir` (desktop value: `/mnt/c/Program Files (x86)/Steam/steamapps/common/Restory`). The laptop has its own copy. When `GameDir` is empty, the plugin project should be skipped or fail with a clear message (INFERENCE).
- **NuGet.Config** should list `https://nuget.bepinex.dev/v3/index.json`, as the [LC template](https://github.com/LethalCompanyCommunity/LethalCompanyTemplate/blob/HEAD/NuGet.Config) does. That's cleaner than the template's `RestoreAdditionalProjectSources` and works with `packages.lock.json` (INFERENCE).
- Note: the newest `BepInEx.Core` 5.x on the feed is **5.4.21** ([index](https://nuget.bepinex.dev/v3/package/bepinex.core/index.json)). Compiling against it and running on 5.4.23.5 should be fine, since 5.4.x releases after LTS are fixes only. This is UNVERIFIED for any API added after 5.4.21. Alternative: reference `$(GameDir)/BepInEx/core/BepInEx.dll` and `0Harmony.dll` directly.

## 3. Referencing game assemblies without committing them

| Option | How | CI? | Notes |
|---|---|---|---|
| **Local HintPath** (recommended) | `<Reference Include="Restory.Assembly" HintPath="$(GameDir)/Restory_Data/Managed/Restory.Assembly.dll" Private="false"/>`, which is the documented route ([BepInEx docs](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html), "Referencing from local install") | No | Zero distribution. Each rebuild compiles against the current game, which gives compile-time breakage detection. |
| **UnityEngine.Modules** (nuget.bepinex.dev) | `6000.3.10` exists. It contains lib/net35, net45 and netstandard2.0, and is a `developmentDependency` ([version index](https://nuget.bepinex.dev/v3/package/unityengine.modules/index.json); I inspected the nupkg) | Yes | Unity engine API only, no game code. Whether its DLLs are stripped is UNVERIFIED (CoreModule is 1.4 MB). **Pin it to the game's exact Unity version.** Unity 6.3 changed scene-handle signatures (`Scene.GetNameInternal`), and code compiled against an older Unity threw `MissingMethodException` ([UnityExplorer#81](https://github.com/yukieiji/UnityExplorer/issues/81)). |
| **GameLibs package** | Community-uploaded stripped and publicized game DLLs. Examples on nuget.bepinex.dev: `RiskOfRain2.GameLibs`, `Valheim.GameLibs`, `AmongUs.GameLibs.Steam`, `H3VR.GameLibs` ([RoR2 index](https://nuget.bepinex.dev/v3/package/riskofrain2.gamelibs/index.json), [search](https://nuget.bepinex.dev/v3/search?q=GameLibs)). Examples on nuget.org: `LethalCompany.GameLibs.Steam`, `TromboneChamp.GameLibs`, `BugFables.GameLibs` ([nuget.org search](https://www.nuget.org/packages?q=GameLibs); [BugFables.GameLibs repo](https://github.com/aldelaro5/BugFables.GameLibs)) | Yes | **No ReStory package exists**: the feed search for "restory" returned 0 results. BepInEx runs an upload service. It only stores "publicised and stripped game assemblies … publically available API definitions", needs an upload key, and strips server-side ([NuGetUpload wiki](https://github.com/BepInEx/BepInEx.NuGetUpload.Service/wiki), [repo](https://github.com/BepInEx/BepInEx.NuGetUpload.Service)). |
| **Self-made stripped refs** | `assembly-publicizer X.dll --strip` ([BepInEx.AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer)), `refasmer` ([JetBrains/Refasmer](https://github.com/JetBrains/Refasmer), v2.0.3), or NStrip (whose README calls itself "less maintained than" the BepInEx tool ([NStrip](https://github.com/bbepis/NStrip))) | Yes, via a private feed | Push them to GitHub Packages. That needs a token even for reads, using `GITHUB_TOKEN` in the linked repo ([GitHub docs](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)). Packages are private by default and inherit the repo's permissions. |
| **Self-hosted runner** on zantox-desktop | CI job runs where `GameDir` exists | Yes | Nothing is distributed. Desktop is always on. Also enables running TargetCheck in CI. |

- **Legality / etiquette:** I found no primary legal source on whether publishing stripped reference assemblies is allowed.
  - BepInEx's policy is to distribute only API definitions (above). That is a community norm, not a licence.
  - I did not check the ReStory/tinyBuild EULA (UNVERIFIED).
  - `Rewired_Core.dll` is a paid third-party asset, a separate copyright from the game.
  - **For a personal mod:** keep DLLs and stripped refs out of any public repo. Add `*.dll` and `lib/` to `.gitignore`. Use the local HintPath. If CI must build the plugin, use a self-hosted runner or a *private* GitHub Packages feed.
- **Public vs private repo:**
  - Public repo: Actions with standard runners are free. Private repo: 2,000 min/month on GitHub Free ([billing](https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-actions/about-billing-for-github-actions)).
  - Public repos auto-disable scheduled workflows after 60 days with no activity ([events doc](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)).
  - Self-hosted runners on public repos let fork PRs run code on your machine. That's a known GitHub warning, but I didn't fetch it here (UNVERIFIED). Use a self-hosted runner only on a private repo.

## 4. Publicizing

- **Krafs.Publicizer:**
  - Latest **2.3.2**, released 2026-08-03; repo pushed 2026-10-08 ([repo](https://github.com/krafs/Publicizer), [NuGet index](https://api.nuget.org/v3-flatcontainer/krafs.publicizer/index.json)).
  - Granular `<Publicize Include="Restory.Assembly:Ns.Type.member"/>` items, `MemberPattern` regex, `DoNotPublicize`, and `IncludeVirtualMembers="false"`, which avoids the runtime access mismatch when overriding publicized protected members.
  - Runtime strategies are `Unsafe` (works for Mono) and `IgnoresAccessChecksTo`; both are on by default ([README](https://github.com/krafs/Publicizer#how-publicizer-works)).
- **BepInEx.AssemblyPublicizer.MSBuild:**
  - Stable **0.4.3**, then 0.5.0-beta.1/.2; repo last pushed 2025-11-08 ([NuGet index](https://api.nuget.org/v3-flatcontainer/bepinex.assemblypublicizer.msbuild/index.json), [repo](https://github.com/BepInEx/BepInEx.AssemblyPublicizer)).
  - Usage is `Publicize="true"` on a `Reference` or `PackageReference`, or `<Publicize Include="Asm"/>`. On Mono it relies on `AllowUnsafeBlocks`. Its CLI can also strip.
- **Recommendation:** start with **no publicizer**. Use Harmony's `___fieldName` patch-parameter injection and `__instance` ([HarmonyX wiki: Patch parameters](https://github.com/BepInEx/HarmonyX/wiki/Patch-parameters)), plus `AccessTools.Field` and `FieldRefAccess`. These fail at **patch time**, inside your try/catch.
  - Publicized direct access fails at **JIT time** with `MissingFieldException`/`MissingMethodException` inside game callbacks once the game renames a member (INFERENCE from how the CLR binds members).
  - If heavy private access becomes necessary, use Krafs.Publicizer, which is more actively released, and limit it to specific types.

## 5. CI (GitHub Actions)

- **What CI can build without the game:** `RestoryMod.Core` and its tests always. The full plugin only with the self-hosted or private-feed options from §3.
- **Caching:** use `actions/setup-dotnet@v6` with `cache: true`. This needs `packages.lock.json`; add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` and run `dotnet restore --locked-mode` ([setup-dotnet README](https://github.com/actions/setup-dotnet#caching-nuget-packages)). The README examples use `actions/checkout@v7`.
- **Release artefact:**
  - Plain zip laid out as `BepInEx/plugins/RestoryMod/RestoryMod.dll`, which can be extracted over the game root.
  - **Thunderstore/r2modman don't apply**: none of Thunderstore's 343 communities matches ReStory (API listing at [thunderstore.io/api/experimental/community/](https://thunderstore.io/api/experimental/community/), queried 2026-10-15).
  - If that changes: the zip root needs `manifest.json` (`name` [A-Za-z0-9_] ≤128, `version_number` Major.Minor.Patch, `website_url`, `description` ≤250, `dependencies` as `team-name-version`), a 256×256 `icon.png` and `README.md` ([Thunderstore wiki](https://wiki.thunderstore.io/mods/creating-a-package)). `tcli build` can produce it ([thunderstore-cli](https://github.com/thunderstore-io/thunderstore-cli)).
- **Versioning:** keep SemVer in the csproj `<Version>`, which PluginInfoProps turns into `MyPluginInfo.PLUGIN_VERSION`. On a `v*` tag, build with `-p:Version=${GITHUB_REF_NAME#v}` and attach the zip to a GitHub Release (INFERENCE: standard MSBuild property override).
- **Detecting game updates:**
  - Steam's public appinfo for 3812600 (via [api.steamcmd.net](https://api.steamcmd.net/v1/info/3812600), a third-party cache of `steamcmd app_info`; source at [github.com/steamcmd/api](https://github.com/steamcmd/api)) shows:
    - branches `public` buildid **24885009** and `beta_version` buildid 24884958;
    - executable `Restory.exe`;
    - `controller_support: partial`;
    - Steam Deck `category: 3`. That this means "Verified" is UNVERIFIED.
    - Note: `public.timeupdated` is 2026-08-22, which conflicts with your DLL mtime of 2026-10-13. Either the cache is stale or the field means something else (UNVERIFIED). Cross-check against the local manifest.
  - **Scheduled workflow** (`on: schedule: cron`; the minimum interval is 5 min and runs can be delayed or dropped under load ([events doc](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows))):
    1. Run `steamcmd +login anonymous +app_info_update 1 +app_info_print 3812600 +quit`, or curl the steamcmd.net API.
    2. Extract `depots.branches.public.buildid`.
    3. Compare it to a value stored in the repo (or a variable).
    4. If it changed, open an issue.
    - Anonymous login is documented on the [Valve SteamCMD wiki](https://developer.valvesoftware.com/wiki/SteamCMD), which blocked direct fetch (403), so I confirmed it via search snippets. I couldn't confirm `app_info_print` on a Valve page (UNVERIFIED primary; widely used).
    - Watching `beta_version` gives earlier warning.
  - **Local detection is cheaper and authoritative:** in `dev.sh`, read `buildid` from `/mnt/c/Program Files (x86)/Steam/steamapps/appmanifest_3812600.acf`. I found no Valve documentation for that file format (UNVERIFIED). Also, or instead, compare the `Restory.Assembly.dll` MVID or SHA-256; the MVID distinguishes module versions ([MS Learn](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.module.moduleversionid)). If it changed, run TargetCheck before launching.

## 6. Update-resilience patterns (HarmonyX)

- **BepInEx uses HarmonyX, not vanilla Harmony.** HarmonyX is a fork of Harmony 2 specialised for game modding and BepInEx ([HarmonyX README](https://github.com/BepInEx/HarmonyX)). BepInEx 5 pins **2.9.0**, while the HarmonyX repo is at 2.16.1. Write to the 2.9.0 API.
- **`[HarmonyOptional]` (skip a missing target with a warning) doesn't exist in 2.9.0.** It's absent at tags v2.9.0 through v2.11.0 and present from **v2.12.0** ([Attributes.cs@master](https://github.com/BepInEx/HarmonyX/blob/master/Harmony/Public/Attributes.cs); I checked each tag).
- **`Prepare()` runs before target resolution** in 2.9.0. `PatchClassProcessor.Patch()` calls the main `HarmonyPrepare` first, and if it returns false, nothing is resolved or patched ([PatchClassProcessor.cs@v2.9.0](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/Harmony/Public/PatchClassProcessor.cs), lines 79–115; [wiki: Class patches](https://github.com/BepInEx/HarmonyX/wiki/Class-patches)). That makes `Prepare` the correct place for lookups.
- A `TargetMethod()` returning null **throws** ("returned an unexpected result: null"). An undefined attribute target throws `ArgumentException` (same file).
- **`PatchAll(Assembly)` processes classes in sequence with `.Do(...)`, so the first exception aborts all later classes.** `PatchAll(Type)` processes one class ([Harmony.cs@v2.9.0](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/Harmony/Public/Harmony.cs)). So apply patches per class, each in its own try/catch:
  ```csharp
  [HarmonyPatch] static class CursorStickPatch {
      static MethodBase target;
      static bool Prepare() {                       // runs first; no exception if target is gone
          target = AccessTools.Method(AccessTools.TypeByName("GameCursor"), "Update"); // illustrative names
          return target != null;                    // false => class skipped silently
      }
      static MethodBase TargetMethod() => target;
      static void Postfix(object __instance) { /* … */ }   // 'object' avoids hard type binding (INFERENCE)
  }
  // Plugin.Awake
  foreach (var t in Features.PatchClasses)
      try { harmony.PatchAll(t); Report(t, harmony) } catch (Exception e) { Log.LogError($"{t.Name} failed: {e.Message}"); }
  ```
  Report what was actually applied with `harmony.GetPatchedMethods()` or `Harmony.GetPatchInfo(m)` ([Harmony.cs@v2.9.0](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/Harmony/Public/Harmony.cs)). Log one summary line, such as `Patches: 7/8 applied; skipped: X (target missing)`. When the user's config disables a feature, skip its patch class.
- **AccessTools lookups return null and log a warning** on the `Warn` channel when a member is missing ([AccessTools.cs@v2.9.0](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/Harmony/Tools/AccessTools.cs)). HarmonyX added structured logging partly to debug "`AccessTools` returns `null` because a member got removed after a game update" ([wiki: differences](https://github.com/BepInEx/HarmonyX/wiki/Difference-between-Harmony-and-HarmonyX)). BepInEx forwards Harmony logs through `[Harmony.Logger] LogChannels`, default Warn|Error ([HarmonyLogSource.cs@v5-lts](https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx/Logging/HarmonyLogSource.cs)).
- **Prefix/postfix over transpilers:** transpilers depend on the IL shape, so any recompile of the method can break them (INFERENCE; widely accepted).
  - If you need one, use `CodeMatcher` with `ThrowIfNotMatch(...)`/`ThrowIfInvalid(...)`, which are present in 2.9.0 ([CodeMatcher.cs@v2.9.0](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/Harmony/Tools/CodeMatcher.cs); [wiki: Transpiler helpers](https://github.com/BepInEx/HarmonyX/wiki/Transpiler-helpers)). A mismatch then throws inside the per-class try/catch instead of silently emitting bad IL.
  - Avoid patching tiny methods: inlined methods make patches "non-functional" ([Harmony docs: edge cases](https://harmony.pardeike.net/articles/patching-edgecases.html)).
- **Version gate at startup:**
  - Log `Application.unityVersion` ([doc](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-unityVersion.html)) and `Application.version`, the PlayerSettings bundleVersion ([doc](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-version.html)).
  - Log the `Restory.Assembly` MVID too. Warn, don't refuse, when they differ from the build the mod was tested against. Embed those values at build time via an MSBuild property (INFERENCE).
- **Patch-target smoke test (`tests/RestoryMod.TargetCheck`):**
  - Keep every target in one `PatchTargets` table of (type name, member, parameter type names). `Prepare()` uses it at runtime and the test uses it too.
  - The test loads `$(GameDir)/Restory_Data/Managed/*.dll` into `System.Reflection.MetadataLoadContext` with a `PathAssemblyResolver`. That inspects assemblies as metadata only, can't execute their code, and handles assemblies built for other runtimes ([MS Learn](https://learn.microsoft.com/en-us/dotnet/standard/assembly/inspect-contents-using-metadataloadcontext)). It then asserts that every entry resolves.
  - Mono.Cecil 0.11.6 ([jbevain/cecil](https://github.com/jbevain/cecil)) is an equally valid option. MLC has the advantage of using the same reflection vocabulary as `AccessTools`.
  - Skip with `Skip` when `GameDir` is unset, so CI stays green. Run it locally after each update: `dotnet test tests/RestoryMod.TargetCheck`.

## 7. Dev loop

- **Build on WSL Linux, deploy to /mnt/c:**
  - Keep the repo in the Linux filesystem. Microsoft recommends storing files in the WSL filesystem when using Linux tools for performance ([MS Learn](https://learn.microsoft.com/en-us/windows/wsl/filesystems)).
  - Only copy outputs to `/mnt/c/.../Restory/BepInEx/plugins/RestoryMod/`. You can do that in an `AfterBuild` target conditioned on `Exists('$(GameDir)')`, which is the JotunnModStub pattern ([csproj](https://github.com/Valheim-Modding/JotunnModStub/blob/master/JotunnModStub/JotunnModStub.csproj)), or in `dev.sh`.
  - Windows locks a loaded plugin DLL while the game runs (INFERENCE; standard Windows file-lock behaviour), so close the game first with `taskkill.exe /IM Restory.exe`, or use ScriptEngine.
- **Launching from WSL:**
  - WSL can run `*.exe` directly, and CMD built-ins need `cmd.exe /C` ([MS Learn](https://learn.microsoft.com/en-us/windows/wsl/filesystems)). Use `cmd.exe /c start "" steam://rungameid/3812600` or `explorer.exe steam://rungameid/3812600`.
  - `steam://rungameid/<id>` is listed on the Valve wiki ([Steam browser protocol](https://developer.valvesoftware.com/wiki/Steam_browser_protocol); 403 on fetch, confirmed via search snippet).
  - Run it from `/mnt/c` so cmd.exe doesn't hit a UNC working directory (INFERENCE).
  - Doorstop hooks via `winhttp.dll` in the game root, so BepInEx loads however the exe is started (INFERENCE from the Doorstop design).
- **Launching `Restory.exe` directly:** if the game calls `SteamAPI_RestartAppIfNecessary`, it relaunches through Steam, which costs time but works. A `steam_appid.txt` next to the exe makes that call return false, so the game starts in place ([Steamworks API docs](https://partner.steamgames.com/doc/sdk/api)). That's a modification of the install, so leave it optional. Whether Restory calls it is UNVERIFIED.
- **Hot reload, ScriptEngine** ([BepInEx.Debug](https://github.com/BepInEx/BepInEx.Debug)): still maintained, with r11.2 on 2026-10-11 adding hot reload of interconnected assemblies.
  - It loads plugins from `BepInEx/scripts` and reloads on F6 (configurable).
  - You must undo your own Harmony patches and loose GameObjects on reload, for example with `harmony.UnpatchSelf()` in `OnDestroy`.
  - Each reload loads a renamed copy (`data-xxxx`), which complicates debugging ([README](https://github.com/BepInEx/BepInEx.Debug#scriptengine)).
  - Unity 6.3 compatibility is UNVERIFIED.
- **UnityExplorer:**
  - The original [sinai-dev/UnityExplorer](https://github.com/sinai-dev/UnityExplorer) has been archived since 2023.
  - [yukieiji/UnityExplorer](https://github.com/yukieiji/UnityExplorer) is maintained (v4.13.6, 2026-04-30), but its maintainer keeps an open list of **Unity 6.3 incompatibilities** and has declined broad fixes ([#104](https://github.com/yukieiji/UnityExplorer/issues/104)).
  - For Unity 6.3 Mono use **[Jettcodey/UnityExplorer](https://github.com/Jettcodey/UnityExplorer)**, "Fork adding Unity 6.3 & 6.4 Mono support". Its v4.14.2 (2026-08-22) ships `UnityExplorer.BepInEx5.Unity63.Mono.zip`. It's small (8★), so treat it as best-effort.
  - Another option is [UnloadedHangar/UnityExplorer-Unity6.BIE.Mono](https://github.com/UnloadedHangar/UnityExplorer-Unity6.BIE.Mono), tested only on Planet Crafter.
- **Config UI:** [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) v19.0 (BepInEx5 zip) gives an in-game editor for `Config.Bind` entries, which is useful for live-tuning curves and deadzones.
- **Debugging:**
  - Doorstop 4 has a Mono debugger server, enabled with `debug_enabled=true` and `debug_address` (default 127.0.0.1:10000) plus `debug_suspend`. It claims to work "without modifications to Mono" for VS, Rider or dnSpy, and dnSpy is auto-detected ([UnityDoorstop README](https://github.com/NeighTools/UnityDoorstop#debugging); keys in [doorstop_config.ini@v5-lts](https://github.com/BepInEx/BepInEx/blob/v5-lts/doorstop/doorstop_config.ini)).
  - The older BepInEx docs still describe swapping in a debug `mono.dll` or converting the game to a debug build ([plugins_vs.md](https://github.com/BepInEx/bepinex-docs/blob/master/articles/advanced/debug/plugins_vs.md)).
  - The dnSpyEx wiki says release builds need a patched `mono-2.0-bdwgc.dll`, but only ships them up to Unity 2018. Its alternative is converting to a debug build by swapping in `WindowsPlayer.exe`/`UnityPlayer.dll` from the same Unity version and setting `player-connection-debug=1` in `boot.config` ([dnSpyEx wiki](https://github.com/dnSpyEx/dnSpy/wiki/Debugging-Unity-Games)). dnSpyEx itself is maintained (v6.6.0, 2026-06).
  - **Whether Doorstop's debugger works on Unity 6000.3 release Mono is UNVERIFIED.** Try it first. The player swap modifies the install and has to be redone after every update.
  - Logging plus UnityExplorer is the pragmatic default.

## 8. Testing

- **Unit-testable in CI** (`RestoryMod.Core`, no UnityEngine/Rewired refs):
  - stick response curves (deadzone, anti-deadzone, exponent and curve shaping);
  - cursor acceleration and velocity integration;
  - radial vs axial deadzones;
  - snapping or "focus next interactable" selection logic over abstract rectangles;
  - config parsing and validation;
  - binding-table generation (action name to controller element), as data;
  - the `PatchTargets` table's internal consistency.
  Use `System.Numerics.Vector2` or your own struct, not `UnityEngine.Vector2`. Unity types depend on native engine calls outside Unity (INFERENCE; UNVERIFIED for which members are purely managed).
- **Locally testable without launching:** TargetCheck (§6).
- **Needs the game:** whether Harmony patches apply and behave correctly, Rewired runtime state (`ReInput.isReady`, maps, controllers), UI navigation and EventSystem behaviour, Zenject-bound services, timing and feel. Mitigate with an in-game self-check: on load, log the applied patches, Rewired players, controllers and map counts.

## 9. Rewired-specific

All from the official Rewired docs (guavaman.com). The API surface depends on the **Rewired version bundled with the game**. Check `Rewired_Core.dll`'s assembly version (UNVERIFIED for which version is bundled).

- **Lifecycle:**
  - `ReInput.isReady`, `ReInput.InitializedEvent` and `ReInput.ShutDownEvent`. Wait for `isReady` before touching maps ([ReInput API](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_ReInput.htm)).
  - `ReInput.players.GetPlayer(id|name)` and `GetSystemPlayer()` ([How-Tos](https://guavaman.com/projects/rewired/docs/HowTos.html)).
- **Controller events:** `ReInput.ControllerConnectedEvent`, `ControllerDisconnectedEvent` and `ControllerPreDisconnectEvent`. Controllers that were already connected at init may not fire events, so enumerate them on init ([How-Tos](https://guavaman.com/projects/rewired/docs/HowTos.html)).
- **Last active device**, for switching glyphs and cursor mode: `ReInput.controllers.GetLastActiveController()` / `GetLastActiveControllerType()` and `AddLastActiveControllerChangedDelegate` (How-Tos).
- **Is it a gamepad:** `controller.ImplementsTemplate<IGamepadTemplate>()` or `GetTemplate<IGamepadTemplate>()`. Template elements are `leftStick`, `rightStick`, `dPad`, `a`/`b`/`x`/`y`, `start` and so on. Gamepad-template maps apply to any *recognized* gamepad ([Controller Templates](https://guavaman.com/projects/rewired/docs/ControllerTemplates.html)).
- **Maps at runtime:**
  - `player.controllers.maps.LoadMap(ControllerType.Joystick, joystickId, category, layout, startEnabled)`, `AddMap`, `RemoveMap`, `ClearMaps`, `LoadDefaultMaps`, `SetMapsEnabled(bool, category)` and `GetAllMapsInCategory` (How-Tos).
  - Element bindings via `ControllerMap.CreateElementMap`, `ReplaceElementMap`, `DeleteElementMap`, `GetElementMapsWithAction` and `ContainsAction` ([ControllerMap API](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_ControllerMap.htm)).
  - XML round-trip via `ToXmlString()` and `AddMapsFromXml`.
  - Template maps via `ToControllerTemplateMap<IGamepadTemplate>()` and `ControllerTemplateMap.FromXml(xml).ToControllerMap(joystick)` ([Controller Templates](https://guavaman.com/projects/rewired/docs/ControllerTemplates.html)).
  - So a mod can ship its own gamepad map as XML and inject it on controller connect.
- **Input Behaviors** (per-action feel), via `player.controllers.maps.InputBehaviors` / `GetInputBehavior` (How-Tos):
  - `joystickAxisSensitivity`, `mouseXYAxisSensitivity`, `mouseXYAxisMode`, `buttonDeadZone`;
  - `digitalAxisSensitivity`/`Gravity`/`Simulation`;
  - `buttonRepeatDelay`/`buttonRepeatRate`, `buttonDoublePressSpeed`, `buttonShortPressTime`/`buttonLongPressTime` ([InputBehavior API](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_InputBehavior.htm)).
- **Per-axis calibration:** `joystick.calibrationMap.GetAxis(i)` returns an `AxisCalibration`, with `deadZone`, `sensitivityType`, `sensitivity`, `sensitivityCurve`, `invert` and range fields (How-Tos).
- **Global settings:** `ReInput.configuration` (How-Tos).
- **UI navigation:** the game's `CustomRewiredStandaloneInputModule` presumably derives from `RewiredStandaloneInputModule` (UNVERIFIED; inspect it). That module's Actions (default `UIHorizontal`/`UIVertical`/`UISubmit`/`UICancel`), `moveOneElementPerAxisPress`, `inputActionsPerSecond`, `repeatDelay` and the allow-mouse flags control gamepad UI navigation ([RewiredStandaloneInputModule](https://guavaman.com/projects/rewired/docs/RewiredStandaloneInputModule.html)).
  - The docs warn that changing its action or player lists from a UI callback can throw, so defer such changes to end of frame.
- **Virtual cursor:** a stick-driven pointer is a good fit for a repair game with a custom `GameCursor`. Rewired's **Player Mouse** is created with `PlayerMouse.Factory.Create()` and bound to Actions, and can be added to the Standalone Input Module at runtime ([Player Controllers](https://guavaman.com/projects/rewired/docs/PlayerControllers.html), [PlayerMouse API](https://guavaman.com/projects/rewired/docs/api-reference/html/T_Rewired_PlayerMouse.htm)). The other option is to Harmony-patch the game's cursor service, which Zenject binds via `GameCursorInstaller`.
- **Injecting synthetic input:** Custom Controllers, updated in `ReInput.InputSourceUpdateEvent` via `CustomController.SetAxisValue`/`SetButtonValue` and created with `ReInput.controllers.CreateCustomController…` ([Custom Controllers](https://guavaman.com/projects/rewired/docs/CustomControllers.html)).
- **Rebinding UI:** `InputMapper.Default.Start(context)`, with `ConflictFoundEvent` and `InputMappedEvent` ([InputMapper](https://guavaman.com/projects/rewired/docs/InputMapper.html)).
- **Typical mod approach** (INFERENCE): after `ReInput.isReady`, for each gamepad on connect:
  1. Load or merge a mod-supplied XML map into the relevant categories.
  2. Tune InputBehaviors and AxisCalibration from BepInEx config.
  3. Patch only the game-side gaps (cursor and interaction code that reads mouse position directly) with Harmony.
  This keeps most changes in Rewired's data layer, which is stable across game updates, and only the small remainder in IL patches.
- Steam appinfo already reports `controller_support: partial` and `primary_player_is_controller_slot_0: 1` ([api.steamcmd.net](https://api.steamcmd.net/v1/info/3812600)). That suggests existing gamepad maps to inspect before writing new ones.

---

## Open questions / verify hands-on

1. Is `Restory_Data/Managed/netstandard.dll` present? This decides between netstandard2.1 and net472. Is `mscorlib` stripped? If so, BepInEx #1150 applies and needs `dll_search_path_override`.
2. Does BepInEx 5.4.23.5 boot cleanly on 6000.3.10f1? Check `LogOutput.log` for #1385, the stack overflow in log redirection.
3. Which Rewired version is bundled (`Rewired_Core.dll` AssemblyVersion)? Does the game ship gamepad maps or a Gamepad Template map already (dump via UnityExplorer, or log `player.controllers.maps`)?
4. Does `CustomRewiredStandaloneInputModule` subclass `RewiredStandaloneInputModule`? How does `GameCursorInstaller` provide the cursor position?
5. Does Restory call `SteamAPI_RestartAppIfNecessary`, which affects launching `Restory.exe` directly?
6. Does Doorstop's `debug_enabled` work on Unity 6000.3 release Mono with Rider or dnSpyEx?
7. Does the Jettcodey UnityExplorer Unity63 build work on ReStory?
8. Buildid discrepancy: steamcmd.net says public build 24885009, updated 2026-08-22, but your DLLs changed 2026-10-13. Compare against the local `appmanifest_3812600.acf`.
9. Zenject assembly name in `Managed/` (`Zenject.dll`?), needed for the HintPath (UNVERIFIED).
10. Does UnityEngine.Modules 6000.3.10 match the local `Managed/UnityEngine.*.dll` closely enough? Or should you reference the local Unity DLLs instead, which also tracks Unity upgrades automatically?
11. tinyBuild/ReStory EULA position on mods and redistributing reference assemblies (not checked).

## Sources

BepInEx / HarmonyX
- https://github.com/BepInEx/BepInEx/releases · https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5 · https://github.com/BepInEx/BepInEx/pull/1264
- https://github.com/BepInEx/BepInEx/issues/1385 · /1150 · /1367 · README, Chainloader.cs, HarmonyLogSource.cs, doorstop_config.ini, BepInEx.Preloader.csproj @ branch `v5-lts`
- https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/1_setup.html · .../2_plugin_start.html · https://github.com/BepInEx/bepinex-docs (installation/unity_mono.md, advanced/debug/*)
- https://github.com/BepInEx/BepInEx.Templates (BepInEx5 template csproj/template.json, PluginInfoProps props/README)
- https://nuget.bepinex.dev/v3/index.json (package indexes for unityengine.modules, bepinex.core, riskofrain2.gamelibs, valheim.gamelibs; search API)
- https://github.com/BepInEx/BepInEx.NuGetUpload.Service (+ wiki)
- https://github.com/BepInEx/HarmonyX (README, wiki: Class-patches, Patching-with-Harmony, Difference-between-Harmony-and-HarmonyX, Patch-parameters, Transpiler-helpers; source @ v2.9.0 and master)
- https://github.com/BepInEx/BepInEx.Debug · https://github.com/BepInEx/BepInEx.ConfigurationManager · https://github.com/BepInEx/BepInEx.AssemblyPublicizer
- https://github.com/NeighTools/UnityDoorstop

Other tools
- https://github.com/krafs/Publicizer · https://github.com/bbepis/NStrip · https://github.com/JetBrains/Refasmer · https://github.com/jbevain/cecil
- https://github.com/LavaGang/MelonLoader/releases · https://github.com/LavaGang/MelonLoader/issues/1199
- https://github.com/sinai-dev/UnityExplorer · https://github.com/yukieiji/UnityExplorer (issues #81, #104) · https://github.com/Jettcodey/UnityExplorer · https://github.com/UnloadedHangar/UnityExplorer-Unity6.BIE.Mono
- https://github.com/dnSpyEx/dnSpy (+ wiki Debugging-Unity-Games)
- https://github.com/LethalCompanyCommunity/LethalCompanyTemplate · https://github.com/Valheim-Modding/JotunnModStub · https://github.com/aldelaro5/BugFables.GameLibs
- https://wiki.thunderstore.io/mods/creating-a-package · https://github.com/thunderstore-io/thunderstore-cli · https://thunderstore.io/api/experimental/community/

Microsoft / GitHub / Unity / Valve
- https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory · https://learn.microsoft.com/en-us/windows/wsl/filesystems
- https://learn.microsoft.com/en-us/dotnet/standard/assembly/inspect-contents-using-metadataloadcontext · https://learn.microsoft.com/en-us/dotnet/api/system.reflection.module.moduleversionid
- https://github.com/actions/setup-dotnet · https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry
- https://docs.github.com/en/billing/managing-billing-for-your-products/managing-billing-for-github-actions/about-billing-for-github-actions · https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows
- https://docs.unity3d.com/6000.3/Documentation/Manual/dotnet-profile-support.html · .../ScriptReference/Application-version.html · .../Application-unityVersion.html
- https://store.steampowered.com/app/3812600/ · https://partner.steamgames.com/doc/sdk/api · https://developer.valvesoftware.com/wiki/SteamCMD · https://developer.valvesoftware.com/wiki/Steam_browser_protocol (Valve wiki: 403 on direct fetch; content via search snippets)
- https://api.steamcmd.net/v1/info/3812600 + https://github.com/steamcmd/api (third-party cache of SteamCMD appinfo; **SECONDARY** for Steam data)

Rewired (guavaman.com)
- https://guavaman.com/projects/rewired/docs/HowTos.html · ControllerTemplates.html · RewiredStandaloneInputModule.html · InputMapper.html · CustomControllers.html · PlayerControllers.html
- API: T_Rewired_ReInput.htm · T_Rewired_InputBehavior.htm · T_Rewired_ControllerMap.htm · T_Rewired_PlayerMouse.htm
