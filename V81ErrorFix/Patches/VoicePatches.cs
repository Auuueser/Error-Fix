using System;
using Dissonance.Integrations.Unity_NFGO;
using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;

namespace V81ErrorFix;

[HarmonyPatch(typeof(StartOfRound), "RefreshPlayerVoicePlaybackObjects")]
internal static class StartOfRoundRefreshPlayerVoicePlaybackObjectsPatch
{
    private const float VoiceSettingsCacheSeconds = 1f;
    private static readonly WarningLimiter Warnings = new();
    private static PlayerVoiceIngameSettings[] _cachedVoiceSettings;
    private static float _cachedVoiceSettingsUntil;
    private static int _cachedPlayerSlots = -1;

    private static bool Prefix(StartOfRound __instance)
    {
        if (!IsPatchEnabled())
        {
            return true;
        }

        if (!HasKnownBrokenVoiceObjects(__instance))
        {
            return true;
        }

        TryRefreshFallback(__instance, "known-missing-components");
        return false;
    }

    private static Exception Finalizer(StartOfRound __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!IsPatchEnabled() || __exception is not NullReferenceException)
        {
            return __exception;
        }

        return TryRefreshFallback(__instance, "vanilla-nre") ? null : __exception;
    }

    internal static void ClearCache()
    {
        _cachedVoiceSettings = null;
        _cachedVoiceSettingsUntil = 0f;
        _cachedPlayerSlots = -1;
        Warnings.ClearPrefix("voice-");
    }

    private static bool TryRefreshFallback(StartOfRound startOfRound, string reason)
    {
        try
        {
            RefreshPlayerVoicePlaybackObjectsSafely(startOfRound);
            Warnings.Warn($"voice-fallback|{reason}", $"Used safe voice playback refresh fallback because {reason} was detected.");
            return true;
        }
        catch (Exception ex)
        {
            Warnings.Warn($"voice-fallback-failed|{reason}", $"Voice playback fallback failed; returning original exception path: {ex.GetType().Name}.");
            return false;
        }
    }

    private static bool HasKnownBrokenVoiceObjects(StartOfRound startOfRound)
    {
        if (startOfRound == null || startOfRound.allPlayerScripts == null)
        {
            return false;
        }

        PlayerVoiceIngameSettings[] voiceSettings = GetVoiceSettings(startOfRound);
        if (voiceSettings == null)
        {
            return false;
        }

        for (int i = 0; i < voiceSettings.Length; i++)
        {
            PlayerVoiceIngameSettings voiceSetting = voiceSettings[i];
            if (voiceSetting == null)
            {
                ClearCache();
                return true;
            }

            if (voiceSetting.voiceAudio == null || voiceSetting._playbackComponent == null || voiceSetting._dissonanceComms == null)
            {
                Warnings.Warn($"voice-known-broken|{voiceSetting.GetInstanceID()}", $"Detected incomplete PlayerVoiceIngameSettings object #{i}; using safe voice refresh fallback.");
                return true;
            }
        }

        return false;
    }

    private static void RefreshPlayerVoicePlaybackObjectsSafely(StartOfRound startOfRound)
    {
        if (startOfRound == null || GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null || startOfRound.allPlayerScripts == null)
        {
            return;
        }

        PlayerVoiceIngameSettings[] voiceSettings = GetVoiceSettings(startOfRound);
        if (voiceSettings == null || voiceSettings.Length == 0)
        {
            Warnings.Warn("voice-no-settings", "Skipped voice playback refresh because no PlayerVoiceIngameSettings objects were found.");
            return;
        }

        for (int i = 0; i < startOfRound.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = startOfRound.allPlayerScripts[i];
            if (player == null || (!player.isPlayerControlled && !player.isPlayerDead))
            {
                continue;
            }

            string playerId = TryGetNfgoPlayerId(player);
            if (string.IsNullOrEmpty(playerId))
            {
                Warnings.Warn($"voice-missing-nfgo-player|{i}", $"Skipped voice playback refresh for player #{i} because NfgoPlayer was missing.");
                continue;
            }

            TryConnectVoiceObject(player, playerId, voiceSettings, i);
        }
    }

    private static void TryConnectVoiceObject(PlayerControllerB player, string playerId, PlayerVoiceIngameSettings[] voiceSettings, int playerIndex)
    {
        for (int j = 0; j < voiceSettings.Length; j++)
        {
            PlayerVoiceIngameSettings voiceSetting = voiceSettings[j];
            if (voiceSetting == null)
            {
                continue;
            }

            try
            {
                if (TryConnectVoiceObject(player, playerId, voiceSetting, playerIndex, j))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Warnings.Warn($"voice-object-failed|{voiceSetting.GetInstanceID()}", $"Skipped voice object #{j} for player #{playerIndex} because inspection failed safely: {ex.GetType().Name}.");
            }
        }
    }

    private static bool TryConnectVoiceObject(PlayerControllerB player, string playerId, PlayerVoiceIngameSettings voiceSetting, int playerIndex, int voiceIndex)
    {
        if (voiceSetting.voiceAudio == null || voiceSetting._playbackComponent == null || voiceSetting._dissonanceComms == null)
        {
            voiceSetting.InitializeComponents();
        }

        int voiceInstanceId = voiceSetting.GetInstanceID();
        if (!voiceSetting.isActiveAndEnabled)
        {
            Warnings.Warn($"voice-disabled|{voiceInstanceId}", $"Skipped voice object #{voiceIndex} for player #{playerIndex} because it was disabled.");
            return false;
        }

        if (voiceSetting._playerState == null)
        {
            voiceSetting.FindPlayerIfNull();
            if (voiceSetting._playerState == null)
            {
                Warnings.Warn($"voice-state-missing|{voiceInstanceId}", $"Skipped voice object #{voiceIndex} for player #{playerIndex} because its player state was not ready.");
                return false;
            }
        }

        if (voiceSetting.voiceAudio == null)
        {
            Warnings.Warn($"voice-audio-missing|{voiceInstanceId}", $"Skipped voice object #{voiceIndex} for player #{playerIndex} because its AudioSource was missing.");
            return false;
        }

        if (voiceSetting._playerState.Name != playerId)
        {
            return false;
        }

        player.voicePlayerState = voiceSetting._playerState;
        player.currentVoiceChatAudioSource = voiceSetting.voiceAudio;
        player.currentVoiceChatIngameSettings = voiceSetting;
        TryAssignVoiceMixer(player);
        return true;
    }

    private static string TryGetNfgoPlayerId(PlayerControllerB player)
    {
        try
        {
            NfgoPlayer nfgoPlayer = player.gameObject != null ? player.gameObject.GetComponentInChildren<NfgoPlayer>() : null;
            return nfgoPlayer != null ? nfgoPlayer.PlayerId : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryAssignVoiceMixer(PlayerControllerB player)
    {
        if (player == null || player.currentVoiceChatAudioSource == null || SoundManager.Instance == null || SoundManager.Instance.playerVoiceMixers == null)
        {
            return;
        }

        int playerId = (int)player.playerClientId;
        if (playerId < 0 || playerId >= SoundManager.Instance.playerVoiceMixers.Length)
        {
            Warnings.Warn($"voice-mixer-index|{playerId}", $"Skipped voice mixer assignment because player client id {playerId} was outside mixer count {SoundManager.Instance.playerVoiceMixers.Length}.");
            return;
        }

        player.currentVoiceChatAudioSource.outputAudioMixerGroup = SoundManager.Instance.playerVoiceMixers[playerId];
    }

    private static PlayerVoiceIngameSettings[] GetVoiceSettings(StartOfRound startOfRound)
    {
        int playerSlots = startOfRound != null && startOfRound.allPlayerScripts != null ? startOfRound.allPlayerScripts.Length : -1;
        float now = Time.realtimeSinceStartup;
        if (_cachedVoiceSettings != null && now < _cachedVoiceSettingsUntil && _cachedPlayerSlots == playerSlots && !ContainsDestroyedVoiceSettings(_cachedVoiceSettings))
        {
            return _cachedVoiceSettings;
        }

        _cachedVoiceSettings = UnityEngine.Object.FindObjectsOfType<PlayerVoiceIngameSettings>(includeInactive: true);
        _cachedVoiceSettingsUntil = now + VoiceSettingsCacheSeconds;
        _cachedPlayerSlots = playerSlots;
        return _cachedVoiceSettings;
    }

    private static bool ContainsDestroyedVoiceSettings(PlayerVoiceIngameSettings[] voiceSettings)
    {
        if (voiceSettings == null)
        {
            return false;
        }

        for (int i = 0; i < voiceSettings.Length; i++)
        {
            if (voiceSettings[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPatchEnabled()
    {
        return PatchModeUtility.IsEnabled(ErrorFixConfig.VoiceRefreshFallbackMode);
    }
}
