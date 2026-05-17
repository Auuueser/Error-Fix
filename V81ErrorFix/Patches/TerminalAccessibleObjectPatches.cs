using System;
using System.Collections.Generic;
using HarmonyLib;
using GameNetcodeStuff;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch(typeof(TerminalAccessibleObject), "Update")]
internal static class TerminalAccessibleObjectUpdatePatch
{
    private const float InitializeRetryCooldownSeconds = 0.5f;
    private static readonly WarningLimiter Warnings = new();
    private static readonly Dictionary<int, float> NextInitializeAttemptTimes = new();

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
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

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
            if (!CanAttemptInitialize(terminalObject))
            {
                return false;
            }

            if (StartOfRound.Instance.objectCodePrefab == null || StartOfRound.Instance.mapScreen.mapCameraStationaryUI == null)
            {
                return false;
            }

            terminalObject.InitializeValues();
            bool initialized = terminalObject.mapRadarObject != null;
            if (!initialized)
            {
                DelayInitializeRetry(terminalObject);
            }

            return initialized;
        }
        catch (Exception ex)
        {
            bool shouldContinue = ShouldContinueAfterInitializeFailure(ex, () =>
                StartOfRound.Instance == null ||
                StartOfRound.Instance.objectCodePrefab == null ||
                StartOfRound.Instance.mapScreen == null ||
                StartOfRound.Instance.mapScreen.mapCameraStationaryUI == null ||
                terminalObject.mapRadarObject == null);
            if (!shouldContinue)
            {
                DelayInitializeRetry(terminalObject);
            }

            return shouldContinue;
        }
    }

    internal static void ClearCache()
    {
        NextInitializeAttemptTimes.Clear();
    }

    private static bool CanAttemptInitialize(TerminalAccessibleObject terminalObject)
    {
        if (terminalObject == null)
        {
            return false;
        }

        int instanceId = terminalObject.GetInstanceID();
        return !NextInitializeAttemptTimes.TryGetValue(instanceId, out float nextAttemptTime) ||
            Time.realtimeSinceStartup >= nextAttemptTime;
    }

    private static void DelayInitializeRetry(TerminalAccessibleObject terminalObject)
    {
        if (terminalObject != null)
        {
            NextInitializeAttemptTimes[terminalObject.GetInstanceID()] = Time.realtimeSinceStartup + InitializeRetryCooldownSeconds;
        }
    }

    internal static bool ShouldContinueAfterInitializeFailure(Exception exception, Func<bool> isKnownSafeCase)
    {
        Exception unsuppressed = NullRefGuard.Suppress(exception, "TerminalAccessibleObject.InitializeValues", isKnownSafeCase);
        if (unsuppressed != null)
        {
            Warnings.Warn(GetInitializeFailureWarningKey(exception), $"Returning original TerminalAccessibleObject.InitializeValues {exception?.GetType().Name ?? "null exception"} because it did not match a known safe null-reference initialization failure.");
            throw unsuppressed;
        }

        return false;
    }

    internal static string GetInitializeFailureWarningKey(Exception exception)
    {
        return $"initialize-values-unknown|{exception?.GetType().Name ?? "null"}";
    }
}
