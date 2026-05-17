using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace V81ErrorFix;

[HarmonyPatch(typeof(EntranceTeleport), "Update")]
internal static class EntranceTeleportUpdatePatch
{
    private const float EnemyNearDistanceSqr = 7.7f * 7.7f;
    private static readonly WarningLimiter Warnings = new();
    private static readonly ConditionalWeakTable<EntranceTeleport, HoverTipState> HoverTipStates = new();

    private static bool Prefix(EntranceTeleport __instance)
    {
        if (!IsPatchEnabled())
        {
            return true;
        }

        if (__instance == null || !__instance.isEntranceToBuilding || RoundManager.Instance == null)
        {
            return false;
        }

        InteractTrigger triggerScript = __instance.triggerScript;
        if (triggerScript == null)
        {
            Warnings.Warn("missing-trigger", "Skipped EntranceTeleport.Update guard because triggerScript was missing.");
            return false;
        }

        if (__instance.checkForEnemiesInterval > 0f)
        {
            __instance.checkForEnemiesInterval -= Time.deltaTime;
            return false;
        }

        if (!__instance.gotExitPoint)
        {
            if (__instance.FindExitPoint())
            {
                __instance.gotExitPoint = true;
            }
            else
            {
                Warnings.Warn($"missing-exit|{GetEntranceName(__instance)}", $"Skipped EntranceTeleport.Update for '{GetEntranceName(__instance)}' because no exit point was found.");
            }

            return false;
        }

        if ((__instance.exitScript == null || __instance.exitScript.entrancePoint == null) && !__instance.FindExitPoint())
        {
            Warnings.Warn($"missing-exit-point|{GetEntranceName(__instance)}", $"Skipped EntranceTeleport.Update for '{GetEntranceName(__instance)}' because exitScript or entrancePoint was missing.");
            return false;
        }

        __instance.checkForEnemiesInterval = 1f;
        bool enemyNear = false;
        if (RoundManager.Instance.SpawnedEnemies != null)
        {
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy == null || enemy.transform == null || enemy.isEnemyDead)
                {
                    continue;
                }

                if (__instance.exitScript == null || __instance.exitScript.entrancePoint == null)
                {
                    break;
                }

                if ((enemy.transform.position - __instance.exitScript.entrancePoint.position).sqrMagnitude < EnemyNearDistanceSqr)
                {
                    enemyNear = true;
                    break;
                }
            }
        }

        if (enemyNear && !__instance.enemyNearLastCheck)
        {
            __instance.enemyNearLastCheck = true;
            SaveDefaultHoverTip(__instance, triggerScript);
            triggerScript.hoverTip = "[Near activity detected!]";
        }
        else if (!enemyNear && __instance.enemyNearLastCheck)
        {
            __instance.enemyNearLastCheck = false;
            triggerScript.hoverTip = GetDefaultHoverTip(__instance, triggerScript);
        }

        return false;
    }

    private static Exception Finalizer(EntranceTeleport __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return NullRefGuard.Suppress(__exception, "EntranceTeleport.Update", () =>
            IsPatchEnabled() &&
            (__instance == null ||
             RoundManager.Instance == null ||
             __instance.triggerScript == null ||
             __instance.exitScript == null ||
             __instance.exitScript.entrancePoint == null));
    }

    private static bool IsPatchEnabled()
    {
        return PatchModeUtility.IsEnabled(ErrorFixConfig.EntranceTeleportUpdateGuardMode);
    }

    private static void SaveDefaultHoverTip(EntranceTeleport entrance, InteractTrigger trigger)
    {
        if (entrance == null || trigger == null)
        {
            return;
        }

        HoverTipState state = HoverTipStates.GetOrCreateValue(entrance);
        if (!string.Equals(trigger.hoverTip, "[Near activity detected!]", StringComparison.Ordinal))
        {
            state.DefaultHoverTip = trigger.hoverTip;
        }
    }

    private static string GetDefaultHoverTip(EntranceTeleport entrance, InteractTrigger trigger)
    {
        if (entrance != null && HoverTipStates.TryGetValue(entrance, out HoverTipState state) && !string.IsNullOrEmpty(state.DefaultHoverTip))
        {
            return state.DefaultHoverTip;
        }

        return trigger != null ? trigger.hoverTip : string.Empty;
    }

    private static string GetEntranceName(EntranceTeleport entrance)
    {
        return entrance != null && entrance.gameObject != null ? entrance.gameObject.name : "unknown";
    }

    private sealed class HoverTipState
    {
        internal string DefaultHoverTip;
    }
}

[HarmonyPatch(typeof(RoundManager), "FindMainEntrancePosition")]
internal static class RoundManagerFindMainEntrancePositionPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static EntranceTeleport[] cachedEntrances;
    private static int cachedSceneHandle = int.MinValue;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PatchModeUtility.IsEnabled(ErrorFixConfig.FindMainEntrancePositionFallbackMode);
    }

    private static bool Prefix(bool getTeleportPosition, bool getOutsideEntrance, ref Vector3 __result)
    {
        EntranceTeleport[] entrances = GetCachedEntrances();
        EntranceTeleport fallbackEntrance = null;
        EntranceTeleport fallbackMainEntrance = null;
        if (TryFindEntrance(entrances, getTeleportPosition, getOutsideEntrance, ref __result, ref fallbackEntrance, ref fallbackMainEntrance))
        {
            return false;
        }

        if (fallbackEntrance == null && entrances.Length > 0)
        {
            entrances = RefreshEntranceCache();
            if (TryFindEntrance(entrances, getTeleportPosition, getOutsideEntrance, ref __result, ref fallbackEntrance, ref fallbackMainEntrance))
            {
                return false;
            }
        }

        EntranceTeleport fallback = fallbackMainEntrance ?? fallbackEntrance;
        if (fallback != null)
        {
            __result = GetEntrancePosition(fallback, getTeleportPosition);
            Warn("Main entrance position was missing; using the first available EntranceTeleport instead of origin.");
            return false;
        }

        __result = Vector3.zero;
        if (!IsCompanyLevel())
        {
            Warn("Main entrance position was missing and no EntranceTeleport fallback existed; returning origin.");
        }

        return false;
    }

    private static bool TryFindEntrance(
        EntranceTeleport[] entrances,
        bool getTeleportPosition,
        bool getOutsideEntrance,
        ref Vector3 result,
        ref EntranceTeleport fallbackEntrance,
        ref EntranceTeleport fallbackMainEntrance)
    {
        for (int i = 0; i < entrances.Length; i++)
        {
            EntranceTeleport entrance = entrances[i];
            if (entrance == null)
            {
                continue;
            }

            fallbackEntrance ??= entrance;
            if (entrance.entranceId == 0)
            {
                fallbackMainEntrance ??= entrance;
            }

            if (entrance.entranceId != 0 || entrance.isEntranceToBuilding != getOutsideEntrance)
            {
                continue;
            }

            result = GetEntrancePosition(entrance, getTeleportPosition);
            return true;
        }

        return false;
    }

    private static EntranceTeleport[] GetCachedEntrances()
    {
        int activeSceneHandle = SceneManager.GetActiveScene().handle;
        if (cachedEntrances == null || cachedSceneHandle != activeSceneHandle || cachedEntrances.Length == 0)
        {
            return RefreshEntranceCache(activeSceneHandle);
        }

        return cachedEntrances;
    }

    private static EntranceTeleport[] RefreshEntranceCache()
    {
        return RefreshEntranceCache(SceneManager.GetActiveScene().handle);
    }

    private static EntranceTeleport[] RefreshEntranceCache(int activeSceneHandle)
    {
        cachedEntrances = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(includeInactive: false);
        cachedSceneHandle = activeSceneHandle;
        return cachedEntrances;
    }

    internal static void ClearCache()
    {
        cachedEntrances = null;
        cachedSceneHandle = int.MinValue;
        Warnings.Clear();
    }

    private static Vector3 GetEntrancePosition(EntranceTeleport entrance, bool getTeleportPosition)
    {
        if (entrance == null)
        {
            return Vector3.zero;
        }

        if (getTeleportPosition && entrance.entrancePoint != null)
        {
            return entrance.entrancePoint.position;
        }

        return entrance.transform != null ? entrance.transform.position : Vector3.zero;
    }

    private static bool IsCompanyLevel()
    {
        SelectableLevel currentLevel = StartOfRound.Instance != null ? StartOfRound.Instance.currentLevel : null;
        if (currentLevel == null)
        {
            return false;
        }

        return currentLevel.levelID == 3
            || string.Equals(currentLevel.sceneName, "CompanyBuilding", StringComparison.OrdinalIgnoreCase)
            || (currentLevel.PlanetName != null && currentLevel.PlanetName.IndexOf("company", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void Warn(string message)
    {
        Warnings.Warn("FindMainEntrancePosition", message);
    }
}
