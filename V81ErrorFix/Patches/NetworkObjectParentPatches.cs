using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class NetworkObjectParentChangedPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return TargetMethod() != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(NetworkObject), "OnTransformParentChanged");
    }

    private static bool Prefix(NetworkObject __instance)
    {
        if (__instance == null || __instance.IsSpawned)
        {
            return true;
        }

        Warnings.Warn(GetWarningKey(__instance), () => $"Skipped NetworkObject.OnTransformParentChanged for unspawned object '{GetObjectName(__instance)}' to avoid invalid reparent handling.");
        return false;
    }

    private static Exception Finalizer(NetworkObject __instance, Exception __exception)
    {
        if (IsKnownSpawnStateException(__exception))
        {
            Warnings.Warn(GetWarningKey(__instance), () => $"Suppressed NetworkObject reparent SpawnStateException for '{GetObjectName(__instance)}'.");
            return null;
        }

        return __exception;
    }

    private static bool IsKnownSpawnStateException(Exception exception)
    {
        return exception != null
            && exception.GetType().Name == "SpawnStateException"
            && exception.Message != null
            && exception.Message.Contains("NetworkObject can only be reparented after being spawned");
    }

    private static string GetWarningKey(NetworkObject networkObject)
    {
        return networkObject != null ? $"network-parent|{networkObject.GetInstanceID()}" : "network-parent|unknown";
    }

    private static string GetObjectName(NetworkObject networkObject)
    {
        return networkObject != null ? networkObject.name : "unknown";
    }
}
