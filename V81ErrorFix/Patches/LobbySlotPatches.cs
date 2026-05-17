using System;
using HarmonyLib;

namespace V81ErrorFix;

[HarmonyPatch(typeof(LobbySlot), "SetModdedIcon")]
internal static class LobbySlotSetModdedIconPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(LobbySlot __instance, ModdedState moddedState)
    {
        if (IsKnownMissingIcon(__instance, moddedState, out string reason))
        {
            Warnings.Warn($"missing-icon|{moddedState}|{reason}", $"Skipped LobbySlot.SetModdedIcon because {reason}.");
            return false;
        }

        return true;
    }

    private static Exception Finalizer(LobbySlot __instance, ModdedState moddedState, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "LobbySlot.SetModdedIcon", () =>
            IsKnownMissingIcon(__instance, moddedState, out _));
    }

    private static bool IsKnownMissingIcon(LobbySlot slot, ModdedState moddedState, out string reason)
    {
        if (slot == null)
        {
            reason = "slot was null";
            return true;
        }

        if (moddedState == ModdedState.Unknown && slot.modStateUnknownIcon == null)
        {
            reason = "modStateUnknownIcon was missing";
            return true;
        }

        if (moddedState == ModdedState.Modded && slot.modStateTrueIcon == null)
        {
            reason = "modStateTrueIcon was missing";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
