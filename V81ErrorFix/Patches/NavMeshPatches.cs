using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace V81ErrorFix;

[HarmonyPatch(typeof(NavMeshSurface), "CollectSources")]
internal static class NavMeshSurfaceCollectSourcesPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter GuardFailureWarnings = new();

    private static void Postfix(NavMeshSurface __instance, ref List<NavMeshBuildSource> __result)
    {
        try
        {
            FilterUnreadableMeshSources(__instance, __result);
        }
        catch (Exception ex)
        {
            GuardFailureWarnings.Warn("guard-failure", $"NavMeshSurface source filter failed safely and left sources unchanged: {ex.GetType().Name}.");
        }
    }

    private static void FilterUnreadableMeshSources(NavMeshSurface surface, List<NavMeshBuildSource> sources)
    {
        if (sources == null || sources.Count == 0)
        {
            return;
        }

        int removedCount = 0;
        string surfaceName = GetSurfaceName(surface);
        bool shouldLog = ShouldLog(surfaceName);
        HashSet<string> removedMeshNames = shouldLog ? new HashSet<string>() : null;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            NavMeshBuildSource source = sources[i];
            if (!TryGetUnreadableMesh(source, out Mesh mesh))
            {
                continue;
            }

            sources.RemoveAt(i);
            removedCount++;
            removedMeshNames?.Add(GetMeshName(mesh));
        }

        if (removedCount > 0 && shouldLog)
        {
            Warn(surfaceName, removedMeshNames, removedCount);
        }
    }

    private static bool TryGetUnreadableMesh(NavMeshBuildSource source, out Mesh mesh)
    {
        mesh = null;
        if (source.shape != NavMeshBuildSourceShape.Mesh || source.sourceObject is not Mesh sourceMesh || sourceMesh == null)
        {
            return false;
        }

        mesh = sourceMesh;
        return !sourceMesh.isReadable;
    }

    private static bool ShouldLog(string surfaceName)
    {
        string key = $"unreadable-navmesh-sources|{surfaceName}";
        return Warnings.CanWarn(key);
    }

    private static void Warn(string surfaceName, HashSet<string> meshNames, int removedCount)
    {
        string key = $"unreadable-navmesh-sources|{surfaceName}";
        Warnings.Warn(key, () =>
        {
            string meshList = meshNames != null && meshNames.Count > 0 ? string.Join(", ", meshNames) : "unknown";
            return $"Filtered {removedCount} unreadable mesh source(s) from NavMeshSurface '{surfaceName}' before runtime NavMesh build: {meshList}.";
        });
    }

    private static string GetSurfaceName(NavMeshSurface surface)
    {
        return surface != null && surface.gameObject != null ? surface.gameObject.name : "unknown";
    }

    private static string GetMeshName(Mesh mesh)
    {
        return mesh != null && !string.IsNullOrEmpty(mesh.name) ? mesh.name : "unnamed mesh";
    }
}

[HarmonyPatch(typeof(EnemyAI), "DoAIInterval")]
internal static class EnemyAINavMeshGuardPatch
{
    private const float RecoveryAttemptCooldown = 0.5f;
    private const float RecoveryCacheCleanupInterval = 30f;
    private static readonly float[] SampleRadii = { 2f, 4f, 8f, 16f, 32f };
    private static readonly WarningLimiter Warnings = new();
    private static readonly Dictionary<int, float> NextRecoveryAttemptTimes = new();
    private static float _nextRecoveryCacheCleanupTime;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return ShouldPatch(
            ErrorFixConfig.EnemyAINavMeshGuardMode?.Value ?? PatchEnableMode.Disabled,
            ErrorFixConfig.EnableEnemyAINavMeshGuard?.Value ?? false);
    }

    private static bool Prefix(EnemyAI __instance)
    {
        try
        {
            return GuardEnemyAIInterval(__instance);
        }
        catch (Exception ex)
        {
            string enemyName = GetEnemyName(__instance);
            Warn(enemyName, $"NavMesh guard failed safely for {enemyName}: {ex.GetType().Name}.");
            return true;
        }
    }

    private static bool GuardEnemyAIInterval(EnemyAI enemy)
    {
        if (!ShouldPatch(
                ErrorFixConfig.EnemyAINavMeshGuardMode?.Value ?? PatchEnableMode.Disabled,
                ErrorFixConfig.EnableEnemyAINavMeshGuard?.Value ?? false))
        {
            return true;
        }

        if (enemy == null || enemy.inSpecialAnimation || enemy.agent == null || !enemy.moveTowardsDestination || !enemy.agent.enabled || enemy.agent.isOnNavMesh)
        {
            return true;
        }

        string enemyName = GetEnemyName(enemy);
        if (!CanAttemptRecovery(enemy))
        {
            return false;
        }

        if (ShouldRestrictRecoveryToHostServer() && !IsHostOrServer())
        {
            Warn(enemyName, $"Suppressed non-host/client {enemyName} SetDestination while its NavMeshAgent is off the NavMesh.");
            return false;
        }

        if (!enemy.IsOwner)
        {
            Warn(enemyName, $"Suppressed non-owner {enemyName} SetDestination while its NavMeshAgent is off the NavMesh.");
            return false;
        }

        if (enemy.isEnemyDead)
        {
            enemy.moveTowardsDestination = false;
            TrySyncPositionToClients(enemy);
            Warn(enemyName, $"Suppressed {enemyName} SetDestination while it is dead and its NavMeshAgent is off the NavMesh.");
            return false;
        }

        if (ShouldAllowWarp() && TryWarpToNearbyNavMesh(enemy))
        {
            Warn(enemyName, $"Recovered {enemyName} NavMeshAgent by warping it back onto the NavMesh.");
            return true;
        }

        enemy.moveTowardsDestination = false;
        TrySyncPositionToClients(enemy);
        Warn(enemyName, $"Suppressed {enemyName} SetDestination while its NavMeshAgent is off the NavMesh and no nearby NavMesh point was found.");
        return false;
    }

    private static bool TryWarpToNearbyNavMesh(EnemyAI enemy)
    {
        int areaMask = enemy.agent.areaMask;
        Vector3 position = enemy.transform.position;
        if (TryWarpToNearbyNavMesh(enemy, position, areaMask))
        {
            return true;
        }

        return areaMask != NavMesh.AllAreas && TryWarpToNearbyNavMesh(enemy, position, NavMesh.AllAreas);
    }

    private static bool TryWarpToNearbyNavMesh(EnemyAI enemy, Vector3 position, int areaMask)
    {
        for (int i = 0; i < SampleRadii.Length; i++)
        {
            float radius = SampleRadii[i];
            if (radius > GetMaxWarpRadius())
            {
                break;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, radius, areaMask) && IsUsableRecoveryPoint(enemy, hit.position, areaMask) && enemy.agent.Warp(hit.position))
            {
                if (enemy.agent.isOnNavMesh)
                {
                    enemy.agent.ResetPath();
                }

                return true;
            }
        }

        return false;
    }

    private static bool ShouldAllowWarp()
    {
        return ErrorFixConfig.AllowEnemyAIWarp == null || ErrorFixConfig.AllowEnemyAIWarp.Value;
    }

    private static bool ShouldRestrictRecoveryToHostServer()
    {
        return ErrorFixConfig.EnemyAINavMeshHostServerOnly == null || ErrorFixConfig.EnemyAINavMeshHostServerOnly.Value;
    }

    private static bool IsHostOrServer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager == null || networkManager.IsHost || networkManager.IsServer;
    }

    private static float GetMaxWarpRadius()
    {
        float configuredRadius = ErrorFixConfig.EnemyAINavMeshMaxWarpRadius?.Value ?? 32f;
        return Mathf.Clamp(configuredRadius, 0f, 64f);
    }

    private static bool IsUsableRecoveryPoint(EnemyAI enemy, Vector3 position, int areaMask)
    {
        if (enemy.destination == Vector3.zero)
        {
            return true;
        }

        NavMeshPath path = enemy.path1;
        if (path == null)
        {
            path = new NavMeshPath();
            enemy.path1 = path;
        }

        return NavMesh.CalculatePath(position, enemy.destination, areaMask, path) && path.status != NavMeshPathStatus.PathInvalid;
    }

    private static bool CanAttemptRecovery(EnemyAI enemy)
    {
        int instanceId = enemy.GetInstanceID();
        float now = Time.realtimeSinceStartup;
        CleanupRecoveryCacheIfNeeded(now);
        if (NextRecoveryAttemptTimes.TryGetValue(instanceId, out float nextAttemptTime) && now < nextAttemptTime)
        {
            return false;
        }

        NextRecoveryAttemptTimes[instanceId] = now + RecoveryAttemptCooldown;
        return true;
    }

    private static void Warn(string enemyName, string message)
    {
        Warnings.Warn(enemyName, message);
    }

    private static void CleanupRecoveryCacheIfNeeded(float now)
    {
        if (now < _nextRecoveryCacheCleanupTime || NextRecoveryAttemptTimes.Count < 128)
        {
            return;
        }

        _nextRecoveryCacheCleanupTime = now + RecoveryCacheCleanupInterval;
        List<int> expiredIds = null;
        foreach (KeyValuePair<int, float> recoveryAttemptTime in NextRecoveryAttemptTimes)
        {
            if (recoveryAttemptTime.Value >= now)
            {
                continue;
            }

            expiredIds ??= new List<int>();
            expiredIds.Add(recoveryAttemptTime.Key);
        }

        if (expiredIds == null)
        {
            return;
        }

        for (int i = 0; i < expiredIds.Count; i++)
        {
            NextRecoveryAttemptTimes.Remove(expiredIds[i]);
        }
    }

    private static string GetEnemyName(EnemyAI enemy)
    {
        if (enemy == null)
        {
            return "Unknown Enemy";
        }

        if (enemy.enemyType != null && !string.IsNullOrEmpty(enemy.enemyType.enemyName))
        {
            return enemy.enemyType.enemyName;
        }

        return enemy.name;
    }

    private static void TrySyncPositionToClients(EnemyAI enemy)
    {
        try
        {
            enemy.SyncPositionToClients();
        }
        catch (Exception ex)
        {
            Warn(GetEnemyName(enemy), $"Skipped EnemyAI position sync after NavMesh guard because it failed safely: {ex.GetType().Name}.");
        }
    }

    internal static bool ShouldPatch(PatchEnableMode mode, bool legacySwitchEnabled)
    {
        // This patch sits on EnemyAI.DoAIInterval, so Auto intentionally remains off.
        // Enabled is an explicit operator choice and is not tied to verified Assembly-CSharp.
        return legacySwitchEnabled && mode == PatchEnableMode.Enabled;
    }
}
