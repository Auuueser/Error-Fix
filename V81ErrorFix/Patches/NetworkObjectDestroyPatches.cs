using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Unity.Netcode;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class NetworkObjectDestroyGuardPatch
{
    private const float WarningInterval = 5f;
    private const float WarningCacheCleanupInterval = 30f;
    private static readonly Dictionary<ulong, float> LastWarningTimes = new();
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter GuardFailureWarnings = new();
    private static bool _loggedBlockedDestroyStackTrace;
    private static float _nextWarningCacheCleanupTime;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(UnityEngine.Object), "Destroy", new[] { typeof(UnityEngine.Object) });
        yield return AccessTools.Method(typeof(UnityEngine.Object), "Destroy", new[] { typeof(UnityEngine.Object), typeof(float) });
    }

    private static bool Prefix(UnityEngine.Object obj)
    {
        try
        {
            return ShouldAllowDestroy(obj);
        }
        catch (Exception ex)
        {
            GuardFailureWarnings.Warn("guard-failure", $"NetworkObject destroy guard failed safely and allowed original Destroy: {ex.GetType().Name}.");
            return true;
        }
    }

    private static bool ShouldAllowDestroy(UnityEngine.Object obj)
    {
        if (!PatchModeUtility.IsEnabled(ErrorFixConfig.GlobalDestroyGuardMode))
        {
            return true;
        }

        if (ErrorFixConfig.EnableGlobalDestroyGuard != null && !ErrorFixConfig.EnableGlobalDestroyGuard.Value)
        {
            return true;
        }

        if (obj == null || NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            return true;
        }

        if (ShouldAllowLifecycleDestroy())
        {
            return true;
        }

        bool targetWasGameObject = obj is GameObject;
        GameObject gameObject = obj as GameObject;
        if (gameObject == null && obj is Component component)
        {
            gameObject = component.gameObject;
        }

        if (gameObject == null)
        {
            return true;
        }

        RagdollGrabbableObject ragdollObject = gameObject.GetComponent<RagdollGrabbableObject>() ?? gameObject.GetComponentInParent<RagdollGrabbableObject>();
        if (ragdollObject == null && targetWasGameObject)
        {
            ragdollObject = gameObject.GetComponentInChildren<RagdollGrabbableObject>();
        }

        if (ragdollObject == null)
        {
            return true;
        }

        NetworkObject networkObject = ragdollObject.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsSpawned)
        {
            return true;
        }

        WarnBlockedDestroy(networkObject);
        return false;
    }

    internal static void ClearCaches()
    {
        LastWarningTimes.Clear();
        Warnings.Clear();
        GuardFailureWarnings.Clear();
        _loggedBlockedDestroyStackTrace = false;
    }

    private static bool ShouldAllowLifecycleDestroy()
    {
        if (ErrorFixConfig.AllowDestroyDuringSceneUnload != null && !ErrorFixConfig.AllowDestroyDuringSceneUnload.Value)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.ShutdownInProgress || !networkManager.IsListening)
        {
            return true;
        }

        if (SceneLifecycle.IsLifecycleDestroyAllowed)
        {
            return true;
        }

        StartOfRound startOfRound = StartOfRound.Instance;
        return startOfRound == null || startOfRound.shipIsLeaving || startOfRound.inShipPhase;
    }

    private static void WarnBlockedDestroy(NetworkObject networkObject)
    {
        float now = Time.realtimeSinceStartup;
        CleanupWarningCacheIfNeeded(now);
        if (LastWarningTimes.TryGetValue(networkObject.NetworkObjectId, out float lastWarningTime) && now - lastWarningTime < WarningInterval)
        {
            return;
        }

        LastWarningTimes[networkObject.NetworkObjectId] = now;
        Warnings.Warn($"blocked-destroy|{networkObject.NetworkObjectId}", $"Blocked client-side Destroy on spawned {networkObject.name}; waiting for server despawn.");
        LogStackTraceOnceIfEnabled();
    }

    private static void CleanupWarningCacheIfNeeded(float now)
    {
        if (now < _nextWarningCacheCleanupTime || LastWarningTimes.Count < 128)
        {
            return;
        }

        _nextWarningCacheCleanupTime = now + WarningCacheCleanupInterval;
        List<ulong> expiredIds = null;
        foreach (KeyValuePair<ulong, float> warningTime in LastWarningTimes)
        {
            if (now - warningTime.Value < WarningInterval)
            {
                continue;
            }

            expiredIds ??= new List<ulong>();
            expiredIds.Add(warningTime.Key);
        }

        if (expiredIds == null)
        {
            return;
        }

        for (int i = 0; i < expiredIds.Count; i++)
        {
            LastWarningTimes.Remove(expiredIds[i]);
        }
    }

    private static void LogStackTraceOnceIfEnabled()
    {
        if (_loggedBlockedDestroyStackTrace || ErrorFixConfig.LogBlockedDestroyStackTraceOnce == null || !ErrorFixConfig.LogBlockedDestroyStackTraceOnce.Value)
        {
            return;
        }

        _loggedBlockedDestroyStackTrace = true;
        Plugin.Log?.LogWarning($"First blocked spawned ragdoll Destroy stack trace: {Environment.StackTrace}");
    }
}
