using System;
using HarmonyLib;
using GameNetcodeStuff;

namespace V81ErrorFix;

internal static class GameplayEnemyUpdatePatchGate
{
    internal static bool ShouldPatch(PatchEnableMode mode)
    {
        return mode == PatchEnableMode.Enabled;
    }

    internal static bool ShouldPatch(PatchEnableMode mode, bool isVerifiedAssembly)
    {
        return mode == PatchEnableMode.Enabled;
    }

    internal static bool ShouldPatchConfigured()
    {
        return ShouldPatch(ErrorFixConfig.GameplayEnemyUpdateGuardMode?.Value ?? PatchEnableMode.Disabled);
    }
}

[HarmonyPatch(typeof(DocileLocustBeesAI), "Update")]
internal static class DocileLocustBeesAIUpdatePatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return GameplayEnemyUpdatePatchGate.ShouldPatchConfigured();
    }

    private static bool Prefix(DocileLocustBeesAI __instance)
    {
        if (__instance == null || StartOfRound.Instance == null || StartOfRound.Instance.activeCamera == null || __instance.bugsEffect == null || __instance.creatureVoice == null || __instance.scanNode == null)
        {
            return false;
        }

        if (__instance.currentBehaviourStateIndex == 1 && (__instance.creatureSFX == null || __instance.enemyType == null || __instance.enemyType.audioClips == null || __instance.enemyType.audioClips.Length == 0 || RoundManager.Instance == null))
        {
            return false;
        }

        return true;
    }

    private static Exception Finalizer(DocileLocustBeesAI __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "DocileLocustBeesAI.Update", () =>
            __instance == null ||
            StartOfRound.Instance == null ||
            StartOfRound.Instance.activeCamera == null ||
            __instance.bugsEffect == null ||
            __instance.creatureVoice == null ||
            __instance.scanNode == null ||
            (__instance.currentBehaviourStateIndex == 1 && (__instance.creatureSFX == null || __instance.enemyType == null || __instance.enemyType.audioClips == null || __instance.enemyType.audioClips.Length == 0 || RoundManager.Instance == null)));
    }
}

[HarmonyPatch(typeof(CrawlerAI), "Update")]
internal static class CrawlerAIUpdatePatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return GameplayEnemyUpdatePatchGate.ShouldPatchConfigured();
    }

    private static bool Prefix(CrawlerAI __instance)
    {
        if (__instance == null || StartOfRound.Instance == null || GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null)
        {
            return false;
        }

        if (__instance.agent == null || __instance.creatureAnimator == null || __instance.transform == null || __instance.searchForPlayers == null)
        {
            return false;
        }

        return true;
    }

    private static Exception Finalizer(CrawlerAI __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        if (__instance != null)
        {
            try
            {
                __instance.movingTowardsTargetPlayer = false;
            }
            catch
            {
            }
        }

        return NullRefGuard.Suppress(__exception, "CrawlerAI.Update", () =>
            __instance == null ||
            StartOfRound.Instance == null ||
            GameNetworkManager.Instance == null ||
            GameNetworkManager.Instance.localPlayerController == null ||
            __instance.agent == null ||
            __instance.creatureAnimator == null ||
            __instance.transform == null ||
            __instance.searchForPlayers == null);
    }
}
