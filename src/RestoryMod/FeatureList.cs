using System.Collections.Generic;
using RestoryMod.Core;
using RestoryMod.Features.AppSwitch;
using RestoryMod.Features.ButtonPrompts;
using RestoryMod.Features.ButtonSwap;
using RestoryMod.Features.CursorSpeed;
using RestoryMod.Features.Diagnostics;
using RestoryMod.Features.DpadSend;
using RestoryMod.Features.DropHint;
using RestoryMod.Features.DropdownModes;
using RestoryMod.Features.Fade;
using RestoryMod.Features.Hitboxes;
using RestoryMod.Features.HoverGrace;
using RestoryMod.Features.HoverOutline;
using RestoryMod.Features.MailList;
using RestoryMod.Features.Notepad;
using RestoryMod.Features.PartsBox;
using RestoryMod.Features.PartsStore;
using RestoryMod.Features.R2Tool;
using RestoryMod.Features.RotationSpeed;
using RestoryMod.Features.Settings;
using RestoryMod.Features.ShoulderButtons;
using RestoryMod.Features.StickScroll;
using RestoryMod.Features.WorkbenchAdvice;
using RestoryMod.Features.ZoomToggle;

namespace RestoryMod;

/// <summary>Every feature the plugin loads, one per manifest.</summary>
internal static class FeatureList
{
    /// <summary>
    /// Load order. Undo steps run in reverse. A feature that takes over another's work after a hot reload
    /// comes after it.
    /// </summary>
    public static IReadOnlyList<IFeature> All { get; } =
    [
        new ButtonPromptsFeature(),
        new PlayStationArtFeature(),
        new CursorSpeedFeature(),
        new RotationSpeedFeature(),
        new DropdownModesFeature(),
        new SettingsRowsFeature(),
        new PartsBoxFeature(),
        new PartsStoreFeature(),
        new PartsBoxDiagnosticsFeature(),
        new NotepadKeyFeature(),
        new StickScrollFeature(),
        new ShoulderButtonsFeature(),
        new MailListFeature(),
        new AppSwitchFeature(),
        new DpadSendFeature(),
        new HoverGraceFeature(),
        new HitboxOverlayFeature(),
        new HitboxEnlargingFeature(),
        new HintPulseFeature(),
        new DropHintFeature(),
        new WorkbenchSessionFeature(),
        new TubeModeFeature(),
        new ScrewHintButtonFeature(),
        // After WorkbenchSession: its hint ghosts are taken over through the session's service.
        new EveryPartHintFeature(),
        new ZoomToggleFeature(),
        new WorkbenchAdviceFeature(),
        new R2ToolFeature(),
        new DeviceFadeFeature(),
        new NotepadInventorySwapFeature(),
        new SettingsMenuDumpFeature(),
        new GamepadBindingsDumpFeature(),
        new OutlineDiagnosticsFeature(),
        new BlockedHoverFeature(),
        new ConditionHoverFeature(),
    ];

    /// <summary>
    /// Manifests with no feature of their own here: a helper other features call (TargetCheck still names
    /// it when a target moves), and the end-to-end harness, which its own plugin loads.
    /// </summary>
    public static IReadOnlyList<FeatureManifest> LoadedElsewhere { get; } =
    [
        FeatureManifests.PcClickSound,
        FeatureManifests.EndToEndHarness,
    ];
}
