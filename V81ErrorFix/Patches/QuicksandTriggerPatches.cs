using System;
using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;

namespace V81ErrorFix;

[HarmonyPatch(typeof(QuicksandTrigger), "OnExit")]
internal static class QuicksandTriggerOnExitPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(QuicksandTrigger __instance, Collider other)
    {
        try
        {
            return ShouldRunVanilla(__instance, other);
        }
        catch (Exception ex)
        {
            Warnings.Warn("guard-failure", $"Skipped QuicksandTrigger.OnExit because the guard failed safely before vanilla execution: {ex.GetType().Name}.");
            return false;
        }
    }

    private static bool ShouldRunVanilla(QuicksandTrigger quicksandTrigger, Collider other)
    {
        if (quicksandTrigger == null || other == null)
        {
            Warnings.Warn("missing-trigger-or-collider", "Skipped QuicksandTrigger.OnExit because the trigger or collider was missing.");
            return false;
        }

        if (!other.CompareTag("Player"))
        {
            return true;
        }

        if (GameNetworkManager.Instance == null)
        {
            Warnings.Warn("missing-network-manager", "Skipped QuicksandTrigger.OnExit because GameNetworkManager.Instance was missing.");
            return false;
        }

        PlayerControllerB player = other.gameObject != null
            ? other.gameObject.GetComponent<PlayerControllerB>() ?? other.GetComponentInParent<PlayerControllerB>()
            : other.GetComponentInParent<PlayerControllerB>();
        if (player == null)
        {
            Warnings.Warn("missing-player", $"Skipped QuicksandTrigger.OnExit for '{other.name}' because no PlayerControllerB was found on the player collider.");
            return false;
        }

        return true;
    }
}
