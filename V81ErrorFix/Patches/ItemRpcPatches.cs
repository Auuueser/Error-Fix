using System;
using HarmonyLib;
using GameNetcodeStuff;

namespace V81ErrorFix;

[HarmonyPatch(typeof(RadMechAI), "SetExplosion")]
internal static class RadMechAISetExplosionPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(RadMechAI __instance, Exception __exception)
    {
        return SuppressKnownException(__exception, "RadMechAI.SetExplosion", Warnings, () => HasKnownMissingExplosionDependency(__instance));
    }

    internal static Exception SuppressKnownException(Exception exception, string key, WarningLimiter warnings, Func<bool> isKnownSafeCase)
    {
        if (exception is not NullReferenceException || isKnownSafeCase == null)
        {
            return exception;
        }

        bool knownSafe;
        try
        {
            knownSafe = isKnownSafeCase();
        }
        catch (Exception ex)
        {
            warnings.Warn($"{key}|classifier-failed", $"Known dependency classifier failed for {key}; returning original NullReferenceException: {ex.GetType().Name}.");
            return exception;
        }

        if (knownSafe)
        {
            warnings.Warn(key, $"Suppressed {key} NullReferenceException for a known missing dependency.");
            return null;
        }

        return exception;
    }

    internal static bool HasKnownMissingExplosionDependency(RadMechAI mech)
    {
        return mech == null ||
            GameNetworkManager.Instance == null ||
            GameNetworkManager.Instance.localPlayerController == null ||
            GameNetworkManager.Instance.localPlayerController.transform == null ||
            StartOfRound.Instance == null ||
            mech.explosionAudio == null ||
            mech.largeExplosionSFX == null;
    }
}

[HarmonyPatch(typeof(RadMechAI), "SetExplosionClientRpc")]
internal static class RadMechAISetExplosionClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter NonExecuteStageWarnings = new(maxWarnings: 1);

    private static Exception Finalizer(RadMechAI __instance, Exception __exception)
    {
        if (__exception is NullReferenceException &&
            !RpcExecStageUtility.ShouldAllowClientRpcSuppression(__instance, "RadMechAI.SetExplosionClientRpc", __exception, NonExecuteStageWarnings))
        {
            return __exception;
        }

        return RadMechAISetExplosionPatch.SuppressKnownException(__exception, "RadMechAI.SetExplosionClientRpc", Warnings, () => RadMechAISetExplosionPatch.HasKnownMissingExplosionDependency(__instance));
    }
}

[HarmonyPatch(typeof(JetpackItem), "DeactivateJetpack")]
internal static class JetpackItemDeactivateJetpackPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(JetpackItem __instance, Exception __exception)
    {
        return RadMechAISetExplosionPatch.SuppressKnownException(__exception, "JetpackItem.DeactivateJetpack", Warnings, () => JetpackItemKnownDependencyGuard.HasKnownMissingDeactivateDependency(__instance));
    }
}

[HarmonyPatch(typeof(JetpackItem), "ItemActivate")]
internal static class JetpackItemItemActivatePatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(JetpackItem __instance, Exception __exception)
    {
        return RadMechAISetExplosionPatch.SuppressKnownException(__exception, "JetpackItem.ItemActivate", Warnings, () => JetpackItemKnownDependencyGuard.HasKnownMissingActivateDependency(__instance));
    }
}

[HarmonyPatch(typeof(GrabbableObject), "ActivateItemRpc")]
internal static class GrabbableObjectActivateItemRpcPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(GrabbableObject __instance, Exception __exception)
    {
        if (__instance is not JetpackItem jetpack)
        {
            return __exception;
        }

        return RadMechAISetExplosionPatch.SuppressKnownException(__exception, "GrabbableObject.ActivateItemRpc", Warnings, () => JetpackItemKnownDependencyGuard.HasKnownMissingActivateDependency(jetpack));
    }
}

internal static class JetpackItemKnownDependencyGuard
{
    internal static bool HasKnownMissingDeactivateDependency(JetpackItem jetpack)
    {
        return jetpack == null ||
            jetpack.previousPlayerHeldBy == null ||
            jetpack.jetpackBeepsAudio == null ||
            jetpack.jetpackAudio == null ||
            jetpack.smokeTrailParticle == null;
    }

    internal static bool HasKnownMissingActivateDependency(JetpackItem jetpack)
    {
        return jetpack == null ||
            jetpack.playerHeldBy == null ||
            jetpack.jetpackAudio == null ||
            jetpack.smokeTrailParticle == null ||
            (jetpack.streamlineJetpack && (StartOfRound.Instance == null || GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null));
    }
}
