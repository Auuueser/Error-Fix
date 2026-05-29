using System;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class BepInExKnownLogNoiseFilterPatch
{
    private const string RuntimeIconsBetterRotationsPrefix = "[debit_card_debit-RuntimeIcons_BetterRotations] Overriding ";
    private const string RuntimeIconsReadPrefix = "[LethalCompanyModding-RuntimeIcons] Reading ";
    private const string RuntimeIconsOverridePrefix = "[LethalCompanyModding-RuntimeIcons] Overriding RuntimeIcons/";
    private const string PathfindingLibAreaMaskPrefix = "Changed prefabbed NavMeshAgent ";
    private const string LethalPerformanceInactiveSearchSuffix = " search called with inactive objects, probably will cause incompatibility!";
    private static int runtimeIconsFilteredCount;
    private static int pathfindingLibFilteredCount;
    private static int lethalPerformanceSaveFilteredCount;
    private static int lethalPerformanceInactiveSearchFilteredCount;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (!PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.KnownBepInExLogNoiseFilterMode))
        {
            return false;
        }

        bool hasTarget = TargetMethod() != null;
        if (!hasTarget)
        {
            Plugin.Log?.LogWarning("Known BepInEx log noise filter disabled because Logger.InternalLogEvent was not found.");
        }

        return hasTarget;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Logger), "InternalLogEvent", new[] { typeof(object), typeof(LogEventArgs) });
    }

    private static bool Prefix(object sender, LogEventArgs eventArgs)
    {
        if (eventArgs == null)
        {
            return true;
        }

        string sourceName = eventArgs.Source != null
            ? eventArgs.Source.SourceName
            : (sender as ILogSource)?.SourceName;
        string message = eventArgs.Data?.ToString();
        if (!ShouldSuppress(sourceName, eventArgs.Level, message, out KnownBepInExLogNoise noise))
        {
            return true;
        }

        IncrementFilteredCount(noise);
        return false;
    }

    internal static bool ShouldSuppressForTest(string sourceName, string levelName, string message)
    {
        if (!Enum.TryParse(levelName, ignoreCase: true, out LogLevel level))
        {
            return false;
        }

        return ShouldSuppress(sourceName, level, message, out _);
    }

    internal static void FlushSummary()
    {
        int runtimeIconsCount = Interlocked.Exchange(ref runtimeIconsFilteredCount, 0);
        int pathfindingLibCount = Interlocked.Exchange(ref pathfindingLibFilteredCount, 0);
        int lethalPerformanceSaveCount = Interlocked.Exchange(ref lethalPerformanceSaveFilteredCount, 0);
        int lethalPerformanceInactiveSearchCount = Interlocked.Exchange(ref lethalPerformanceInactiveSearchFilteredCount, 0);
        if (runtimeIconsCount == 0 &&
            pathfindingLibCount == 0 &&
            lethalPerformanceSaveCount == 0 &&
            lethalPerformanceInactiveSearchCount == 0)
        {
            return;
        }

        Plugin.Log?.LogInfo(
            "Known BepInEx log noise filter suppressed logs since the last scene change: " +
            $"runtimeIcons={runtimeIconsCount}, " +
            $"pathfindingLib={pathfindingLibCount}, " +
            $"lethalPerformanceSaves={lethalPerformanceSaveCount}, " +
            $"lethalPerformanceInactiveSearch={lethalPerformanceInactiveSearchCount}.");
    }

    private static bool ShouldSuppress(string sourceName, LogLevel level, string message, out KnownBepInExLogNoise noise)
    {
        noise = KnownBepInExLogNoise.None;
        if (string.IsNullOrEmpty(sourceName) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (sourceName == "RuntimeIcons" &&
            level == LogLevel.Debug &&
            (message.StartsWith(RuntimeIconsBetterRotationsPrefix, StringComparison.Ordinal) ||
             message.StartsWith(RuntimeIconsReadPrefix, StringComparison.Ordinal) ||
             message.StartsWith(RuntimeIconsOverridePrefix, StringComparison.Ordinal)))
        {
            noise = KnownBepInExLogNoise.RuntimeIcons;
            return true;
        }

        if (sourceName == "PathfindingLib" &&
            level == LogLevel.Debug &&
            (message.StartsWith(PathfindingLibAreaMaskPrefix, StringComparison.Ordinal) ||
             message.IndexOf(" connects to ", StringComparison.Ordinal) >= 0))
        {
            noise = KnownBepInExLogNoise.PathfindingLib;
            return true;
        }

        if (sourceName == "LethalPerformance.Patcher" &&
            level == LogLevel.Info &&
            message.StartsWith("Saved ", StringComparison.Ordinal) &&
            message.EndsWith(" config(s)", StringComparison.Ordinal))
        {
            noise = KnownBepInExLogNoise.LethalPerformanceSave;
            return true;
        }

        if (sourceName == "LethalPerformance" &&
            level == LogLevel.Info &&
            message.StartsWith("Saved ", StringComparison.Ordinal) &&
            message.EndsWith(" save(s)", StringComparison.Ordinal))
        {
            noise = KnownBepInExLogNoise.LethalPerformanceSave;
            return true;
        }

        if (sourceName == "LethalPerformance" &&
            level == LogLevel.Warning &&
            message.EndsWith(LethalPerformanceInactiveSearchSuffix, StringComparison.Ordinal))
        {
            noise = KnownBepInExLogNoise.LethalPerformanceInactiveSearch;
            return true;
        }

        return false;
    }

    private static void IncrementFilteredCount(KnownBepInExLogNoise noise)
    {
        switch (noise)
        {
            case KnownBepInExLogNoise.RuntimeIcons:
                Interlocked.Increment(ref runtimeIconsFilteredCount);
                break;
            case KnownBepInExLogNoise.PathfindingLib:
                Interlocked.Increment(ref pathfindingLibFilteredCount);
                break;
            case KnownBepInExLogNoise.LethalPerformanceSave:
                Interlocked.Increment(ref lethalPerformanceSaveFilteredCount);
                break;
            case KnownBepInExLogNoise.LethalPerformanceInactiveSearch:
                Interlocked.Increment(ref lethalPerformanceInactiveSearchFilteredCount);
                break;
        }
    }

    private enum KnownBepInExLogNoise
    {
        None,
        RuntimeIcons,
        PathfindingLib,
        LethalPerformanceSave,
        LethalPerformanceInactiveSearch
    }
}
