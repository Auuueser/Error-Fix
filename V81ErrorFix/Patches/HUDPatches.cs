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

[HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
internal static class HUDManagerAddChatMessagePatch
{
    private const int MaxWarnings = 5;
    private static int _warningCount;

    private static bool Prefix(HUDManager __instance)
    {
        if (IsHudChatReady(__instance, out string missingDependency))
        {
            return true;
        }

        Warn($"Skipped HUDManager.AddChatMessage because chat HUD was not ready: {missingDependency}.");
        return false;
    }

    private static Exception Finalizer(HUDManager __instance, Exception __exception)
    {
        if (__exception is NullReferenceException && !IsHudChatReady(__instance, out _))
        {
            Warn("Suppressed HUDManager.AddChatMessage NullReferenceException while chat HUD was not ready.");
            return null;
        }

        return __exception;
    }

    internal static bool IsHudChatReady(HUDManager hudManager, out string missingDependency)
    {
        if (hudManager == null)
        {
            missingDependency = "HUDManager";
            return false;
        }

        if (hudManager.Chat == null)
        {
            missingDependency = "HUDManager.Chat";
            return false;
        }

        if (hudManager.chatText == null)
        {
            missingDependency = "HUDManager.chatText";
            return false;
        }

        if (hudManager.ChatMessageHistory == null)
        {
            missingDependency = "HUDManager.ChatMessageHistory";
            return false;
        }

        if (GameNetworkManager.Instance == null)
        {
            missingDependency = "GameNetworkManager.Instance";
            return false;
        }

        if (GameNetworkManager.Instance.localPlayerController == null)
        {
            missingDependency = "GameNetworkManager.Instance.localPlayerController";
            return false;
        }

        if (StartOfRound.Instance == null)
        {
            missingDependency = "StartOfRound.Instance";
            return false;
        }

        if (StartOfRound.Instance.allPlayerScripts == null || StartOfRound.Instance.allPlayerScripts.Length < 4)
        {
            missingDependency = "StartOfRound.Instance.allPlayerScripts";
            return false;
        }

        int playerSlotsToCheck = Math.Min(4, StartOfRound.Instance.allPlayerScripts.Length);
        for (int i = 0; i < playerSlotsToCheck; i++)
        {
            if (StartOfRound.Instance.allPlayerScripts[i] == null)
            {
                missingDependency = $"StartOfRound.Instance.allPlayerScripts[{i}]";
                return false;
            }
        }

        missingDependency = string.Empty;
        return true;
    }

    internal static void Warn(string message)
    {
        if (_warningCount >= MaxWarnings)
        {
            return;
        }

        _warningCount++;
        Plugin.Log?.LogWarning($"{message} ({_warningCount}/{MaxWarnings})");
    }
}

[HarmonyPatch(typeof(HUDManager), "AddTextMessageClientRpc")]
internal static class HUDManagerAddTextMessageClientRpcPatch
{
    private static bool Prefix(HUDManager __instance)
    {
        if (HUDManagerAddChatMessagePatch.IsHudChatReady(__instance, out string missingDependency))
        {
            return true;
        }

        HUDManagerAddChatMessagePatch.Warn($"Skipped HUDManager.AddTextMessageClientRpc because chat HUD was not ready: {missingDependency}.");
        return false;
    }

    private static Exception Finalizer(HUDManager __instance, Exception __exception)
    {
        if (__exception is NullReferenceException && !HUDManagerAddChatMessagePatch.IsHudChatReady(__instance, out _))
        {
            HUDManagerAddChatMessagePatch.Warn("Suppressed HUDManager.AddTextMessageClientRpc NullReferenceException while chat HUD was not ready.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(HUDManager), "SyncAllPlayerLevelsServerRpc", new Type[] { })]
internal static class HUDManagerSyncAllPlayerLevelsServerRpcPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(HUDManager __instance)
    {
        if (__instance == null || __instance.NetworkManager == null || !__instance.NetworkManager.IsListening)
        {
            return false;
        }

        if ((__instance.NetworkManager.IsClient || __instance.NetworkManager.IsHost) && __instance.OwnerClientId != __instance.NetworkManager.LocalClientId)
        {
            Warnings.Warn("not-owner", "Skipped HUDManager.SyncAllPlayerLevelsServerRpc because the local client does not own HUDManager.");
            return false;
        }

        return true;
    }
}
