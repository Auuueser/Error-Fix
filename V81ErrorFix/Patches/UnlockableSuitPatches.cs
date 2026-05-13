using System;
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

    private static Exception Finalizer(int suitID, Exception __exception)
    {
        if (!UnlockableSuitUpdatePatch.IsPatchEnabled())
        {
            return __exception;
        }

        if (__exception is ArgumentOutOfRangeException || __exception is NullReferenceException)
        {
            Warnings.Warn($"exception|{suitID}|{__exception.GetType().Name}", $"Suppressed UnlockableSuit.SwitchSuitForPlayer {__exception.GetType().Name} for suit id {suitID}.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(UnlockableSuit), "SwitchSuitForAllPlayers")]
internal static class UnlockableSuitSwitchSuitForAllPlayersPatch
{
    private static readonly WarningLimiter Warnings = new();

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
            Warnings.Warn($"exception|{suitID}|{__exception.GetType().Name}", $"Suppressed UnlockableSuit.SwitchSuitForAllPlayers {__exception.GetType().Name} for suit id {suitID}.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesClientRpc")]
internal static class StartOfRoundSyncShipUnlockablesClientRpcSuitGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static void Prefix(
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
            return;
        }

        int playerCount = StartOfRound.Instance != null && StartOfRound.Instance.allPlayerScripts != null
            ? Math.Max(4, StartOfRound.Instance.allPlayerScripts.Length)
            : 4;
        int unlockableCount = StartOfRound.Instance != null && StartOfRound.Instance.unlockablesList != null && StartOfRound.Instance.unlockablesList.unlockables != null
            ? StartOfRound.Instance.unlockablesList.unlockables.Count
            : 0;

        playerSuitIDs = EnsureLength(playerSuitIDs, playerCount, 0, "playerSuitIDs");
        placeableObjectPositions = EnsureLength(placeableObjectPositions, unlockableCount, Vector3.zero, "placeableObjectPositions");
        placeableObjectRotations = EnsureLength(placeableObjectRotations, unlockableCount, Vector3.zero, "placeableObjectRotations");
        unlockedObjects = EnsureLength(unlockedObjects, unlockableCount, false, "unlockedObjects");
        storedItems ??= Array.Empty<int>();
        scrapValues ??= Array.Empty<int>();
        itemSaveData ??= Array.Empty<int>();

        for (int i = 0; i < playerSuitIDs.Length; i++)
        {
            int suitId = playerSuitIDs[i];
            if (UnlockableSuitUpdatePatch.IsValidSuitId(suitId))
            {
                continue;
            }

            Warnings.Warn($"invalid-player-suit|{i}|{suitId}", $"Replaced invalid synced suit id {suitId} for player slot {i} with suit id 0.");
            playerSuitIDs[i] = 0;
        }
    }

    private static T[] EnsureLength<T>(T[] values, int requiredLength, T defaultValue, string arrayName)
    {
        T[] resizedValues = ArrayUtility.EnsureLength(values, requiredLength, defaultValue);
        if (!ReferenceEquals(values, resizedValues))
        {
            Warnings.Warn($"resize|{arrayName}", $"Resized SyncShipUnlockablesClientRpc {arrayName} to {requiredLength} entries.");
        }

        return resizedValues;
    }
}
