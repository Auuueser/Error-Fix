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

[HarmonyPatch(typeof(PlayerControllerB), "NearOtherPlayers")]
internal static class PlayerControllerBNearOtherPlayersPatch
{
    private static bool Prefix(PlayerControllerB __instance, float checkRadius, ref bool __result)
    {
        try
        {
            return CheckNearOtherPlayersSafely(__instance, checkRadius, ref __result);
        }
        catch (Exception ex)
        {
            __result = false;
            return NullRefGuard.Suppress(ex, "PlayerControllerB.NearOtherPlayers", () =>
                __instance == null || StartOfRound.Instance == null || StartOfRound.Instance.allPlayerScripts == null) == null ? false : true;
        }
    }

    private static bool CheckNearOtherPlayersSafely(PlayerControllerB __instance, float checkRadius, ref bool __result)
    {
        if (__instance == null || __instance.transform == null || StartOfRound.Instance == null || StartOfRound.Instance.allPlayerScripts == null)
        {
            __result = false;
            return false;
        }

        float sqrCheckRadius = checkRadius * checkRadius;
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (player == null || player == __instance || !player.isPlayerControlled || player.transform == null)
            {
                continue;
            }

            if ((__instance.transform.position - player.transform.position).sqrMagnitude < sqrCheckRadius)
            {
                __result = true;
                return false;
            }
        }

        __result = false;
        return false;
    }
}
[HarmonyPatch(typeof(PlayerControllerB), "PlayJumpAudio")]
internal static class PlayerControllerBPlayJumpAudioPatch
{
    private const int MaxWarnings = 5;
    private static int _warningCount;

    private static bool Prefix(PlayerControllerB __instance)
    {
        try
        {
            PlayJumpAudioSafely(__instance);
        }
        catch (Exception ex)
        {
            Warn($"Skipped PlayerControllerB.PlayJumpAudio because the guard failed safely: {ex.GetType().Name}.");
        }

        return false;
    }

    private static void PlayJumpAudioSafely(PlayerControllerB player)
    {
        if (player == null || player.movementAudio == null || StartOfRound.Instance == null)
        {
            Warn("Skipped PlayerControllerB.PlayJumpAudio because audio dependencies were not ready.");
            return;
        }

        AudioClip jumpAudio = TryGetSuitJumpAudio(player, out string missingDependency) ?? TryGetDefaultJumpAudio(ref missingDependency);
        if (jumpAudio == null)
        {
            Warn($"Skipped PlayerControllerB.PlayJumpAudio because no jump audio clip was available: {missingDependency}.");
            return;
        }

        if (!string.IsNullOrEmpty(missingDependency))
        {
            Warn($"Used default jump audio because suit jump audio was not available: {missingDependency}.");
        }

        player.movementAudio.PlayOneShot(jumpAudio);
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is ArgumentOutOfRangeException || __exception is NullReferenceException)
        {
            Warn($"Suppressed PlayerControllerB.PlayJumpAudio {__exception.GetType().Name}.");
            return null;
        }

        return __exception;
    }

    private static AudioClip TryGetSuitJumpAudio(PlayerControllerB player, out string missingDependency)
    {
        missingDependency = string.Empty;
        if (StartOfRound.Instance.unlockablesList == null || StartOfRound.Instance.unlockablesList.unlockables == null)
        {
            missingDependency = "StartOfRound.Instance.unlockablesList";
            return null;
        }

        if (player.currentSuitID < 0 || player.currentSuitID >= StartOfRound.Instance.unlockablesList.unlockables.Count)
        {
            missingDependency = $"currentSuitID {player.currentSuitID} outside unlockables count {StartOfRound.Instance.unlockablesList.unlockables.Count}";
            return null;
        }

        UnlockableItem unlockable = StartOfRound.Instance.unlockablesList.unlockables[player.currentSuitID];
        if (unlockable == null)
        {
            missingDependency = $"unlockables[{player.currentSuitID}]";
            return null;
        }

        AudioClip suitJumpAudio = unlockable.jumpAudio;
        if (suitJumpAudio == null)
        {
            missingDependency = $"unlockables[{player.currentSuitID}].jumpAudio";
        }

        return suitJumpAudio;
    }

    private static AudioClip TryGetDefaultJumpAudio(ref string missingDependency)
    {
        if (StartOfRound.Instance == null || StartOfRound.Instance.playerJumpSFX == null)
        {
            missingDependency = string.IsNullOrEmpty(missingDependency)
                ? "StartOfRound.Instance.playerJumpSFX"
                : $"{missingDependency}; StartOfRound.Instance.playerJumpSFX";
            return null;
        }

        return StartOfRound.Instance.playerJumpSFX;
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

[HarmonyPatch(typeof(PlayerControllerB), "PlayerJumpedClientRpc")]
internal static class PlayerControllerBPlayerJumpedClientRpcPatch
{
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is ArgumentOutOfRangeException || __exception is NullReferenceException)
        {
            PlayerControllerBPlayJumpAudioPatch.Warn($"Suppressed PlayerControllerB.PlayerJumpedClientRpc {__exception.GetType().Name} from jump audio playback.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
internal static class PlayerControllerBThrowObjectClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly FieldInfo RpcExecStageField = AccessTools.Field(typeof(NetworkBehaviour), "__rpc_exec_stage");
    private static readonly FieldInfo ThrowingObjectField = AccessTools.Field(typeof(PlayerControllerB), "throwingObject");
    private static bool _rpcStageInitialized;
    private static bool _rpcStageReady;
    private static object _rpcExecStageSend;
    private static bool _loggedRpcStageValues;

    private static bool Prefix(
        PlayerControllerB __instance,
        bool droppedInElevator,
        bool droppedInShipRoom,
        Vector3 targetFloorPosition,
        NetworkObjectReference grabbedObject,
        int floorYRot)
    {
        if (!PatchModeUtility.IsEnabled(ErrorFixConfig.ThrowObjectClientRpcGuardMode))
        {
            return true;
        }

        if (!EnsureRpcExecStageReady() || !IsExecutingClientRpc(__instance))
        {
            return true;
        }

        object oldRpcStage = GetRpcExecStage(__instance);
        bool rpcStageChanged = false;
        bool stateMutated = false;
        try
        {
            HandleThrowObjectClientRpcSafely(__instance, droppedInElevator, droppedInShipRoom, targetFloorPosition, grabbedObject, floorYRot, ref rpcStageChanged, ref stateMutated);
        }
        catch (Exception ex)
        {
            if (stateMutated)
            {
                SetRpcExecStageSend(__instance);
                TryFinishThrowingIfOwner(__instance);
                Warnings.Warn("guard-failed-after-mutation", $"ThrowObjectClientRpc guard failed after partial state changes; skipped vanilla to avoid double-discard: {ex.GetType().Name}.");
                return false;
            }

            if (rpcStageChanged)
            {
                SetRpcExecStage(__instance, oldRpcStage);
            }

            Warnings.Warn("guard-failed-before-mutation", $"ThrowObjectClientRpc guard failed before changing held object state and allowed vanilla to run: {ex.GetType().Name}.");
            return true;
        }

        return false;
    }

    private static void HandleThrowObjectClientRpcSafely(
        PlayerControllerB player,
        bool droppedInElevator,
        bool droppedInShipRoom,
        Vector3 targetFloorPosition,
        NetworkObjectReference grabbedObject,
        int floorYRot,
        ref bool rpcStageChanged,
        ref bool stateMutated)
    {
        if (player == null || player.NetworkManager == null || !player.NetworkManager.IsListening || (!player.NetworkManager.IsClient && !player.NetworkManager.IsHost))
        {
            return;
        }

        SetRpcExecStageSend(player);
        rpcStageChanged = true;
        if (grabbedObject.TryGet(out NetworkObject networkObject) && networkObject != null)
        {
            GrabbableObject grabbableObject = networkObject.GetComponent<GrabbableObject>();
            if (grabbableObject == null)
            {
                Warnings.Warn($"missing-grabbable|{player.playerClientId}", $"Skipped ThrowObjectClientRpc for player #{player.playerClientId} because the NetworkObject had no GrabbableObject.");
                stateMutated = true;
                FinishThrowingIfOwner(player);
                return;
            }

            if (!player.IsOwner)
            {
                stateMutated = true;
                player.SetObjectAsNoLongerHeld(droppedInElevator, droppedInShipRoom, targetFloorPosition, grabbableObject, floorYRot);
            }

            if (grabbableObject.itemProperties == null || !grabbableObject.itemProperties.syncDiscardFunction)
            {
                stateMutated = true;
                grabbableObject.playerHeldBy = null;
            }

            if (grabbableObject == player.currentlyHeldObjectServer)
            {
                stateMutated = true;
                player.currentlyHeldObjectServer = null;
            }
            else
            {
                Warnings.Warn($"held-mismatch|{player.playerClientId}", $"Suppressed ThrowObjectClientRpc held-object mismatch for player #{player.playerClientId}; currentlyHeldObjectServer was {GetHeldObjectName(player.currentlyHeldObjectServer)}.");
            }
        }
        else
        {
            Warnings.Warn($"missing-network-object|{player.playerClientId}", $"Suppressed ThrowObjectClientRpc because the server object reference was missing for player #{player.playerClientId}.");
        }

        stateMutated = true;
        FinishThrowingIfOwner(player);
    }

    private static bool EnsureRpcExecStageReady()
    {
        if (_rpcStageInitialized)
        {
            return _rpcStageReady;
        }

        _rpcStageInitialized = true;
        if (RpcExecStageField == null)
        {
            Warnings.Warn("rpc-stage-missing", "Disabled ThrowObjectClientRpc guard because NetworkBehaviour.__rpc_exec_stage was not found.");
            return false;
        }

        Type stageType = RpcExecStageField.FieldType;
        if (!RpcExecStageUtility.TryParseEnumValue(stageType, "Send", out _rpcExecStageSend))
        {
            Warnings.Warn("rpc-stage-send-missing", "Disabled ThrowObjectClientRpc guard because RpcExecStage.Send could not be resolved.");
            return false;
        }

        LogRpcStageValues(stageType);
        _rpcStageReady = true;
        return true;
    }

    private static void LogRpcStageValues(Type stageType)
    {
        if (_loggedRpcStageValues || stageType == null || !stageType.IsEnum)
        {
            return;
        }

        _loggedRpcStageValues = true;
        string values = string.Join(", ", Enum.GetNames(stageType));
        Plugin.Log?.LogInfo($"ThrowObjectClientRpc guard detected RpcExecStage values: {values}.");
    }

    private static bool IsExecutingClientRpc(PlayerControllerB player)
    {
        if (player == null || RpcExecStageField == null || !_rpcStageReady)
        {
            return false;
        }

        object execStage = RpcExecStageField.GetValue(player);
        return execStage != null && execStage.ToString() == "Execute";
    }

    private static string GetHeldObjectName(GrabbableObject heldObject)
    {
        if (heldObject == null)
        {
            return "null";
        }

        return heldObject.gameObject != null ? heldObject.gameObject.name : "missing GameObject";
    }

    private static void SetRpcExecStageSend(PlayerControllerB player)
    {
        SetRpcExecStage(player, _rpcExecStageSend);
    }

    private static object GetRpcExecStage(PlayerControllerB player)
    {
        return player != null && RpcExecStageField != null ? RpcExecStageField.GetValue(player) : null;
    }

    private static void SetRpcExecStage(PlayerControllerB player, object stage)
    {
        if (player != null && RpcExecStageField != null && stage != null)
        {
            RpcExecStageField.SetValue(player, stage);
        }
    }

    private static void FinishThrowingIfOwner(PlayerControllerB player)
    {
        if (player != null && player.IsOwner)
        {
            SetThrowingObject(player, false);
        }
    }

    private static void TryFinishThrowingIfOwner(PlayerControllerB player)
    {
        try
        {
            FinishThrowingIfOwner(player);
        }
        catch (Exception ex)
        {
            Warnings.Warn($"finish-throwing-failed|{GetPlayerId(player)}", $"Skipped ThrowObjectClientRpc owner throwing cleanup after guard failure because cleanup failed safely: {ex.GetType().Name}.");
        }
    }

    private static string GetPlayerId(PlayerControllerB player)
    {
        return player != null ? player.playerClientId.ToString() : "unknown";
    }

    private static void SetThrowingObject(PlayerControllerB player, bool value)
    {
        if (player != null && ThrowingObjectField != null)
        {
            ThrowingObjectField.SetValue(player, value);
        }
    }

}

[HarmonyPatch(typeof(PlayerControllerB), "GrabObjectClientRpc")]
internal static class PlayerControllerBGrabObjectClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(PlayerControllerB __instance, Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn($"GrabObjectClientRpc|{GetPlayerId(__instance)}", $"Suppressed PlayerControllerB.GrabObjectClientRpc NullReferenceException for player #{GetPlayerId(__instance)}.");
            return null;
        }

        return __exception;
    }

    private static string GetPlayerId(PlayerControllerB player)
    {
        return player != null ? player.playerClientId.ToString() : "unknown";
    }
}
