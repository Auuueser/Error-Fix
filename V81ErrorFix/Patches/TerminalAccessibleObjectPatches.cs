using System;
using HarmonyLib;
using GameNetcodeStuff;

namespace V81ErrorFix;

[HarmonyPatch(typeof(TerminalAccessibleObject), "Update")]
internal static class TerminalAccessibleObjectUpdatePatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(TerminalAccessibleObject __instance)
    {
        if (__instance == null || GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null || StartOfRound.Instance == null || StartOfRound.Instance.mapScreen == null)
        {
            return false;
        }

        if (StartOfRound.Instance.mapScreen.mapCamera == null)
        {
            return false;
        }

        return __instance.mapRadarObject != null || TryInitializeValues(__instance);
    }

    private static Exception Finalizer(TerminalAccessibleObject __instance, Exception __exception)
    {
        return NullRefGuard.Suppress(__exception, "TerminalAccessibleObject.Update", () =>
            __instance == null ||
            GameNetworkManager.Instance == null ||
            GameNetworkManager.Instance.localPlayerController == null ||
            StartOfRound.Instance == null ||
            StartOfRound.Instance.mapScreen == null ||
            StartOfRound.Instance.mapScreen.mapCamera == null ||
            __instance.mapRadarObject == null);
    }

    private static bool TryInitializeValues(TerminalAccessibleObject terminalObject)
    {
        try
        {
            if (StartOfRound.Instance.objectCodePrefab == null || StartOfRound.Instance.mapScreen.mapCameraStationaryUI == null)
            {
                return false;
            }

            terminalObject.InitializeValues();
            return terminalObject.mapRadarObject != null;
        }
        catch (Exception ex)
        {
            return ShouldContinueAfterInitializeFailure(ex, () =>
                StartOfRound.Instance == null ||
                StartOfRound.Instance.objectCodePrefab == null ||
                StartOfRound.Instance.mapScreen == null ||
                StartOfRound.Instance.mapScreen.mapCameraStationaryUI == null ||
                terminalObject.mapRadarObject == null);
        }
    }

    internal static bool ShouldContinueAfterInitializeFailure(Exception exception, Func<bool> isKnownSafeCase)
    {
        Exception unsuppressed = NullRefGuard.Suppress(exception, "TerminalAccessibleObject.InitializeValues", isKnownSafeCase);
        if (unsuppressed != null)
        {
            Warnings.Warn(GetInitializeFailureWarningKey(exception), $"Skipped TerminalAccessibleObject.Update after InitializeValues failed with an unexpected {exception?.GetType().Name ?? "null exception"}.");
        }

        return false;
    }

    internal static string GetInitializeFailureWarningKey(Exception exception)
    {
        return $"initialize-values-unknown|{exception?.GetType().Name ?? "null"}";
    }
}
