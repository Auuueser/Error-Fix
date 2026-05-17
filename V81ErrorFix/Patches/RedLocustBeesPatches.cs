using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Unity.AI.Navigation;
using UnityEngine;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine.AI;

namespace V81ErrorFix;

[HarmonyPatch(typeof(RedLocustBees), "IsHiveMissing")]
internal static class RedLocustBeesIsHiveMissingPatch
{
    private const float HiveNearbyDistanceSqr = 4f * 4f;
    private const float HiveVisibleDistanceSqr = 8f * 8f;
    private const float HiveMovedDistanceSqr = 6f * 6f;

    private static bool Prefix(RedLocustBees __instance, ref bool __result)
    {
        if (__instance == null || __instance.eye == null || StartOfRound.Instance == null)
        {
            __result = false;
            return false;
        }

        if (__instance.hive == null)
        {
            __result = true;
            return false;
        }

        float distanceToLastKnownHiveSqr = (__instance.eye.position - __instance.lastKnownHivePosition).sqrMagnitude;
        if (!__instance.syncedLastKnownHivePosition)
        {
            __result = false;
            return false;
        }

        bool canSeeLastKnownHive = distanceToLastKnownHiveSqr < HiveVisibleDistanceSqr &&
            !Physics.Linecast(
                __instance.eye.position,
                __instance.lastKnownHivePosition,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault,
                QueryTriggerInteraction.Ignore);

        if (distanceToLastKnownHiveSqr < HiveNearbyDistanceSqr || canSeeLastKnownHive)
        {
            if (((__instance.hive.transform.position - __instance.lastKnownHivePosition).sqrMagnitude > HiveMovedDistanceSqr &&
                 !IsHivePlacedAndInLOS(__instance)) ||
                __instance.hive.isHeld)
            {
                __result = true;
                return false;
            }

            __instance.lastKnownHivePosition = __instance.hive.transform.position + Vector3.up * 0.5f;
        }

        __result = false;
        return false;
    }

    private static Exception Finalizer(RedLocustBees __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "RedLocustBees.IsHiveMissing", () =>
            __instance == null || __instance.eye == null || StartOfRound.Instance == null || __instance.hive == null);
    }

    private static bool IsHivePlacedAndInLOS(RedLocustBees bees)
    {
        const float HiveLineOfSightDistanceSqr = 9f * 9f;

        if (bees.hive == null || bees.eye == null || StartOfRound.Instance == null || bees.hive.isHeld)
        {
            return false;
        }

        return (bees.eye.position - bees.hive.transform.position).sqrMagnitude <= HiveLineOfSightDistanceSqr &&
            !Physics.Linecast(
                bees.eye.position,
                bees.hive.transform.position,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault,
                QueryTriggerInteraction.Ignore);
    }
}

[HarmonyPatch(typeof(RedLocustBees), "IsHivePlacedAndInLOS")]
internal static class RedLocustBeesIsHivePlacedAndInLOSPatch
{
    private const float HiveLineOfSightDistanceSqr = 9f * 9f;

    private static bool Prefix(RedLocustBees __instance, ref bool __result)
    {
        if (__instance == null || __instance.hive == null || __instance.eye == null || StartOfRound.Instance == null || __instance.hive.isHeld)
        {
            __result = false;
            return false;
        }

        __result = (__instance.eye.position - __instance.hive.transform.position).sqrMagnitude <= HiveLineOfSightDistanceSqr &&
            !Physics.Linecast(
                __instance.eye.position,
                __instance.hive.transform.position,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault,
                QueryTriggerInteraction.Ignore);
        return false;
    }

    private static Exception Finalizer(RedLocustBees __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "RedLocustBees.IsHivePlacedAndInLOS", () =>
            __instance == null || __instance.hive == null || __instance.eye == null || StartOfRound.Instance == null);
    }
}

[HarmonyPatch(typeof(RedLocustBees), "DoAIInterval")]
internal static class RedLocustBeesDoAIIntervalPatch
{
    private static bool Prefix(RedLocustBees __instance)
    {
        if (__instance == null || StartOfRound.Instance == null)
        {
            return false;
        }

        if (!__instance.hasSpawnedHive || __instance.hive != null)
        {
            return true;
        }

        __instance.targetPlayer = null;
        __instance.movingTowardsTargetPlayer = false;
        if (__instance.currentBehaviourStateIndex != 2)
        {
            __instance.SwitchToBehaviourState(2);
        }

        return false;
    }

    private static Exception Finalizer(RedLocustBees __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "RedLocustBees.DoAIInterval", () =>
            __instance == null || StartOfRound.Instance == null || __instance.hive == null);
    }
}

[HarmonyPatch(typeof(RedLocustBees), "Update")]
internal static class RedLocustBeesUpdatePatch
{
    private static bool Prefix(RedLocustBees __instance)
    {
        if (__instance == null || StartOfRound.Instance == null)
        {
            return false;
        }

        if (__instance.agent == null || __instance.beeParticles == null || __instance.beeParticlesTarget == null)
        {
            return false;
        }

        return true;
    }

    private static Exception Finalizer(RedLocustBees __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "RedLocustBees.Update", () =>
            __instance == null || StartOfRound.Instance == null || __instance.agent == null || __instance.beeParticles == null || __instance.beeParticlesTarget == null);
    }
}
