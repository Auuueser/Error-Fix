using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class DisabledAudioSourcePlayGuardPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter GuardFailureWarnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.AudioSourcePlaybackGuardMode);
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in typeof(AudioSource).GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (method.DeclaringType == typeof(AudioSource) && IsPlaybackMethod(method))
            {
                yield return method;
            }
        }
    }

    private static bool Prefix(AudioSource __instance, MethodBase __originalMethod)
    {
        try
        {
            return ShouldAllowPlayback(__instance, __originalMethod);
        }
        catch (Exception ex)
        {
            GuardFailureWarnings.Warn("guard-failure", $"Disabled AudioSource guard failed safely and allowed original playback: {ex.GetType().Name}.");
            return true;
        }
    }

    private static bool ShouldAllowPlayback(AudioSource audioSource, MethodBase originalMethod)
    {
        if (audioSource == null || audioSource.isActiveAndEnabled)
        {
            return true;
        }

        string methodName = originalMethod?.Name ?? "Play";
        string key = $"{audioSource.GetInstanceID()}|{methodName}";
        Warnings.Warn(key, () =>
        {
            string sourceName = GetTransformPath(audioSource.transform);
            return $"Suppressed disabled AudioSource {methodName} on '{sourceName}' because AudioSource.enabled={audioSource.enabled}, activeInHierarchy={audioSource.gameObject != null && audioSource.gameObject.activeInHierarchy}.";
        });
        return false;
    }

    private static bool IsPlaybackMethod(MethodInfo method)
    {
        return method.ReturnType == typeof(void)
            && (method.Name == "Play"
                || method.Name == "PlayDelayed"
                || method.Name == "PlayScheduled"
                || method.Name == "PlayOneShot");
    }

    internal static string GetTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return "unknown";
        }

        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}

[HarmonyPatch]
internal static class AudioSourcePlayOneShotNullClipGuardPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter GuardFailureWarnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.AudioSourcePlaybackGuardMode);
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(AudioSource), "PlayOneShot", new[] { typeof(AudioClip) });
        yield return AccessTools.Method(typeof(AudioSource), "PlayOneShot", new[] { typeof(AudioClip), typeof(float) });
    }

    private static bool Prefix(AudioSource __instance, AudioClip __0)
    {
        try
        {
            if (__0 != null)
            {
                return true;
            }

            WarnNullClip(__instance);
            return false;
        }
        catch (Exception ex)
        {
            GuardFailureWarnings.Warn("guard-failure", $"AudioSource null AudioClip guard failed safely and allowed original PlayOneShot: {ex.GetType().Name}.");
            return true;
        }
    }

    private static void WarnNullClip(AudioSource audioSource)
    {
        string key = audioSource != null ? $"null-clip|{audioSource.GetInstanceID()}" : "null-clip|unknown";
        Warnings.Warn(key, () =>
        {
            string sourceName = audioSource != null ? DisabledAudioSourcePlayGuardPatch.GetTransformPath(audioSource.transform) : "unknown";
            return $"Suppressed AudioSource.PlayOneShot on '{sourceName}' because AudioClip was null.";
        });
    }
}
