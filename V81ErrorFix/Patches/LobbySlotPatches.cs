using System;
using HarmonyLib;

namespace V81ErrorFix;

[HarmonyPatch(typeof(LobbySlot), "SetModdedIcon")]
internal static class LobbySlotSetModdedIconPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(LobbySlot __instance, ModdedState moddedState)
    {
        if (__instance == null)
        {
            Warnings.Warn("null-slot", "Skipped LobbySlot.SetModdedIcon because the lobby slot was null.");
            return false;
        }

        if (moddedState == ModdedState.Unknown && __instance.modStateUnknownIcon == null)
        {
            Warnings.Warn("missing-unknown-icon", "Skipped LobbySlot.SetModdedIcon because modStateUnknownIcon was missing.");
            return false;
        }

        if (moddedState == ModdedState.Modded && __instance.modStateTrueIcon == null)
        {
            Warnings.Warn("missing-modded-icon", "Skipped LobbySlot.SetModdedIcon because modStateTrueIcon was missing.");
            return false;
        }

        return true;
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("finalizer-null-reference", "Suppressed LobbySlot.SetModdedIcon NullReferenceException.");
            return null;
        }

        return __exception;
    }
}
