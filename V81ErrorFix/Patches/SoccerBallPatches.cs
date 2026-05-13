using System;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch(typeof(GrabbableObjectPhysicsTrigger), "OnTriggerEnter")]
internal static class GrabbableObjectPhysicsTriggerOnTriggerEnterPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(GrabbableObjectPhysicsTrigger __instance, Collider other)
    {
        if (__instance?.itemScript != null && other?.gameObject != null)
        {
            return true;
        }

        Warnings.Warn("missing-trigger-dependency", "Skipped GrabbableObjectPhysicsTrigger.OnTriggerEnter because itemScript or collider was missing.");
        return false;
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("nre", "Suppressed GrabbableObjectPhysicsTrigger.OnTriggerEnter NullReferenceException.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(SoccerBallProp), "ActivatePhysicsTrigger")]
internal static class SoccerBallPropActivatePhysicsTriggerPatch
{
    private static readonly WarningLimiter Warnings = new();

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
            return false;
        }

        if (StartOfRound.Instance == null)
        {
            Warnings.Warn("missing-startofround", "Skipped SoccerBallProp.ActivatePhysicsTrigger because StartOfRound was not ready.");
            return false;
        }

        Vector3 hitPosition = otherObject.transform.position + Vector3.up;
        Vector3 ballPosition = __instance.transform.position + Vector3.up * 0.5f;
        if (!Physics.Linecast(hitPosition, ballPosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
        {
            __instance.BeginKickBall(hitPosition, otherObject.CompareTag("Enemy"));
        }

        return false;
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("nre", "Suppressed SoccerBallProp.ActivatePhysicsTrigger NullReferenceException.");
            return null;
        }

        return __exception;
    }

    private static bool IsPlayerOrEnemy(GameObject gameObject)
    {
        return gameObject != null && (gameObject.CompareTag("Player") || gameObject.CompareTag("Enemy"));
    }
}

[HarmonyPatch(typeof(SoccerBallProp), "BeginKickBall")]
internal static class SoccerBallPropBeginKickBallPatch
{
    private static readonly WarningLimiter Warnings = new();

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

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("nre", "Suppressed SoccerBallProp.BeginKickBall NullReferenceException.");
            return null;
        }

        return __exception;
    }

    private static bool HasRequiredKickDependencies(SoccerBallProp ball)
    {
        if (ball == null || ball.transform == null || ball.itemProperties == null)
        {
            return false;
        }

        StartOfRound startOfRound = StartOfRound.Instance;
        RoundManager roundManager = RoundManager.Instance;
        GameNetworkManager gameNetworkManager = GameNetworkManager.Instance;
        if (startOfRound == null || roundManager == null || gameNetworkManager?.localPlayerController == null)
        {
            return false;
        }

        return startOfRound.elevatorTransform != null
            && startOfRound.propsContainer != null
            && startOfRound.shipBounds != null
            && startOfRound.shipInnerRoomBounds != null
            && roundManager.spawnedScrapContainer != null;
    }
}
