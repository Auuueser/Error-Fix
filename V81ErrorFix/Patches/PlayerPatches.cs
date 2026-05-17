using System;
using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;
using Unity.Netcode;

namespace V81ErrorFix;

[HarmonyPatch(typeof(PlayerControllerB), "NearOtherPlayers")]
internal static class PlayerControllerBNearOtherPlayersPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(PlayerControllerB __instance, float checkRadius, ref bool __result)
    {
        try
        {
            if (!HasUnsafeDependencies(__instance, out string reason))
            {
                return true;
            }

            bool runOriginal = CheckNearOtherPlayersSafely(__instance, checkRadius, ref __result);
            Warnings.Warn($"near-other-players|{reason}", $"Used safe PlayerControllerB.NearOtherPlayers fallback because {reason}.");
            return runOriginal;
        }
        catch (Exception ex)
        {
            __result = false;
            Warnings.Warn("near-other-players|guard-failure", $"Skipped PlayerControllerB.NearOtherPlayers because the guard failed safely: {ex.GetType().Name}.");
            return false;
        }
    }

    private static bool HasUnsafeDependencies(PlayerControllerB player, out string reason)
    {
        if (player == null)
        {
            reason = "player was null";
            return true;
        }

        if (player.transform == null)
        {
            reason = "player transform was missing";
            return true;
        }

        if (StartOfRound.Instance == null || StartOfRound.Instance.allPlayerScripts == null)
        {
            reason = "StartOfRound player list was missing";
            return true;
        }

        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerControllerB otherPlayer = players[i];
            if (otherPlayer == null)
            {
                reason = $"allPlayerScripts[{i}] was null";
                return true;
            }

            if (otherPlayer != player && otherPlayer.isPlayerControlled && otherPlayer.transform == null)
            {
                reason = $"allPlayerScripts[{i}].transform was missing";
                return true;
            }
        }

        reason = string.Empty;
        return false;
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
            if (!TryGetUnsafeJumpAudioFallback(__instance, out AudioClip fallbackClip, out string reason))
            {
                return true;
            }

            PlayJumpAudioSafely(__instance, fallbackClip, reason);
        }
        catch (Exception ex)
        {
            Warn($"Skipped PlayerControllerB.PlayJumpAudio because the guard failed safely: {ex.GetType().Name}.");
        }

        return false;
    }

    private static void PlayJumpAudioSafely(PlayerControllerB player, AudioClip fallbackClip, string reason)
    {
        if (player == null || player.movementAudio == null || fallbackClip == null)
        {
            Warn($"Skipped PlayerControllerB.PlayJumpAudio because audio dependencies were not ready: {reason}.");
            return;
        }

        Warn($"Used default jump audio because vanilla suit jump audio dependencies were unsafe: {reason}.");
        player.movementAudio.PlayOneShot(fallbackClip);
    }

    private static Exception Finalizer(PlayerControllerB __instance, Exception __exception)
    {
        if ((__exception is ArgumentOutOfRangeException || __exception is NullReferenceException) &&
            TryGetUnsafeJumpAudioFallback(__instance, out _, out string reason))
        {
            Warn($"Suppressed PlayerControllerB.PlayJumpAudio {__exception.GetType().Name} after known unsafe jump audio dependencies were detected: {reason}.");
            return null;
        }

        return __exception;
    }

    internal static bool TryGetUnsafeJumpAudioFallback(PlayerControllerB player, out AudioClip fallbackClip, out string reason)
    {
        fallbackClip = TryGetDefaultJumpAudio();
        if (player == null)
        {
            reason = "player was null";
            return true;
        }

        if (player.movementAudio == null)
        {
            reason = "movementAudio was missing";
            return true;
        }

        if (StartOfRound.Instance == null)
        {
            reason = "StartOfRound.Instance was missing";
            return true;
        }

        if (StartOfRound.Instance.unlockablesList == null || StartOfRound.Instance.unlockablesList.unlockables == null)
        {
            reason = "StartOfRound.Instance.unlockablesList was missing";
            return true;
        }

        if (player.currentSuitID < 0 || player.currentSuitID >= StartOfRound.Instance.unlockablesList.unlockables.Count)
        {
            reason = $"currentSuitID {player.currentSuitID} outside unlockables count {StartOfRound.Instance.unlockablesList.unlockables.Count}";
            return true;
        }

        UnlockableItem unlockable = StartOfRound.Instance.unlockablesList.unlockables[player.currentSuitID];
        if (unlockable == null)
        {
            reason = $"unlockables[{player.currentSuitID}] was null";
            return true;
        }

        if (unlockable.jumpAudio == null)
        {
            reason = fallbackClip != null
                ? $"unlockables[{player.currentSuitID}].jumpAudio was null; using StartOfRound.playerJumpSFX"
                : $"unlockables[{player.currentSuitID}].jumpAudio was null and StartOfRound.playerJumpSFX was missing";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static AudioClip TryGetDefaultJumpAudio()
    {
        return StartOfRound.Instance != null ? StartOfRound.Instance.playerJumpSFX : null;
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
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();
    private static readonly WarningLimiter NonExecuteStageWarnings = new(maxWarnings: 1);

    private static Exception Finalizer(PlayerControllerB __instance, Exception __exception)
    {
        if (__exception is not ArgumentOutOfRangeException && __exception is not NullReferenceException)
        {
            return __exception;
        }

        if (!RpcExecStageUtility.ShouldAllowClientRpcSuppression(__instance, "PlayerControllerB.PlayerJumpedClientRpc", __exception, NonExecuteStageWarnings))
        {
            return __exception;
        }

        if (IsKnownSafeJumpRpcException(__instance, out string reason))
        {
            Warnings.Warn($"PlayerJumpedClientRpc|{reason}", $"Suppressed PlayerControllerB.PlayerJumpedClientRpc {__exception.GetType().Name} after known unsafe jump dependencies were detected: {reason}.");
            return null;
        }

        UnknownWarnings.Warn($"PlayerJumpedClientRpc|{__exception.GetType().Name}", () => $"Unhandled PlayerControllerB.PlayerJumpedClientRpc {__exception.GetType().Name}; returning original exception. First stack fingerprint: {Fingerprint(__exception)}. First detail: {__exception}");
        return __exception;
    }

    private static bool IsKnownSafeJumpRpcException(PlayerControllerB player, out string reason)
    {
        if (player != null && player.IsOwner)
        {
            reason = "owner path does not call jump audio";
            return false;
        }

        if (StartOfRound.Instance == null)
        {
            reason = "StartOfRound.Instance was missing";
            return true;
        }

        if (StartOfRound.Instance.PlayerJumpEvent == null)
        {
            reason = "StartOfRound.PlayerJumpEvent was missing";
            return true;
        }

        return PlayerControllerBPlayJumpAudioPatch.TryGetUnsafeJumpAudioFallback(player, out _, out reason);
    }

    private static string Fingerprint(Exception exception)
    {
        string stackTrace = exception?.StackTrace;
        if (!string.IsNullOrEmpty(stackTrace))
        {
            string[] stackLines = stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            if (stackLines.Length > 0)
            {
                return stackLines[0];
            }
        }

        return exception?.GetType().Name ?? "unknown";
    }
}

[HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
internal static class PlayerControllerBThrowObjectClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();

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

        if (!RpcExecStageUtility.TryIsExecuting(__instance, out bool isExecuting) || !isExecuting)
        {
            return true;
        }

        object oldRpcStage = null;
        bool rpcStageChanged = false;
        bool stateMutated = false;
        try
        {
            if (!ShouldHandleThrowObjectClientRpc(__instance, grabbedObject, out string unsafeReason))
            {
                return true;
            }

            RpcExecStageUtility.TryGetStage(__instance, out oldRpcStage);
            Warnings.Warn($"guarded-throw|{GetPlayerId(__instance)}|{unsafeReason}", $"Using guarded ThrowObjectClientRpc path for player #{GetPlayerId(__instance)} because {unsafeReason}.");
            HandleThrowObjectClientRpcSafely(__instance, droppedInElevator, droppedInShipRoom, targetFloorPosition, grabbedObject, floorYRot, ref rpcStageChanged, ref stateMutated);
        }
        catch (Exception ex)
        {
            if (stateMutated)
            {
                // Vanilla sets __rpc_exec_stage back to Send before mutating held-object state.
                // After partial mutation, do not restore Execute or re-enter vanilla; doing so can discard the same object twice.
                SetRpcExecStageSend(__instance);
                TryFinishThrowingIfOwner(__instance);
                Warnings.Warn("guard-failed-after-mutation", $"ThrowObjectClientRpc guard failed after partial state changes; skipped vanilla to avoid double-discard: {ex.GetType().Name}.");
                return false;
            }

            if (rpcStageChanged)
            {
                // Only restore the previous Execute stage when no state changed and vanilla will run normally.
                RpcExecStageUtility.TrySetStage(__instance, oldRpcStage);
            }

            Warnings.Warn("guard-failed-before-mutation", $"ThrowObjectClientRpc guard failed before changing held object state and allowed vanilla to run: {ex.GetType().Name}.");
            return true;
        }

        // V81 generated code sets __rpc_exec_stage to Execute in __rpc_handler_3943098567,
        // calls ThrowObjectClientRpc, then sets it to Send after a normal return. The generated
        // ThrowObjectClientRpc body also sets Send before mutating held-object state, so this
        // replacement intentionally leaves Send when it returns false after handling the discard.
        return false;
    }

    private static bool ShouldHandleThrowObjectClientRpc(PlayerControllerB player, NetworkObjectReference grabbedObject, out string reason)
    {
        reason = string.Empty;
        if (player == null || player.NetworkManager == null || !player.NetworkManager.IsListening || (!player.NetworkManager.IsClient && !player.NetworkManager.IsHost))
        {
            return false;
        }

        if (!grabbedObject.TryGet(out NetworkObject networkObject) || networkObject == null)
        {
            reason = "grabbed NetworkObject reference could not be resolved";
            return true;
        }

        GrabbableObject grabbableObject = networkObject.GetComponent<GrabbableObject>();
        if (grabbableObject == null)
        {
            reason = "NetworkObject had no GrabbableObject";
            return true;
        }

        if (grabbableObject.itemProperties == null)
        {
            reason = "GrabbableObject.itemProperties was missing";
            return true;
        }

        if (!player.IsOwner && HasUnsafeRemoteThrowDependencies(player, grabbableObject, out reason))
        {
            return true;
        }

        if (grabbableObject != player.currentlyHeldObjectServer)
        {
            reason = "grabbed object did not match currentlyHeldObjectServer";
            return true;
        }

        return false;
    }

    private static bool HasUnsafeRemoteThrowDependencies(PlayerControllerB player, GrabbableObject grabbableObject, out string reason)
    {
        if (player.ItemSlots == null)
        {
            reason = "player ItemSlots were missing";
            return true;
        }

        if (player.playersManager == null)
        {
            reason = "player manager was missing";
            return true;
        }

        if (player.playersManager.elevatorTransform == null || player.playersManager.propsContainer == null)
        {
            reason = "drop parent transforms were missing";
            return true;
        }

        if (grabbableObject == null || grabbableObject.transform == null)
        {
            reason = "drop object transform was missing";
            return true;
        }

        reason = string.Empty;
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
                FinishThrowingIfOwner(player);
                return;
            }

            if (grabbableObject != player.currentlyHeldObjectServer)
            {
                Warnings.Warn($"held-mismatch|{player.playerClientId}", $"Suppressed ThrowObjectClientRpc held-object mismatch for player #{player.playerClientId}; currentlyHeldObjectServer was {GetHeldObjectName(player.currentlyHeldObjectServer)}.");
                FinishThrowingIfOwner(player);
                return;
            }

            string skipRemoteDropReason = string.Empty;
            if (!player.IsOwner && grabbableObject.itemProperties != null && !HasUnsafeRemoteThrowDependencies(player, grabbableObject, out skipRemoteDropReason))
            {
                stateMutated = true;
                player.SetObjectAsNoLongerHeld(droppedInElevator, droppedInShipRoom, targetFloorPosition, grabbableObject, floorYRot);
            }
            else if (!player.IsOwner)
            {
                skipRemoteDropReason = grabbableObject.itemProperties == null ? "itemProperties was missing" : skipRemoteDropReason;
                Warnings.Warn($"unsafe-remote-drop|{player.playerClientId}|{skipRemoteDropReason}", $"Skipped remote SetObjectAsNoLongerHeld in ThrowObjectClientRpc for player #{player.playerClientId} because {skipRemoteDropReason}.");
            }

            if (grabbableObject.itemProperties == null || !grabbableObject.itemProperties.syncDiscardFunction)
            {
                stateMutated = true;
                grabbableObject.playerHeldBy = null;
            }

            stateMutated = true;
            player.currentlyHeldObjectServer = null;
        }
        else
        {
            Warnings.Warn($"missing-network-object|{player.playerClientId}", $"Suppressed ThrowObjectClientRpc because the server object reference was missing for player #{player.playerClientId}.");
        }

        stateMutated = true;
        FinishThrowingIfOwner(player);
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
        if (RpcExecStageUtility.TryGetSendStage(out object sendStage))
        {
            RpcExecStageUtility.TrySetStage(player, sendStage);
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
        if (player != null)
        {
            player.throwingObject = value;
        }
    }

}

[HarmonyPatch(typeof(PlayerControllerB), "GrabObjectClientRpc")]
internal static class PlayerControllerBGrabObjectClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();
    private static readonly WarningLimiter NonExecuteStageWarnings = new(maxWarnings: 1);

    private static Exception Finalizer(PlayerControllerB __instance, bool grabValidated, NetworkObjectReference grabbedObject, Exception __exception)
    {
        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        if (!RpcExecStageUtility.ShouldAllowClientRpcSuppression(__instance, "PlayerControllerB.GrabObjectClientRpc", __exception, NonExecuteStageWarnings))
        {
            return __exception;
        }

        if (IsKnownSafeGrabNre(__instance, grabValidated, grabbedObject, out string reason))
        {
            Warnings.Warn($"GrabObjectClientRpc|{GetPlayerId(__instance)}|{reason}", () => $"Suppressed PlayerControllerB.GrabObjectClientRpc NullReferenceException for player #{GetPlayerId(__instance)} after known unsafe grab dependencies were detected: {reason}. First stack fingerprint: {Fingerprint(__exception)}. First detail: {__exception}");
            return null;
        }

        UnknownWarnings.Warn($"GrabObjectClientRpc|{GetPlayerId(__instance)}|{__exception.GetType().Name}", () => $"Unhandled PlayerControllerB.GrabObjectClientRpc NullReferenceException for player #{GetPlayerId(__instance)}; returning original exception. First stack fingerprint: {Fingerprint(__exception)}. First detail: {__exception}");
        return __exception;
    }

    private static bool IsKnownSafeGrabNre(PlayerControllerB player, bool grabValidated, NetworkObjectReference grabbedObject, out string reason)
    {
        if (player == null)
        {
            reason = "player was null";
            return true;
        }

        if (!grabValidated)
        {
            reason = "grab was not validated";
            return false;
        }

        if (!grabbedObject.TryGet(out NetworkObject networkObject) || networkObject == null)
        {
            reason = "grabbed NetworkObject reference could not be resolved";
            return true;
        }

        GrabbableObject grabbableObject = networkObject.gameObject != null
            ? networkObject.gameObject.GetComponentInChildren<GrabbableObject>()
            : networkObject.GetComponentInChildren<GrabbableObject>();
        if (grabbableObject == null)
        {
            reason = "grabbed NetworkObject had no GrabbableObject";
            return true;
        }

        if (player.currentlyHeldObjectServer == null)
        {
            reason = "currentlyHeldObjectServer was null after grab validation";
            return true;
        }

        if (player.currentlyHeldObjectServer.itemProperties == null)
        {
            reason = "currentlyHeldObjectServer.itemProperties was null";
            return true;
        }

        if (player.playersManager == null || player.playersManager.propsContainer == null)
        {
            reason = "player props container was missing";
            return true;
        }

        if (StartOfRound.Instance == null || StartOfRound.Instance.elevatorTransform == null)
        {
            reason = "StartOfRound elevator dependencies were missing";
            return true;
        }

        if (!player.IsOwner && player.serverItemHolder == null)
        {
            reason = "remote player serverItemHolder was missing";
            return true;
        }

        if (!player.IsOwner && player.itemAudio == null)
        {
            reason = "remote player itemAudio was missing";
            return true;
        }

        if (player.IsOwner && player.currentItemSlot == 50 && (HUDManager.Instance == null || HUDManager.Instance.itemOnlySlotIconFrame == null))
        {
            reason = "utility belt tutorial HUD dependencies were missing";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static string GetPlayerId(PlayerControllerB player)
    {
        return player != null ? player.playerClientId.ToString() : "unknown";
    }

    private static string Fingerprint(Exception exception)
    {
        string stackTrace = exception?.StackTrace;
        if (!string.IsNullOrEmpty(stackTrace))
        {
            string[] stackLines = stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            if (stackLines.Length > 0)
            {
                return stackLines[0];
            }
        }

        return exception?.GetType().Name ?? "unknown";
    }
}
