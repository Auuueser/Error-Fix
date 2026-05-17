using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class UnityKnownWarningFilterPatch
{
    private const string AudioSpatializerWarningPrefix = "Audio source failed to initialize audio spatializer.";
    private const string BoxColliderNegativeScaleWarningPrefix = "BoxCollider does not support negative scale or size.";
    private const string SteamValveEmptyAudioSourceWarningPrefix = "Only custom filters can be played. Please add a custom filter or an audioclip to the audiosource (SteamValve(Clone)).";
    private const string StaticLightingSkyWarningPrefix = "One Static Lighting Sky component was already set for baking, only the latest one will be used.";
    private static int audioSpatializerFilteredCount;
    private static int boxColliderFilteredCount;
    private static int steamValveEmptyAudioSourceFilteredCount;
    private static int staticLightingSkyFilteredCount;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (!PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.KnownUnityWarningFilterMode))
        {
            return false;
        }

        bool hasTarget = TargetMethod() != null;
        if (!hasTarget)
        {
            Plugin.Log?.LogWarning("Known Unity warning filter disabled because BepInEx UnityLogSource.OnUnityLogMessageReceived was not found.");
        }

        return hasTarget;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type unityLogSourceType = AccessTools.TypeByName("BepInEx.Logging.UnityLogSource");
        return unityLogSourceType != null
            ? AccessTools.Method(unityLogSourceType, "OnUnityLogMessageReceived", new[] { typeof(string), typeof(string), typeof(LogType) })
            : null;
    }

    private static bool Prefix(string message, LogType type)
    {
        if (type != LogType.Warning || !ShouldFilter(message, out KnownUnityWarning warning))
        {
            return true;
        }

        IncrementFilteredCount(warning);
        return false;
    }

    internal static void FlushSummary()
    {
        int audioSpatializerCount = Interlocked.Exchange(ref audioSpatializerFilteredCount, 0);
        int boxColliderCount = Interlocked.Exchange(ref boxColliderFilteredCount, 0);
        int steamValveEmptyAudioSourceCount = Interlocked.Exchange(ref steamValveEmptyAudioSourceFilteredCount, 0);
        int staticLightingSkyCount = Interlocked.Exchange(ref staticLightingSkyFilteredCount, 0);
        if (audioSpatializerCount == 0 &&
            boxColliderCount == 0 &&
            steamValveEmptyAudioSourceCount == 0 &&
            staticLightingSkyCount == 0)
        {
            return;
        }

        Plugin.Log?.LogInfo(
            "Known Unity warning filter suppressed warnings since the last scene change: " +
            $"audioSpatializer={audioSpatializerCount}, " +
            $"boxColliderNegativeScale={boxColliderCount}, " +
            $"steamValveEmptyAudioSource={steamValveEmptyAudioSourceCount}, " +
            $"staticLightingSky={staticLightingSkyCount}.");
    }

    private static bool ShouldFilter(string message, out KnownUnityWarning warning)
    {
        warning = KnownUnityWarning.None;
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        switch (message[0])
        {
            case 'A' when message.StartsWith(AudioSpatializerWarningPrefix, StringComparison.Ordinal):
                warning = KnownUnityWarning.AudioSpatializer;
                return true;
            case 'B' when message.StartsWith(BoxColliderNegativeScaleWarningPrefix, StringComparison.Ordinal):
                warning = KnownUnityWarning.BoxColliderNegativeScale;
                return true;
            case 'O' when message.StartsWith(SteamValveEmptyAudioSourceWarningPrefix, StringComparison.Ordinal):
                warning = KnownUnityWarning.SteamValveEmptyAudioSource;
                return true;
            case 'O' when message.StartsWith(StaticLightingSkyWarningPrefix, StringComparison.Ordinal):
                warning = KnownUnityWarning.StaticLightingSky;
                return true;
            default:
                return false;
        }
    }

    private static void IncrementFilteredCount(KnownUnityWarning warning)
    {
        switch (warning)
        {
            case KnownUnityWarning.AudioSpatializer:
                Interlocked.Increment(ref audioSpatializerFilteredCount);
                break;
            case KnownUnityWarning.BoxColliderNegativeScale:
                Interlocked.Increment(ref boxColliderFilteredCount);
                break;
            case KnownUnityWarning.SteamValveEmptyAudioSource:
                Interlocked.Increment(ref steamValveEmptyAudioSourceFilteredCount);
                break;
            case KnownUnityWarning.StaticLightingSky:
                Interlocked.Increment(ref staticLightingSkyFilteredCount);
                break;
        }
    }

    private enum KnownUnityWarning
    {
        None,
        AudioSpatializer,
        BoxColliderNegativeScale,
        SteamValveEmptyAudioSource,
        StaticLightingSky
    }
}
