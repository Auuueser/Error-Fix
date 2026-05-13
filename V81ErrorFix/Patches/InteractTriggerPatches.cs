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

[HarmonyPatch(typeof(InteractTrigger), "UpdateUsedByPlayerClientRpc")]
internal static class InteractTriggerUpdateUsedByPlayerClientRpcPatch
{
    private const int MaxWarnings = 5;
    private static int _warningCount;

    private static bool Prefix(InteractTrigger __instance, int playerNum)
    {
        if (IsInteractRpcReady(__instance, playerNum, out string missingDependency))
        {
            return true;
        }

        Warn($"Skipped InteractTrigger.UpdateUsedByPlayerClientRpc for player {playerNum} because dependencies were not ready: {missingDependency}.");
        return false;
    }

    private static Exception Finalizer(InteractTrigger __instance, int playerNum, Exception __exception)
    {
        if (__exception is NullReferenceException && !IsInteractRpcReady(__instance, playerNum, out _))
        {
            Warn("Suppressed InteractTrigger.UpdateUsedByPlayerClientRpc NullReferenceException while trigger dependencies were not ready.");
            return null;
        }

        return __exception;
    }

    private static bool IsInteractRpcReady(InteractTrigger trigger, int playerNum, out string missingDependency)
    {
        if (trigger == null)
        {
            missingDependency = "InteractTrigger";
            return false;
        }

        if (trigger.onInteractEarlyOtherClients == null)
        {
            missingDependency = $"{GetTriggerName(trigger)}.onInteractEarlyOtherClients";
            return false;
        }

        if (!TryGetPlayer(playerNum, out PlayerControllerB player, out missingDependency))
        {
            return false;
        }

        if (GameNetworkManager.Instance == null)
        {
            missingDependency = "GameNetworkManager.Instance";
            return false;
        }

        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (localPlayer == null || localPlayer.transform == null)
        {
            missingDependency = "GameNetworkManager.Instance.localPlayerController";
            return false;
        }

        if (trigger.specialCharacterAnimation && trigger.setVehicleAnimation && player.gameplayCamera == null)
        {
            missingDependency = $"player {playerNum} gameplayCamera";
            return false;
        }

        missingDependency = string.Empty;
        return true;
    }

    private static bool TryGetPlayer(int playerNum, out PlayerControllerB player, out string missingDependency)
    {
        player = null;
        if (StartOfRound.Instance == null || StartOfRound.Instance.allPlayerScripts == null || playerNum < 0 || playerNum >= StartOfRound.Instance.allPlayerScripts.Length)
        {
            missingDependency = "StartOfRound.Instance.allPlayerScripts";
            return false;
        }

        player = StartOfRound.Instance.allPlayerScripts[playerNum];
        if (player == null || player.transform == null)
        {
            missingDependency = $"StartOfRound.Instance.allPlayerScripts[{playerNum}]";
            return false;
        }

        missingDependency = string.Empty;
        return true;
    }

    private static string GetTriggerName(InteractTrigger trigger)
    {
        return trigger != null && trigger.gameObject != null ? trigger.gameObject.name : "unknown trigger";
    }

    private static void Warn(string message)
    {
        if (_warningCount >= MaxWarnings)
        {
            return;
        }

        _warningCount++;
        Plugin.Log?.LogWarning($"{message} ({_warningCount}/{MaxWarnings})");
    }
}
