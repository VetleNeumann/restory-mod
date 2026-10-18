using System;
using System.IO;
using System.Runtime.CompilerServices;
using Steamworks;
using UnityEngine;

namespace RestoryMod.Features.ButtonPrompts;

/// <summary>
/// Asks Steam which controller sits behind Steam Input. The game initialises the Steam API but never
/// Steam Input, so we start it ourselves; Valve's guide for XInput games calls for exactly this
/// (Init, then GetControllerForGamepadIndex + GetInputTypeForHandle). Steam pumps it from the game's
/// own RunCallbacks, so no RunFrame is needed.
/// </summary>
internal static class SteamPads
{
    private const int XInputSlots = 4;
    private const int MaxInitAttempts = 30;
    private const string UnknownType = "k_ESteamInputType_Unknown";

    private static bool initialised;
    private static int initAttempts;
    private static bool unavailable;
    private static int cachedFrame = -1;
    private static string? cachedType;

    /// <summary>
    /// The ESteamInputType name of the first pad Steam Input is handling, or null when it handles
    /// none (Steam Input off for the pad, game not launched through Steam, or Steam not up yet).
    /// Cached per frame: every on-screen prompt asks while it refreshes.
    /// </summary>
    public static string? ActiveTypeName()
    {
        if (unavailable || !ButtonPromptsFeature.UseSteamInput.Value) return null;
        if (cachedFrame == Time.frameCount) return cachedType;
        cachedFrame = Time.frameCount;
        cachedType = null;

        try
        {
            cachedType = Query();
        }
        catch (InvalidOperationException)
        {
            // Steamworks.NET throws this until the game has initialised the Steam API; ask again later.
        }
        catch (Exception e) when (e is TypeLoadException or MissingMethodException or FileNotFoundException or
                                       DllNotFoundException or EntryPointNotFoundException)
        {
            // A game update changed or dropped Steamworks.NET: fall back to Rewired's view for good.
            unavailable = true;
            Plugin.Logger.LogWarning($"Steam Input unavailable, button prompts follow Rewired only: {e.Message}");
        }
        return cachedType;
    }

    // Kept out of ActiveTypeName so a missing Steamworks assembly fails here, inside its try.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? Query()
    {
        if (!initialised)
        {
            if (!SteamInput.Init(false))
            {
                if (++initAttempts >= MaxInitAttempts)
                {
                    unavailable = true;
                    Plugin.Logger.LogWarning("Steam Input did not start; button prompts follow Rewired only.");
                }
                return null;
            }
            initialised = true;
            Plugin.Logger.LogInfo("Steam Input initialised for controller detection.");
        }

        // A pad Steam has named beats one it still reports as Unknown (it does so for a moment after start-up).
        string? found = null;
        for (var slot = 0; slot < XInputSlots; slot++)
        {
            var handle = SteamInput.GetControllerForGamepadIndex(slot);
            if (handle.m_InputHandle == 0) continue;
            found = SteamInput.GetInputTypeForHandle(handle).ToString();
            if (found != UnknownType) break;
        }
        return found;
    }
}
