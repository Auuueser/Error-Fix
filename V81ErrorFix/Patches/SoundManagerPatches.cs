using System;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch(typeof(SoundManager), "PlayAmbienceClipLocal")]
internal static class SoundManagerPlayAmbienceClipLocalPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(SoundManager __instance, int soundType, int clipIndex, float soundVolume, bool playInsanitySounds)
    {
        try
        {
            return IsAmbienceClipValid(__instance, soundType, clipIndex, playInsanitySounds);
        }
        catch (Exception ex)
        {
            Warnings.Warn("guard-failed", $"SoundManager.PlayAmbienceClipLocal guard failed safely and allowed original playback: {ex.GetType().Name}.");
            return true;
        }
    }

    private static Exception Finalizer(SoundManager __instance, int soundType, int clipIndex, bool playInsanitySounds, Exception __exception)
    {
        if (__exception is IndexOutOfRangeException && IsAmbienceClipInvalid(__instance, soundType, clipIndex, playInsanitySounds, out int clipCount))
        {
            Warnings.Warn($"index-exception|{soundType}|{playInsanitySounds}", $"Suppressed SoundManager.PlayAmbienceClipLocal IndexOutOfRangeException for known invalid clip soundType {soundType}, clipIndex {clipIndex}, playInsanitySounds={playInsanitySounds}, clipCount={clipCount}.");
            return null;
        }

        return __exception;
    }

    private static bool IsAmbienceClipValid(SoundManager soundManager, int soundType, int clipIndex, bool playInsanitySounds)
    {
        if (soundManager == null || soundManager.currentLevelAmbience == null)
        {
            Warnings.Warn("missing-ambience", "Skipped SoundManager.PlayAmbienceClipLocal because currentLevelAmbience was missing.");
            return false;
        }

        if (clipIndex < 0)
        {
            Warnings.Warn($"negative-index|{soundType}|{playInsanitySounds}", $"Skipped SoundManager.PlayAmbienceClipLocal because clipIndex {clipIndex} was negative for soundType {soundType}.");
            return false;
        }

        LevelAmbienceLibrary ambience = soundManager.currentLevelAmbience;
        if (playInsanitySounds)
        {
            return IsInsanityClipValid(ambience, soundType, clipIndex);
        }

        return IsNormalClipValid(ambience, soundType, clipIndex);
    }

    private static bool IsNormalClipValid(LevelAmbienceLibrary ambience, int soundType, int clipIndex)
    {
        AudioClip[] clips = soundType switch
        {
            0 => ambience.insideAmbience,
            1 => ambience.outsideAmbience,
            2 => ambience.shipAmbience,
            _ => null
        };

        if (clips != null && clipIndex < clips.Length && clips[clipIndex] != null)
        {
            return true;
        }

        WarnInvalidIndex(soundType, clipIndex, playInsanitySounds: false, clips?.Length ?? 0);
        return false;
    }

    private static bool IsInsanityClipValid(LevelAmbienceLibrary ambience, int soundType, int clipIndex)
    {
        RandomAudioClip[] clips = soundType switch
        {
            0 => ambience.insideAmbienceInsanity,
            1 => ambience.outsideAmbienceInsanity,
            2 => ambience.shipAmbienceInsanity,
            _ => null
        };

        if (clips != null && clipIndex < clips.Length && clips[clipIndex] != null && clips[clipIndex].audioClip != null)
        {
            return true;
        }

        WarnInvalidIndex(soundType, clipIndex, playInsanitySounds: true, clips?.Length ?? 0);
        return false;
    }

    private static void WarnInvalidIndex(int soundType, int clipIndex, bool playInsanitySounds, int clipCount)
    {
        Warnings.Warn($"invalid-index|{soundType}|{playInsanitySounds}", $"Skipped SoundManager.PlayAmbienceClipLocal because soundType {soundType}, clipIndex {clipIndex}, playInsanitySounds={playInsanitySounds} was outside clip count {clipCount} or the clip was missing.");
    }

    private static bool IsAmbienceClipInvalid(SoundManager soundManager, int soundType, int clipIndex, bool playInsanitySounds, out int clipCount)
    {
        clipCount = 0;
        if (soundManager == null || soundManager.currentLevelAmbience == null)
        {
            return false;
        }

        LevelAmbienceLibrary ambience = soundManager.currentLevelAmbience;
        if (playInsanitySounds)
        {
            RandomAudioClip[] clips = soundType switch
            {
                0 => ambience.insideAmbienceInsanity,
                1 => ambience.outsideAmbienceInsanity,
                2 => ambience.shipAmbienceInsanity,
                _ => null
            };

            clipCount = clips?.Length ?? 0;
            return clips != null && (clipIndex < 0 || clipIndex >= clips.Length);
        }

        AudioClip[] normalClips = soundType switch
        {
            0 => ambience.insideAmbience,
            1 => ambience.outsideAmbience,
            2 => ambience.shipAmbience,
            _ => null
        };

        clipCount = normalClips?.Length ?? 0;
        return normalClips != null && (clipIndex < 0 || clipIndex >= normalClips.Length);
    }
}
