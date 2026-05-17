using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;
using Unity.Netcode;

namespace V81ErrorFix;

[HarmonyPatch(typeof(UnlockableSuit), "Update")]
internal static class UnlockableSuitUpdatePatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(UnlockableSuit __instance)
    {
        if (!IsPatchEnabled())
        {
            return true;
        }

        if (__instance == null || NetworkManager.Singleton == null || NetworkManager.Singleton.ShutdownInProgress)
        {
            return false;
        }

        if (__instance.syncedSuitID == null)
        {
            Warnings.Warn("missing-syncedSuitID", "Skipped UnlockableSuit.Update because syncedSuitID was missing.");
            return false;
        }

        int syncedSuitId = __instance.syncedSuitID.Value;
        if (__instance.suitID == syncedSuitId)
        {
            return true;
        }

        if (!IsValidSuitId(syncedSuitId))
        {
            Warnings.Warn($"invalid-suit|{syncedSuitId}", $"Skipped UnlockableSuit.Update because synced suit id {syncedSuitId} was outside unlockables list.");
            return false;
        }

        if (__instance.suitRenderer == null)
        {
            Warnings.Warn($"missing-renderer|{syncedSuitId}", $"Skipped UnlockableSuit.Update for suit id {syncedSuitId} because suitRenderer was missing.");
            return false;
        }

        return true;
    }

    private static Exception Finalizer(UnlockableSuit __instance, Exception __exception)
    {
        if (!IsPatchEnabled())
        {
            return __exception;
        }

        if (__exception is ArgumentOutOfRangeException || __exception is NullReferenceException)
        {
            int suitId = __instance != null && __instance.syncedSuitID != null ? __instance.syncedSuitID.Value : -1;
            bool knownSafe = __instance == null ||
                __instance.syncedSuitID == null ||
                __instance.suitRenderer == null ||
                !IsValidSuitId(suitId);
            if (knownSafe)
            {
                Warnings.Warn($"exception|{suitId}|{__exception.GetType().Name}", $"Suppressed UnlockableSuit.Update {__exception.GetType().Name} for invalid or incomplete suit id {suitId}.");
                return null;
            }
        }

        return __exception;
    }

    internal static bool IsValidSuitId(int suitId)
    {
        return suitId >= 0
            && StartOfRound.Instance != null
            && StartOfRound.Instance.unlockablesList != null
            && StartOfRound.Instance.unlockablesList.unlockables != null
            && suitId < StartOfRound.Instance.unlockablesList.unlockables.Count
            && StartOfRound.Instance.unlockablesList.unlockables[suitId] != null;
    }

    internal static bool IsPatchEnabled()
    {
        return PatchModeUtility.IsEnabled(ErrorFixConfig.UnlockableSuitGuardMode);
    }
}

[HarmonyPatch(typeof(UnlockableSuit), "SwitchSuitForPlayer")]
internal static class UnlockableSuitSwitchSuitForPlayerPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    private static bool Prefix(PlayerControllerB player, int suitID)
    {
        if (!UnlockableSuitUpdatePatch.IsPatchEnabled())
        {
            return true;
        }

        if (player == null)
        {
            Warnings.Warn("null-player", "Skipped UnlockableSuit.SwitchSuitForPlayer because player was null.");
            return false;
        }

        if (!UnlockableSuitUpdatePatch.IsValidSuitId(suitID))
        {
            Warnings.Warn($"invalid-suit|{suitID}", $"Skipped UnlockableSuit.SwitchSuitForPlayer because suit id {suitID} was outside unlockables list.");
            return false;
        }

        return true;
    }

    private static Exception Finalizer(PlayerControllerB player, int suitID, Exception __exception)
    {
        if (!UnlockableSuitUpdatePatch.IsPatchEnabled())
        {
            return __exception;
        }

        if (__exception is ArgumentOutOfRangeException || __exception is NullReferenceException)
        {
            if (player == null || !UnlockableSuitUpdatePatch.IsValidSuitId(suitID))
            {
                Warnings.Warn($"exception|{suitID}|{__exception.GetType().Name}", $"Suppressed UnlockableSuit.SwitchSuitForPlayer {__exception.GetType().Name} for known invalid player or suit id {suitID}.");
                return null;
            }

            UnknownWarnings.Warn($"unknown-exception|{suitID}|{__exception.GetType().Name}", () => $"Unhandled UnlockableSuit.SwitchSuitForPlayer {__exception.GetType().Name} for valid suit id {suitID}; returning original exception. First detail: {__exception}");
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(UnlockableSuit), "SwitchSuitForAllPlayers")]
internal static class UnlockableSuitSwitchSuitForAllPlayersPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    private static bool Prefix(int suitID)
    {
        if (!UnlockableSuitUpdatePatch.IsPatchEnabled())
        {
            return true;
        }

        if (UnlockableSuitUpdatePatch.IsValidSuitId(suitID))
        {
            return true;
        }

        Warnings.Warn($"invalid-suit|{suitID}", $"Skipped UnlockableSuit.SwitchSuitForAllPlayers because suit id {suitID} was outside unlockables list.");
        return false;
    }

    private static Exception Finalizer(int suitID, Exception __exception)
    {
        if (!UnlockableSuitUpdatePatch.IsPatchEnabled())
        {
            return __exception;
        }

        if (__exception is ArgumentOutOfRangeException || __exception is NullReferenceException)
        {
            if (!UnlockableSuitUpdatePatch.IsValidSuitId(suitID))
            {
                Warnings.Warn($"exception|{suitID}|{__exception.GetType().Name}", $"Suppressed UnlockableSuit.SwitchSuitForAllPlayers {__exception.GetType().Name} for known invalid suit id {suitID}.");
                return null;
            }

            UnknownWarnings.Warn($"unknown-exception|{suitID}|{__exception.GetType().Name}", () => $"Unhandled UnlockableSuit.SwitchSuitForAllPlayers {__exception.GetType().Name} for valid suit id {suitID}; returning original exception. First detail: {__exception}");
        }

        return __exception;
    }
}

[HarmonyPatch]
internal static class StartOfRoundSpawnUnlockableSuitNetworkVariablePatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return UnlockableSuitUpdatePatch.IsPatchEnabled()
            && TargetMethod() != null
            && AccessTools.PropertySetter(typeof(NetworkVariable<int>), nameof(NetworkVariable<int>.Value)) != null
            && AccessTools.Method(typeof(NetworkVariable<int>), nameof(NetworkVariable<int>.Reset), new[] { typeof(int) }) != null
            && AccessTools.Field(typeof(UnlockableSuit), nameof(UnlockableSuit.syncedSuitID)) != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(StartOfRound), "SpawnUnlockable", new[] { typeof(int), typeof(bool) });
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo valueSetter = AccessTools.PropertySetter(typeof(NetworkVariable<int>), nameof(NetworkVariable<int>.Value));
        MethodInfo resetMethod = AccessTools.Method(typeof(NetworkVariable<int>), nameof(NetworkVariable<int>.Reset), new[] { typeof(int) });
        FieldInfo syncedSuitIdField = AccessTools.Field(typeof(UnlockableSuit), nameof(UnlockableSuit.syncedSuitID));
        List<CodeInstruction> codes = new(instructions);
        if (valueSetter == null || resetMethod == null || syncedSuitIdField == null)
        {
            Plugin.Log?.LogWarning("StartOfRound.SpawnUnlockable suit NetworkVariable patch was skipped because required Netcode or UnlockableSuit members were not found.");
            return codes;
        }

        List<int> replacementIndices = new();
        for (int i = 0; i < codes.Count; i++)
        {
            if (!codes[i].Calls(valueSetter) || !LoadsSyncedSuitId(codes, i, syncedSuitIdField))
            {
                continue;
            }

            replacementIndices.Add(i);
        }

        if (replacementIndices.Count != 1)
        {
            Plugin.Log?.LogWarning($"StartOfRound.SpawnUnlockable suit NetworkVariable patch expected one Value setter but found {replacementIndices.Count}; leaving generated method unchanged.");
            return codes;
        }

        // V81 initializes suit ids before NetworkObject.Spawn(). Netcode's Reset path is
        // intended for pre-spawn initial values and avoids dirtying an unbound behaviour.
        codes[replacementIndices[0]].opcode = OpCodes.Callvirt;
        codes[replacementIndices[0]].operand = resetMethod;
        Plugin.Log?.LogInfo("Patched StartOfRound.SpawnUnlockable suit NetworkVariable initialization to use Reset before NetworkObject.Spawn.");

        return codes;
    }

    private static bool LoadsSyncedSuitId(List<CodeInstruction> codes, int setterIndex, FieldInfo syncedSuitIdField)
    {
        return setterIndex >= 2
            && codes[setterIndex - 2].opcode == OpCodes.Ldfld
            && Equals(codes[setterIndex - 2].operand, syncedSuitIdField);
    }
}

[HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesClientRpc")]
internal static class StartOfRoundSyncShipUnlockablesClientRpcSuitGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(
        StartOfRound __instance,
        ref int[] playerSuitIDs,
        ref Vector3[] placeableObjectPositions,
        ref Vector3[] placeableObjectRotations,
        ref bool[] unlockedObjects,
        ref int[] storedItems,
        ref int[] scrapValues,
        ref int[] itemSaveData)
    {
        if (!UnlockableSuitUpdatePatch.IsPatchEnabled())
        {
            return true;
        }

        if (!IsExecutingClientRpc(__instance))
        {
            return true;
        }

        int playerCount = StartOfRound.Instance != null && StartOfRound.Instance.allPlayerScripts != null
            ? StartOfRound.Instance.allPlayerScripts.Length
            : 4;
        int unlockableCount = StartOfRound.Instance != null && StartOfRound.Instance.unlockablesList != null && StartOfRound.Instance.unlockablesList.unlockables != null
            ? StartOfRound.Instance.unlockablesList.unlockables.Count
            : 0;

        if (!HasRequiredLength(playerSuitIDs, playerCount, "playerSuitIDs") ||
            !HasRequiredLength(placeableObjectPositions, unlockableCount, "placeableObjectPositions") ||
            !HasRequiredLength(placeableObjectRotations, unlockableCount, "placeableObjectRotations") ||
            !HasRequiredLength(unlockedObjects, unlockableCount, "unlockedObjects"))
        {
            return false;
        }

        int slotsToValidate = Math.Min(playerSuitIDs.Length, playerCount);
        int unlockablesCount = GetUnlockablesCount();
        for (int i = 0; i < slotsToValidate; i++)
        {
            int suitId = playerSuitIDs[i];
            if (UnlockableSuitUpdatePatch.IsValidSuitId(suitId))
            {
                continue;
            }

            if (!UnlockableSuitUpdatePatch.IsValidSuitId(0))
            {
                Warnings.Warn($"invalid-player-suit-no-fallback|{i}|{suitId}", $"Skipped SyncShipUnlockablesClientRpc because player slot {i} had invalid suit id {suitId} and fallback suit id 0 was unavailable; unlockables count was {unlockablesCount}.");
                return false;
            }

            Warnings.Warn($"invalid-player-suit|{i}|{suitId}", $"Replaced invalid synced suit id {suitId} for player slot {i} with suit id 0; unlockables count was {unlockablesCount}.");
            playerSuitIDs[i] = 0;
        }

        return true;
    }

    private static bool HasRequiredLength<T>(T[] values, int requiredLength, string arrayName)
    {
        if (requiredLength <= 0)
        {
            return true;
        }

        if (values != null && values.Length >= requiredLength)
        {
            return true;
        }

        int actualLength = values != null ? values.Length : -1;
        Warnings.Warn($"malformed-sync-array|{arrayName}|{actualLength}|{requiredLength}", $"Skipped SyncShipUnlockablesClientRpc because {arrayName} length {actualLength} was shorter than required length {requiredLength}; preserving original data instead of default-expanding unknown RPC payloads.");
        return false;
    }

    private static int GetUnlockablesCount()
    {
        return StartOfRound.Instance != null && StartOfRound.Instance.unlockablesList != null && StartOfRound.Instance.unlockablesList.unlockables != null
            ? StartOfRound.Instance.unlockablesList.unlockables.Count
            : 0;
    }

    private static bool IsExecutingClientRpc(StartOfRound startOfRound)
    {
        return RpcExecStageUtility.TryIsExecuting(startOfRound, out bool isExecuting) && isExecuting;
    }
}
