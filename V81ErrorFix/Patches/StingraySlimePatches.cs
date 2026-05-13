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

[HarmonyPatch]
internal static class StingrayCreateSlimePatch
{
    private const float WarningCooldownSeconds = 5f;
    private const int MaxWarnings = 5;
    private static float _nextWarningTime;
    private static int _warningCount;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo createSlimeSingle = AccessTools.Method(typeof(StingrayAI), "CreateSlime", new[] { typeof(Vector3) });
        MethodInfo createSlimeArray = AccessTools.Method(typeof(StingrayAI), "CreateSlime", new[] { typeof(Vector3[]) });

        if (createSlimeSingle != null)
        {
            yield return createSlimeSingle;
        }

        if (createSlimeArray != null)
        {
            yield return createSlimeArray;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(StingrayAI __instance, MethodBase __originalMethod)
    {
        if (__instance == null)
        {
            ThrottledWarning($"{__originalMethod?.Name ?? "CreateSlime"} skipped because dependency was not ready: __instance");
            return false;
        }

        if (TryGetMissingDependency(__instance, out string missingDependency))
        {
            ThrottledWarning($"{__originalMethod?.Name ?? "CreateSlime"} skipped because dependency was not ready: {missingDependency}");
            return false;
        }

        return true;
    }

    private static bool TryGetMissingDependency(StingrayAI stingray, out string missingDependency)
    {
        StartOfRound startOfRound = StartOfRound.Instance;
        if (startOfRound == null)
        {
            missingDependency = "StartOfRound.Instance";
            return true;
        }

        RoundManager roundManager = RoundManager.Instance;
        if (roundManager == null)
        {
            missingDependency = "RoundManager.Instance";
            return true;
        }

        if (roundManager.mapPropsContainer == null)
        {
            missingDependency = "RoundManager.Instance.mapPropsContainer";
            return true;
        }

        if (stingray.slimePrefab == null)
        {
            missingDependency = "__instance.slimePrefab";
            return true;
        }

        if (startOfRound.slimeDecals == null)
        {
            missingDependency = "StartOfRound.Instance.slimeDecals";
            return true;
        }

        if (startOfRound.slimeDecalsFadingIn == null)
        {
            missingDependency = "StartOfRound.Instance.slimeDecalsFadingIn";
            return true;
        }

        if (startOfRound.slimeDecalsFadingIn.Length <= 0)
        {
            missingDependency = "StartOfRound.Instance.slimeDecalsFadingIn.Length <= 0";
            return true;
        }

        missingDependency = string.Empty;
        return false;
    }

    private static void ThrottledWarning(string message)
    {
        if (_warningCount >= MaxWarnings || Time.realtimeSinceStartup < _nextWarningTime)
        {
            return;
        }

        _warningCount++;
        _nextWarningTime = Time.realtimeSinceStartup + WarningCooldownSeconds;
        Plugin.Log?.LogWarning($"{message} ({_warningCount}/{MaxWarnings})");
    }
}
