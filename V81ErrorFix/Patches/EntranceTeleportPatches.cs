using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch(typeof(EntranceTeleport), "Update")]
internal static class EntranceTeleportUpdatePatch
{
    private static readonly FieldInfo TriggerScriptField = AccessTools.Field(typeof(EntranceTeleport), "triggerScript");
    private static readonly FieldInfo CheckForEnemiesIntervalField = AccessTools.Field(typeof(EntranceTeleport), "checkForEnemiesInterval");
    private static readonly FieldInfo EnemyNearLastCheckField = AccessTools.Field(typeof(EntranceTeleport), "enemyNearLastCheck");
    private static readonly FieldInfo GotExitPointField = AccessTools.Field(typeof(EntranceTeleport), "gotExitPoint");
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

        InteractTrigger triggerScript = (InteractTrigger)TriggerScriptField.GetValue(__instance);
        if (triggerScript == null)
        {
            Warnings.Warn("missing-trigger", "Skipped EntranceTeleport.Update guard because triggerScript was missing.");
            return false;
        }

        float checkForEnemiesInterval = (float)CheckForEnemiesIntervalField.GetValue(__instance);
        if (checkForEnemiesInterval > 0f)
        {
            CheckForEnemiesIntervalField.SetValue(__instance, checkForEnemiesInterval - Time.deltaTime);
            return false;
        }

        bool gotExitPoint = (bool)GotExitPointField.GetValue(__instance);
        if (!gotExitPoint)
        {
            if (__instance.FindExitPoint())
            {
                GotExitPointField.SetValue(__instance, true);
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

        CheckForEnemiesIntervalField.SetValue(__instance, 1f);
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

                if (Vector3.Distance(enemy.transform.position, __instance.exitScript.entrancePoint.position) < 7.7f)
                {
                    enemyNear = true;
                    break;
                }
            }
        }

        bool enemyNearLastCheck = (bool)EnemyNearLastCheckField.GetValue(__instance);
        if (enemyNear && !enemyNearLastCheck)
        {
            EnemyNearLastCheckField.SetValue(__instance, true);
            SaveDefaultHoverTip(__instance, triggerScript);
            triggerScript.hoverTip = "[Near activity detected!]";
        }
        else if (!enemyNear && enemyNearLastCheck)
        {
            EnemyNearLastCheckField.SetValue(__instance, false);
            triggerScript.hoverTip = GetDefaultHoverTip(__instance, triggerScript);
        }

        return false;
    }

    private static Exception Finalizer(EntranceTeleport __instance, Exception __exception)
    {
        return NullRefGuard.Suppress(__exception, "EntranceTeleport.Update", () =>
            IsPatchEnabled() &&
            (__instance == null ||
             RoundManager.Instance == null ||
             TriggerScriptField.GetValue(__instance) == null ||
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

    private static bool Prefix(bool getTeleportPosition, bool getOutsideEntrance, ref Vector3 __result)
    {
        EntranceTeleport[] entrances = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(includeInactive: false);
        EntranceTeleport fallbackEntrance = null;
        EntranceTeleport fallbackMainEntrance = null;
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

            __result = GetEntrancePosition(entrance, getTeleportPosition);
            return false;
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
