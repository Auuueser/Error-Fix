using UnityEngine;
using UnityEngine.SceneManagement;

namespace V81ErrorFix;

internal static class SceneLifecycle
{
    private const float DefaultLifecycleDestroyWindowSeconds = 3f;
    private const float MaxLifecycleDestroyWindowSeconds = 15f;

    internal static float LifecycleDestroyAllowedUntil { get; private set; }
    internal static bool IsSceneUnloading => IsLifecycleDestroyAllowed;
    internal static bool IsLifecycleDestroyAllowed => LifecycleDestroyAllowedUntil > 0f && Time.realtimeSinceStartup <= LifecycleDestroyAllowedUntil;

    internal static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnityKnownWarningFilterPatch.FlushSummary();
        BepInExKnownLogNoiseFilterPatch.FlushSummary();
        AllowLifecycleDestroyWindow();
        StartOfRoundRefreshPlayerVoicePlaybackObjectsPatch.ClearCache();
        TerminalAccessibleObjectUpdatePatch.ClearCache();
        RoundManagerFindMainEntrancePositionPatch.ClearCache();
        ParticleMeshShapeGuard.NotifySceneLoaded();
        NetworkObjectDestroyGuardPatch.ClearCaches();
        WarningLimiter.ClearSceneScopedLimiters();
        NullRefGuard.Clear();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        UnityKnownWarningFilterPatch.FlushSummary();
        BepInExKnownLogNoiseFilterPatch.FlushSummary();
        AllowLifecycleDestroyWindow();
        StartOfRoundRefreshPlayerVoicePlaybackObjectsPatch.ClearCache();
        TerminalAccessibleObjectUpdatePatch.ClearCache();
        RoundManagerFindMainEntrancePositionPatch.ClearCache();
        ParticleMeshShapeGuard.NotifySceneUnloaded();
        NetworkObjectDestroyGuardPatch.ClearCaches();
        WarningLimiter.ClearSceneScopedLimiters();
        NullRefGuard.Clear();
    }

    private static void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        UnityKnownWarningFilterPatch.FlushSummary();
        BepInExKnownLogNoiseFilterPatch.FlushSummary();
        AllowLifecycleDestroyWindow();
        TerminalAccessibleObjectUpdatePatch.ClearCache();
        RoundManagerFindMainEntrancePositionPatch.ClearCache();
        WarningLimiter.ClearSceneScopedLimiters();
        NullRefGuard.Clear();
    }

    private static void AllowLifecycleDestroyWindow()
    {
        float windowSeconds = GetLifecycleDestroyWindowSeconds();
        LifecycleDestroyAllowedUntil = windowSeconds > 0f ? Time.realtimeSinceStartup + windowSeconds : 0f;
    }

    private static float GetLifecycleDestroyWindowSeconds()
    {
        return ClampLifecycleDestroyWindowSeconds(ErrorFixConfig.LifecycleDestroyWindowSeconds?.Value ?? DefaultLifecycleDestroyWindowSeconds);
    }

    internal static float ClampLifecycleDestroyWindowSeconds(float value)
    {
        return Mathf.Clamp(value, 0f, MaxLifecycleDestroyWindowSeconds);
    }
}
