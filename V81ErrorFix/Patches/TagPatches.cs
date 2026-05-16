using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

internal static class PlayerRagdollGlobalTagGuard
{
    internal static bool ShouldPatch()
    {
        return PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.PlayerRagdollGlobalTagGuardMode);
    }
}

[HarmonyPatch]
internal static class PlayerRagdollCompareTagGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PlayerRagdollGlobalTagGuard.ShouldPatch();
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GameObject), "CompareTag", new[] { typeof(string) });
        yield return AccessTools.Method(typeof(Component), "CompareTag", new[] { typeof(string) });
    }

    private static Exception Finalizer(Exception __exception, string __0, ref bool __result)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!PlayerRagdollTagUtility.IsPlayerRagdollTag(__0) || !PlayerRagdollTagUtility.IsUndefinedPlayerRagdollTagException(__exception, __0))
        {
            return __exception;
        }

        __result = false;
        Warnings.Warn(__0, $"Suppressed undefined tag comparison for '{__0}'.");
        return null;
    }
}

[HarmonyPatch(typeof(GameObject), "FindWithTag")]
internal static class PlayerRagdollFindWithTagGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PlayerRagdollGlobalTagGuard.ShouldPatch();
    }

    private static Exception Finalizer(string tag, Exception __exception, ref GameObject __result)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!PlayerRagdollTagUtility.IsPlayerRagdollTag(tag) || !PlayerRagdollTagUtility.IsUndefinedPlayerRagdollTagException(__exception, tag))
        {
            return __exception;
        }

        __result = null;
        Warnings.Warn(tag, $"Suppressed undefined tag lookup GameObject.FindWithTag('{tag}') and returned null.");
        return null;
    }
}

[HarmonyPatch(typeof(GameObject), "FindGameObjectWithTag")]
internal static class PlayerRagdollFindGameObjectWithTagGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PlayerRagdollGlobalTagGuard.ShouldPatch();
    }

    private static Exception Finalizer(string tag, Exception __exception, ref GameObject __result)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!PlayerRagdollTagUtility.IsPlayerRagdollTag(tag) || !PlayerRagdollTagUtility.IsUndefinedPlayerRagdollTagException(__exception, tag))
        {
            return __exception;
        }

        __result = null;
        Warnings.Warn(tag, $"Suppressed undefined tag lookup GameObject.FindGameObjectWithTag('{tag}') and returned null.");
        return null;
    }
}

[HarmonyPatch(typeof(GameObject), "FindGameObjectsWithTag")]
internal static class PlayerRagdollFindGameObjectsWithTagGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PlayerRagdollGlobalTagGuard.ShouldPatch();
    }

    private static Exception Finalizer(string tag, Exception __exception, ref GameObject[] __result)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!PlayerRagdollTagUtility.IsPlayerRagdollTag(tag) || !PlayerRagdollTagUtility.IsUndefinedPlayerRagdollTagException(__exception, tag))
        {
            return __exception;
        }

        __result = Array.Empty<GameObject>();
        Warnings.Warn(tag, $"Suppressed undefined tag lookup GameObject.FindGameObjectsWithTag('{tag}') and returned an empty array.");
        return null;
    }
}

[HarmonyPatch(typeof(DeadBodyInfo), "Start")]
internal static class DeadBodyInfoPlayerRagdollTagGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(DeadBodyInfo __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is ArgumentOutOfRangeException)
        {
            if (IsKnownPlayerRagdollIndexFailure(__instance))
            {
                TryApplyFallbackRagdollTags(__instance);
                Warn($"Suppressed DeadBodyInfo.Start ArgumentOutOfRangeException for known ragdoll/player index path: {GetRagdollContext(__instance)}.");
                return null;
            }

            return __exception;
        }

        if (!PlayerRagdollTagUtility.IsUndefinedPlayerRagdollTagException(__exception))
        {
            return __exception;
        }

        TryApplyFallbackRagdollTags(__instance);
        Warn("Suppressed undefined PlayerRagdoll tag assignment and fell back to 'PlayerRagdoll'.");
        return null;
    }

    private static void TryApplyFallbackRagdollTags(DeadBodyInfo deadBody)
    {
        if (deadBody == null || deadBody.bodyParts == null)
        {
            return;
        }

        for (int i = 0; i < deadBody.bodyParts.Length; i++)
        {
            Rigidbody bodyPart = deadBody.bodyParts[i];
            if (bodyPart == null || bodyPart.gameObject == null)
            {
                continue;
            }

            try
            {
                bodyPart.gameObject.tag = "PlayerRagdoll";
            }
            catch
            {
                return;
            }
        }
    }

    private static void Warn(string message)
    {
        Warnings.Warn("DeadBodyInfo.Start", message);
    }

    private static bool IsKnownPlayerRagdollIndexFailure(DeadBodyInfo deadBody)
    {
        if (deadBody == null || deadBody.bodyParts == null || deadBody.bodyParts.Length == 0)
        {
            return false;
        }

        int playerObjectId = GetPlayerObjectId(deadBody);
        int bodyPartsLength = GetBodyPartsLength(deadBody);
        int allPlayerScriptsLength = GetAllPlayerScriptsLength();
        bool playerScriptMissing = deadBody.playerScript == null;
        bool missingNumericRagdollTag = playerObjectId >= 4 && !PlayerRagdollTagUtility.TagExists($"PlayerRagdoll{playerObjectId}");

        return IsKnownPlayerRagdollIndexFailure(playerObjectId, bodyPartsLength, allPlayerScriptsLength, playerScriptMissing, missingNumericRagdollTag);
    }

    private static int GetPlayerObjectId(DeadBodyInfo deadBody)
    {
        return deadBody != null ? deadBody.playerObjectId : -1;
    }

    internal static bool IsKnownPlayerRagdollIndexFailure(int playerObjectId, int bodyPartsLength, int allPlayerScriptsLength, bool playerScriptMissing, bool numericRagdollTagMissing)
    {
        if (playerObjectId < 0 || bodyPartsLength <= 0 || allPlayerScriptsLength < 0)
        {
            return false;
        }

        bool invalidPlayerIndex = playerObjectId >= allPlayerScriptsLength;
        if (!invalidPlayerIndex)
        {
            return false;
        }

        return playerScriptMissing || numericRagdollTagMissing || playerObjectId >= 4;
    }

    private static int GetBodyPartsLength(DeadBodyInfo deadBody)
    {
        return deadBody != null && deadBody.bodyParts != null ? deadBody.bodyParts.Length : -1;
    }

    private static int GetAllPlayerScriptsLength()
    {
        return StartOfRound.Instance != null && StartOfRound.Instance.allPlayerScripts != null ? StartOfRound.Instance.allPlayerScripts.Length : -1;
    }

    private static string GetRagdollContext(DeadBodyInfo deadBody)
    {
        return $"playerObjectId={GetPlayerObjectId(deadBody)}, bodyParts.Length={GetBodyPartsLength(deadBody)}, allPlayerScripts.Length={GetAllPlayerScriptsLength()}, playerScriptMissing={deadBody?.playerScript == null}";
    }
}

[HarmonyPatch(typeof(GameObject), "tag", MethodType.Setter)]
internal static class GameObjectPlayerRagdollTagSetterGuardPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PlayerRagdollGlobalTagGuard.ShouldPatch();
    }

    private static Exception Finalizer(GameObject __instance, string value, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (PlayerRagdollTagUtility.IsTagExistenceProbe)
        {
            return __exception;
        }

        if (!PlayerRagdollTagUtility.IsPlayerRagdollTag(value) || !PlayerRagdollTagUtility.IsUndefinedPlayerRagdollTagException(__exception, value))
        {
            return __exception;
        }

        if (PlayerRagdollTagUtility.TagExists(value))
        {
            return __exception;
        }

        TrySetFallbackTag(__instance, value);
        return null;
    }

    private static void TrySetFallbackTag(GameObject gameObject, string originalTag)
    {
        if (gameObject == null)
        {
            return;
        }

        try
        {
            gameObject.tag = "PlayerRagdoll";
            Warnings.Warn(originalTag, $"Replaced undefined tag '{originalTag}' with fallback tag 'PlayerRagdoll' on '{gameObject.name}'.");
        }
        catch
        {
            Warnings.Warn(originalTag, $"Suppressed undefined tag '{originalTag}', but fallback tag 'PlayerRagdoll' could not be assigned.");
        }
    }
}
