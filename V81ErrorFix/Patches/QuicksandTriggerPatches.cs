using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Unity.AI.Navigation;
using UnityEngine;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine.AI;

namespace V81ErrorFix;

[HarmonyPatch(typeof(QuicksandTrigger), "OnExit")]
internal static class QuicksandTriggerOnExitPatch
{
    private const int MaxWarnings = 5;
    private static readonly Dictionary<string, int> WarningCounts = new();

    private static bool Prefix(QuicksandTrigger __instance, Collider other)
    {
        try
        {
            HandleExitSafely(__instance, other);
        }
        catch (Exception ex)
        {
            Warn("exception", $"Suppressed QuicksandTrigger.OnExit failure safely: {ex.GetType().Name}.");
        }

        return false;
    }

    private static void HandleExitSafely(QuicksandTrigger quicksandTrigger, Collider other)
    {
        if (quicksandTrigger == null || other == null)
        {
            Warn("missing-trigger-or-collider", "Skipped QuicksandTrigger.OnExit because the trigger or collider was missing.");
            return;
        }

        PlayerControllerB player = TryGetPlayer(other);
        if (player == null)
        {
            if (other.CompareTag("Player"))
            {
                Warn("missing-player", $"Skipped QuicksandTrigger.OnExit for '{other.name}' because no PlayerControllerB was found.");
            }

            return;
        }

        if (GameNetworkManager.Instance == null)
        {
            Warn("missing-network-manager", "Skipped QuicksandTrigger.OnExit because GameNetworkManager.Instance was missing.");
            return;
        }

        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (!quicksandTrigger.sinkingLocalPlayer)
        {
            if (quicksandTrigger.isWater && player != localPlayer)
            {
                player.isUnderwater = false;
            }

            return;
        }

        if (player == localPlayer)
        {
            quicksandTrigger.StopSinkingLocalPlayer(player);
        }
    }

    private static PlayerControllerB TryGetPlayer(Collider other)
    {
        if (other == null || other.gameObject == null)
        {
            return null;
        }

        return other.gameObject.GetComponent<PlayerControllerB>() ?? other.GetComponentInParent<PlayerControllerB>();
    }

    private static void Warn(string key, string message)
    {
        WarningCounts.TryGetValue(key, out int warningCount);
        if (warningCount >= MaxWarnings)
        {
            return;
        }

        warningCount++;
        WarningCounts[key] = warningCount;
        Plugin.Log?.LogWarning($"{message} ({warningCount}/{MaxWarnings})");
    }
}
