using System;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch(typeof(GrabbableObjectPhysicsTrigger), "OnTriggerEnter")]
internal static class GrabbableObjectPhysicsTriggerOnTriggerEnterPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    private static bool Prefix(GrabbableObjectPhysicsTrigger __instance, Collider other)
    {
        if (__instance?.itemScript != null && other?.gameObject != null)
        {
            return true;
        }

        Warnings.Warn("missing-trigger-dependency", "Skipped GrabbableObjectPhysicsTrigger.OnTriggerEnter because itemScript or collider was missing.");
        return false;
    }

    private static Exception Finalizer(GrabbableObjectPhysicsTrigger __instance, Collider other, Exception __exception)
    {
        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        if (__instance?.itemScript == null || other?.gameObject == null)
        {
            Warnings.Warn("nre|missing-trigger-dependency", "Suppressed GrabbableObjectPhysicsTrigger.OnTriggerEnter NullReferenceException after known missing itemScript or collider dependencies were detected.");
            return null;
        }

        UnknownWarnings.Warn($"nre|unknown|{__exception.GetType().Name}", () => $"Unhandled GrabbableObjectPhysicsTrigger.OnTriggerEnter NullReferenceException; returning original exception. First stack fingerprint: {Fingerprint(__exception)}. First detail: {__exception}");
        return __exception;
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

[HarmonyPatch(typeof(SoccerBallProp), "ActivatePhysicsTrigger")]
internal static class SoccerBallPropActivatePhysicsTriggerPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    private static bool Prefix(SoccerBallProp __instance, Collider other)
    {
        if (__instance == null || other?.gameObject == null)
        {
            Warnings.Warn("missing-trigger", "Skipped SoccerBallProp.ActivatePhysicsTrigger because the ball or collider was missing.");
            return false;
        }

        GameObject otherObject = other.gameObject;
        if (!IsPlayerOrEnemy(otherObject))
        {
            return true;
        }

        if (StartOfRound.Instance == null)
        {
            Warnings.Warn("missing-startofround", "Skipped SoccerBallProp.ActivatePhysicsTrigger because StartOfRound was not ready.");
            return false;
        }

        return true;
    }

    private static Exception Finalizer(SoccerBallProp __instance, Collider other, Exception __exception)
    {
        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        if (IsKnownSafeActivateNre(__instance, other, out string reason))
        {
            Warnings.Warn($"nre|{reason}", $"Suppressed SoccerBallProp.ActivatePhysicsTrigger NullReferenceException after known unsafe trigger dependencies were detected: {reason}.");
            return null;
        }

        UnknownWarnings.Warn($"nre|unknown|{__exception.GetType().Name}", () => $"Unhandled SoccerBallProp.ActivatePhysicsTrigger NullReferenceException; returning original exception. First stack fingerprint: {Fingerprint(__exception)}. First detail: {__exception}");
        return __exception;
    }

    private static bool IsKnownSafeActivateNre(SoccerBallProp ball, Collider other, out string reason)
    {
        if (ball == null)
        {
            reason = "ball was null";
            return true;
        }

        if (other?.gameObject == null)
        {
            reason = "collider or collider GameObject was missing";
            return true;
        }

        if (IsPlayerOrEnemy(other.gameObject) && StartOfRound.Instance == null)
        {
            reason = "StartOfRound.Instance was missing for a player/enemy trigger";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool IsPlayerOrEnemy(GameObject gameObject)
    {
        return gameObject != null && (gameObject.CompareTag("Player") || gameObject.CompareTag("Enemy"));
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

[HarmonyPatch(typeof(SoccerBallProp), "BeginKickBall")]
internal static class SoccerBallPropBeginKickBallPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    private static bool Prefix(SoccerBallProp __instance, bool hitByEnemy)
    {
        if (__instance != null && ((hitByEnemy && !__instance.IsServer) || __instance.isHeld || __instance.parentObject != null))
        {
            return false;
        }

        if (HasRequiredKickDependencies(__instance))
        {
            return true;
        }

        Warnings.Warn("missing-kick-dependency", "Skipped SoccerBallProp.BeginKickBall because required ball, round, player, or ship references were missing.");
        return false;
    }

    private static Exception Finalizer(SoccerBallProp __instance, Exception __exception)
    {
        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        if (HasKnownUnsafeKickDependencies(__instance, out string reason))
        {
            Warnings.Warn($"nre|{reason}", $"Suppressed SoccerBallProp.BeginKickBall NullReferenceException after known unsafe kick dependencies were detected: {reason}.");
            return null;
        }

        UnknownWarnings.Warn($"nre|unknown|{__exception.GetType().Name}", () => $"Unhandled SoccerBallProp.BeginKickBall NullReferenceException; returning original exception. First stack fingerprint: {Fingerprint(__exception)}. First detail: {__exception}");
        return __exception;
    }

    private static bool HasRequiredKickDependencies(SoccerBallProp ball)
    {
        return !HasKnownUnsafeKickDependencies(ball, out _);
    }

    private static bool HasKnownUnsafeKickDependencies(SoccerBallProp ball, out string reason)
    {
        if (ball == null || ball.transform == null || ball.itemProperties == null)
        {
            reason = "ball, transform, or itemProperties was missing";
            return true;
        }

        StartOfRound startOfRound = StartOfRound.Instance;
        RoundManager roundManager = RoundManager.Instance;
        GameNetworkManager gameNetworkManager = GameNetworkManager.Instance;
        if (startOfRound == null || roundManager == null || gameNetworkManager?.localPlayerController == null)
        {
            reason = "round, start-of-round, or local player dependencies were missing";
            return true;
        }

        if (startOfRound.elevatorTransform == null ||
            startOfRound.propsContainer == null ||
            startOfRound.shipBounds == null ||
            startOfRound.shipInnerRoomBounds == null ||
            roundManager.spawnedScrapContainer == null)
        {
            reason = "ship bounds or scrap container dependencies were missing";
            return true;
        }

        reason = string.Empty;
        return false;
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
