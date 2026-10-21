using Restory.Data.Outline;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using RestoryMod.Features.PartsBox;

namespace RestoryMod.Features;

/// <summary>
/// The condition look: the game's own outline for a part's condition (burnt, damaged, dirty), worn by a
/// ready part's pulse and by its hover outline, so the two agree. Also where an unknown part's condition
/// comes to light.
/// </summary>
internal static class ConditionLook
{
    /// <summary>The game's outline for this condition; null for a part that needs no work.</summary>
    public static OutlineSettingsPreset? Preset(ElementOutlineSettings settings, PartCondition condition) => condition switch
    {
        PartCondition.Burnt => settings.BurntElementOutline,
        PartCondition.Damaged => settings.DamagedElementOutline,
        PartCondition.Dirty => settings.DirtyElementOutline,
        _ => null,
    };

    /// <summary>The part counts as seen, no longer an unknown part; returns its condition.</summary>
    public static PartCondition Reveal(ElementBase part)
    {
        var data = part.ConditionHandler.ElementData;
        data.IsInspected = true;
        return BenchState.ConditionOf(data.Condition);
    }
}
