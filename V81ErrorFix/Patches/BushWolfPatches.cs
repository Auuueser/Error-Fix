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

[HarmonyPatch(typeof(BushWolfEnemy), "Update")]
internal static class BushWolfEnemyUpdatePatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return GameplayEnemyUpdatePatchGate.ShouldPatchConfigured();
    }

    private static bool Prefix(BushWolfEnemy __instance)
    {
        if (__instance == null || StartOfRound.Instance == null)
        {
            return false;
        }

        if (__instance.agent == null || __instance.creatureAnimator == null || __instance.animationContainer == null)
        {
            return false;
        }

        return true;
    }

    private static Exception Finalizer(BushWolfEnemy __instance, Exception __exception)
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
                __instance.targetPlayer = null;
                __instance.movingTowardsTargetPlayer = false;
            }
            catch
            {
            }
        }

        return NullRefGuard.Suppress(__exception, "BushWolfEnemy.Update", () =>
            __instance == null || StartOfRound.Instance == null || __instance.agent == null || __instance.creatureAnimator == null || __instance.animationContainer == null);
    }
}

[HarmonyPatch(typeof(BushWolfEnemy), "LateUpdate")]
internal static class BushWolfEnemyLateUpdatePatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return GameplayEnemyUpdatePatchGate.ShouldPatchConfigured();
    }

    private static bool Prefix(BushWolfEnemy __instance)
    {
        if (__instance == null || StartOfRound.Instance == null)
        {
            return false;
        }

        if (__instance.tongue == null ||
            __instance.tongueStartPoint == null ||
            __instance.animationContainer == null ||
            __instance.bendHeadBack == null ||
            __instance.proceduralBodyTargets == null ||
            __instance.IKTargetContainers == null ||
            __instance.IKTargetContainers.Length < __instance.proceduralBodyTargets.Length)
        {
            return false;
        }

        return true;
    }

    private static Exception Finalizer(BushWolfEnemy __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "BushWolfEnemy.LateUpdate", () =>
            __instance == null ||
            StartOfRound.Instance == null ||
            __instance.tongue == null ||
            __instance.tongueStartPoint == null ||
            __instance.animationContainer == null ||
            __instance.bendHeadBack == null ||
            __instance.proceduralBodyTargets == null ||
            __instance.IKTargetContainers == null ||
            __instance.IKTargetContainers.Length < __instance.proceduralBodyTargets.Length);
    }
}
