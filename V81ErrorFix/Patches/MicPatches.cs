using System;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch(typeof(DisplayPlayerMicVolume), "InitMic")]
internal static class DisplayPlayerMicVolumeInitMicPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static bool Prefix(DisplayPlayerMicVolume __instance)
    {
        if (__instance == null || IngamePlayerSettings.Instance == null || IngamePlayerSettings.Instance.unsavedSettings == null)
        {
            return false;
        }

        IngamePlayerSettings.Instance.RefreshAndDisplayCurrentMicrophone(saveResult: false);
        string device = IngamePlayerSettings.Instance.unsavedSettings.micDevice;
        if (!IsValidMicrophoneDevice(device))
        {
            return false;
        }

        Traverse fields = Traverse.Create(__instance);
        string currentDevice = fields.Field("_device").GetValue<string>();
        if (IsValidMicrophoneDevice(currentDevice) && Microphone.IsRecording(currentDevice))
        {
            Microphone.End(currentDevice);
        }

        try
        {
            Microphone.GetDeviceCaps(device, out int minFreq, out int maxFreq);
            int frequency = maxFreq <= 0 ? 44100 : Mathf.Clamp(44100, Mathf.Max(minFreq, 1), maxFreq);
            fields.Field("_device").SetValue(device);
            fields.Field("_clipRecord").SetValue(Microphone.Start(device, loop: true, 1, frequency));
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
        {
            Warn($"Skipping microphone preview for unavailable device '{device}': {ex.Message}");
            fields.Field("_clipRecord").SetValue(null);
        }

        return false;
    }

    internal static void Warn(string message)
    {
        Warnings.Warn("DisplayPlayerMicVolume", message);
    }

    internal static bool IsValidMicrophoneDevice(string device)
    {
        if (string.IsNullOrWhiteSpace(device) || device == "none" || device == "LCNoMic" || Microphone.devices == null)
        {
            return false;
        }

        return Array.IndexOf(Microphone.devices, device) >= 0;
    }
}

[HarmonyPatch(typeof(DisplayPlayerMicVolume), "StopMicrophone")]
internal static class DisplayPlayerMicVolumeStopMicrophonePatch
{
    private static bool Prefix(DisplayPlayerMicVolume __instance)
    {
        if (__instance == null)
        {
            return false;
        }

        try
        {
            string device = Traverse.Create(__instance).Field("_device").GetValue<string>();
            if (DisplayPlayerMicVolumeInitMicPatch.IsValidMicrophoneDevice(device) && Microphone.IsRecording(device))
            {
                Microphone.End(device);
            }
        }
        catch (Exception ex)
        {
            DisplayPlayerMicVolumeInitMicPatch.Warn($"Skipping microphone stop because it failed safely: {ex.GetType().Name}");
        }

        return false;
    }
}

[HarmonyPatch(typeof(DisplayPlayerMicVolume), "LevelMax")]
internal static class DisplayPlayerMicVolumeLevelMaxPatch
{
    private static bool Prefix(DisplayPlayerMicVolume __instance, ref float __result)
    {
        __result = 0f;
        if (__instance == null || IngamePlayerSettings.Instance == null || IngamePlayerSettings.Instance.unsavedSettings == null)
        {
            return false;
        }

        string device = IngamePlayerSettings.Instance.unsavedSettings.micDevice;
        if (!DisplayPlayerMicVolumeInitMicPatch.IsValidMicrophoneDevice(device) || !Microphone.IsRecording(device))
        {
            return false;
        }

        try
        {
            AudioClip clipRecord = Traverse.Create(__instance).Field("_clipRecord").GetValue<AudioClip>();
            return clipRecord != null;
        }
        catch (Exception ex)
        {
            DisplayPlayerMicVolumeInitMicPatch.Warn($"Skipping microphone level preview because it failed safely: {ex.GetType().Name}");
            return false;
        }
    }
}
