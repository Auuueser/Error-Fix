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

        float distanceToLastKnownHive = Vector3.Distance(__instance.eye.position, __instance.lastKnownHivePosition);
        if (!__instance.syncedLastKnownHivePosition)
        {
            __result = false;
            return false;
        }

        bool canSeeLastKnownHive = distanceToLastKnownHive < 8f &&
            !Physics.Linecast(
                __instance.eye.position,
                __instance.lastKnownHivePosition,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault,
                QueryTriggerInteraction.Ignore);

        if (distanceToLastKnownHive < 4f || canSeeLastKnownHive)
        {
            if ((Vector3.Distance(__instance.hive.transform.position, __instance.lastKnownHivePosition) > 6f &&
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
        return NullRefGuard.Suppress(__exception, "RedLocustBees.IsHiveMissing", () =>
            __instance == null || __instance.eye == null || StartOfRound.Instance == null || __instance.hive == null);
    }

    private static bool IsHivePlacedAndInLOS(RedLocustBees bees)
    {
        if (bees.hive == null || bees.eye == null || StartOfRound.Instance == null || bees.hive.isHeld)
        {
            return false;
        }

        return Vector3.Distance(bees.eye.position, bees.hive.transform.position) <= 9f &&
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
    private static bool Prefix(RedLocustBees __instance, ref bool __result)
    {
        if (__instance == null || __instance.hive == null || __instance.eye == null || StartOfRound.Instance == null || __instance.hive.isHeld)
        {
            __result = false;
            return false;
        }

        __result = Vector3.Distance(__instance.eye.position, __instance.hive.transform.position) <= 9f &&
            !Physics.Linecast(
                __instance.eye.position,
                __instance.hive.transform.position,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault,
                QueryTriggerInteraction.Ignore);
        return false;
    }

    private static Exception Finalizer(RedLocustBees __instance, Exception __exception)
    {
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
        return NullRefGuard.Suppress(__exception, "RedLocustBees.Update", () =>
            __instance == null || StartOfRound.Instance == null || __instance.agent == null || __instance.beeParticles == null || __instance.beeParticlesTarget == null);
    }
}
