using System;
using System.Reflection;
using FMODUnity;
using Restory.Audio;
using Restory.UI.SFX;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features;

/// <summary>
/// The PC screen's own mouse-click sound, which the game plays only on a click (or A with the virtual
/// cursor). Steps the mod makes on the PC without a click play it too, so they sound like one.
/// </summary>
internal static class PcClickSound
{
    private static bool? targetsPresent;
    private static FieldInfo? soundField;
    private static FieldInfo? audioPlayerField;
    private static GUI_PcScreenClickSFX? sfx;
    private static bool errorLogged;

    public static void Play()
    {
        try
        {
            if (!(targetsPresent ??= GameTargets.AllPresent(FeatureManifests.PcClickSound))) return;
            if (!sfx) sfx = UnityEngine.Object.FindAnyObjectByType<GUI_PcScreenClickSFX>();
            if (!sfx) return;
            soundField ??= GameTargets.Field(PatchTargets.PcClickSound);
            audioPlayerField ??= GameTargets.Field(PatchTargets.PcClickAudioPlayer);
            // The audio service is a MonoBehaviour: mind Unity's fake null.
            if (audioPlayerField!.GetValue(sfx) is not IAudioPlayerService audioPlayer ||
                audioPlayer is UnityEngine.Object service && !service) return;
            audioPlayer.PlaySoundEventOneShot((EventReference)soundField!.GetValue(sfx));
        }
        catch (Exception e)
        {
            if (errorLogged) return;
            errorLogged = true;
            Plugin.Logger.LogError($"PC click sound failed (further errors suppressed): {e}");
        }
    }
}
