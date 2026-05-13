using System;
using System.Reflection;
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
        if (exception is NullReferenceException && isKnownSafeCase != null && isKnownSafeCase())
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

    private static Exception Finalizer(RadMechAI __instance, Exception __exception)
    {
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
    private static readonly FieldInfo PreviousPlayerHeldByField = AccessTools.Field(typeof(JetpackItem), "previousPlayerHeldBy");

    internal static bool HasKnownMissingDeactivateDependency(JetpackItem jetpack)
    {
        return jetpack == null ||
            GetPreviousPlayerHeldBy(jetpack) == null ||
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

    private static PlayerControllerB GetPreviousPlayerHeldBy(JetpackItem jetpack)
    {
        return jetpack != null && PreviousPlayerHeldByField != null ? PreviousPlayerHeldByField.GetValue(jetpack) as PlayerControllerB : null;
    }
}
