using BepInEx.Configuration;

namespace V81ErrorFix;

internal enum PatchEnableMode
{
    Auto,
    Enabled,
    Disabled
}

internal static class ErrorFixConfig
{
    internal static ConfigEntry<PatchEnableMode> RuntimePatchMode;
    internal static ConfigEntry<bool> EnableGlobalDestroyGuard;
    internal static ConfigEntry<PatchEnableMode> GlobalDestroyGuardMode;
    internal static ConfigEntry<bool> AllowDestroyDuringSceneUnload;
    internal static ConfigEntry<float> LifecycleDestroyWindowSeconds;
    internal static ConfigEntry<bool> LogBlockedDestroyStackTraceOnce;
    internal static ConfigEntry<PatchEnableMode> AudioSourcePlaybackGuardMode;
    internal static ConfigEntry<PatchEnableMode> KnownUnityWarningFilterMode;
    internal static ConfigEntry<PatchEnableMode> KnownBepInExLogNoiseFilterMode;
    internal static ConfigEntry<PatchEnableMode> PlayerRagdollGlobalTagGuardMode;
    internal static ConfigEntry<PatchEnableMode> ParticleMeshShapeGuardMode;
    internal static ConfigEntry<bool> ParticleMeshShapeGuardDryRun;
    internal static ConfigEntry<PatchEnableMode> EntranceTeleportUpdateGuardMode;
    internal static ConfigEntry<PatchEnableMode> FindMainEntrancePositionFallbackMode;
    internal static ConfigEntry<PatchEnableMode> PlayerNearOtherPlayersGuardMode;
    internal static ConfigEntry<PatchEnableMode> TerminalAccessibleObjectUpdateGuardMode;
    internal static ConfigEntry<PatchEnableMode> GameplayEnemyUpdateGuardMode;
    internal static ConfigEntry<PatchEnableMode> ThrowObjectClientRpcGuardMode;
    internal static ConfigEntry<PatchEnableMode> VoiceRefreshFallbackMode;
    internal static ConfigEntry<PatchEnableMode> UnlockableSuitGuardMode;
    internal static ConfigEntry<PatchEnableMode> UnlockableSuitUpdateGuardMode;
    internal static ConfigEntry<PatchEnableMode> NetworkObjectParentGuardMode;
    internal static ConfigEntry<PatchEnableMode> SteamValveDamageTriggerSpawnGuardMode;
    internal static ConfigEntry<PatchEnableMode> EnemyHealthBarsLateUpdateGuardMode;
    internal static ConfigEntry<PatchEnableMode> ShipLootPlusUiHelperGuardMode;
    internal static ConfigEntry<PatchEnableMode> NightVisionInsideLightingPostfixGuardMode;
    internal static ConfigEntry<PatchEnableMode> ChatCommandsStartHostPostfixGuardMode;
    internal static ConfigEntry<PatchEnableMode> EnemyAINavMeshGuardMode;
    internal static ConfigEntry<bool> EnableEnemyAINavMeshGuard;
    internal static ConfigEntry<bool> AllowEnemyAIWarp;
    internal static ConfigEntry<float> EnemyAINavMeshMaxWarpRadius;
    internal static ConfigEntry<bool> EnemyAINavMeshHostServerOnly;

    internal static ConfigEntry<PatchEnableMode> BindRuntimePatchMode(ConfigFile config)
    {
        RuntimePatchMode = config.Bind(
            "Performance",
            "RuntimePatchMode",
            PatchEnableMode.Enabled,
            "Master switch for all Error Fix runtime Harmony patches and scene lifecycle hooks. Disabled leaves the DLL loaded but installs no runtime patches, binds no other ErrorFix settings, and skips Assembly-CSharp verification; use this for FPS baseline testing against an installed-but-passive ErrorFix. Requires restart.");
        return RuntimePatchMode;
    }

    internal static void Bind(ConfigFile config)
    {
        BindRuntimePatchMode(config);

        EnableGlobalDestroyGuard = config.Bind(
            "NetworkObjectDestroy",
            "EnableGlobalDestroyGuard",
            true,
            "Legacy switch for the global Destroy guard. GlobalDestroyGuardMode can also disable this patch.");

        GlobalDestroyGuardMode = config.Bind(
            "NetworkObjectDestroy",
            "GlobalDestroyGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the global UnityEngine.Object.Destroy hook. Auto is treated as disabled for this global hook to avoid upgrade-time performance surprises. Requires restart.");

        AllowDestroyDuringSceneUnload = config.Bind(
            "NetworkObjectDestroy",
            "AllowDestroyDuringSceneUnload",
            true,
            "Allows Destroy during network shutdown, ship scene unload, or lobby transitions.");

        LifecycleDestroyWindowSeconds = config.Bind(
            "NetworkObjectDestroy",
            "LifecycleDestroyWindowSeconds",
            3f,
            "Seconds after scene load/unload/active-scene changes where lifecycle Destroy calls are allowed. Values are clamped from 0 to 15.");

        LogBlockedDestroyStackTraceOnce = config.Bind(
            "NetworkObjectDestroy",
            "LogBlockedDestroyStackTraceOnce",
            true,
            "Logs one stack trace for the first blocked spawned ragdoll Destroy call.");

        AudioSourcePlaybackGuardMode = config.Bind(
            "Performance",
            "AudioSourcePlaybackGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs global AudioSource.Play* hooks. Auto is treated as disabled for this global hook. Requires restart.");

        KnownUnityWarningFilterMode = config.Bind(
            "Performance",
            "KnownUnityWarningFilterMode",
            PatchEnableMode.Enabled,
            "Enabled by default to suppress high-frequency Unity log spam for missing audio spatializer plugin setup, BoxCollider negative scale/size asset warnings, disabled AudioSource playback, the SteamValve(Clone) custom-filter AudioSource warning, and duplicate Static Lighting Sky baking warnings. This is log-only and does not repair the underlying audio plugin, collider geometry, AudioSource setup, or lighting setup. It filters only those exact warning prefixes, does not filter Netcode NetworkVariable lifecycle warnings, and requires restart.");

        KnownBepInExLogNoiseFilterMode = config.Bind(
            "Performance",
            "KnownBepInExLogNoiseFilterMode",
            PatchEnableMode.Enabled,
            "Enabled by default to suppress exact known low-value BepInEx log noise from RuntimeIcons, PathfindingLib, and LethalPerformance. Error and exception logs are not filtered. Requires restart.");

        PlayerRagdollGlobalTagGuardMode = config.Bind(
            "Performance",
            "PlayerRagdollGlobalTagGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs global GameObject/Component tag lookup, CompareTag, and tag setter guards. Auto is treated as disabled for these global hooks. The targeted DeadBodyInfo ragdoll guard remains active. Requires restart.");

        ParticleMeshShapeGuardMode = config.Bind(
            "Performance",
            "ParticleMeshShapeGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled starts ParticleSystem mesh shape scans after scene load. Auto is treated as disabled for this scene scan. Requires restart.");

        ParticleMeshShapeGuardDryRun = config.Bind(
            "Performance",
            "ParticleMeshShapeGuardDryRun",
            false,
            "When true, ParticleMeshShapeGuard logs invalid particle mesh shapes without disabling them. Requires restart.");

        EntranceTeleportUpdateGuardMode = config.Bind(
            "EntranceTeleport",
            "EntranceTeleportUpdateGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the EntranceTeleport.Update hot-path replacement. Auto is treated as disabled to avoid per-frame entrance overhead. Requires restart.");

        FindMainEntrancePositionFallbackMode = config.Bind(
            "EntranceTeleport",
            "FindMainEntrancePositionFallbackMode",
            PatchEnableMode.Auto,
            "Auto enables the guarded RoundManager.FindMainEntrancePosition fallback only for the verified game assembly. Disabled preserves vanilla origin fallback.");

        PlayerNearOtherPlayersGuardMode = config.Bind(
            "PlayerControllerB",
            "PlayerNearOtherPlayersGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the PlayerControllerB.NearOtherPlayers hot-path guard. Auto is treated as disabled to avoid per-frame local-player proximity overhead. Requires restart.");

        TerminalAccessibleObjectUpdateGuardMode = config.Bind(
            "TerminalAccessibleObject",
            "TerminalAccessibleObjectUpdateGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the TerminalAccessibleObject.Update hot-path guard. Auto is treated as disabled to avoid per-frame terminal object initialization checks. Requires restart.");

        GameplayEnemyUpdateGuardMode = config.Bind(
            "Performance",
            "GameplayEnemyUpdateGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs enemy Update/LateUpdate hot-path guards for BushWolf, Crawler, DocileLocustBees, and RedLocustBees. Auto is treated as disabled to avoid per-enemy frame overhead. Requires restart.");

        ThrowObjectClientRpcGuardMode = config.Bind(
            "PlayerControllerB",
            "ThrowObjectClientRpcGuardMode",
            PatchEnableMode.Auto,
            "Auto enables the ThrowObjectClientRpc guard only for the verified game assembly. Enabled forces it on; Disabled turns it off.");

        VoiceRefreshFallbackMode = config.Bind(
            "Voice",
            "VoiceRefreshFallbackMode",
            PatchEnableMode.Auto,
            "Auto enables the voice refresh fallback only for the verified game assembly. Enabled forces it on; Disabled turns it off.");

        UnlockableSuitGuardMode = config.Bind(
            "UnlockableSuit",
            "UnlockableSuitGuardMode",
            PatchEnableMode.Auto,
            "Auto enables suit/unlockable sync guards only for the verified game assembly. Enabled forces them on; Disabled turns them off.");

        UnlockableSuitUpdateGuardMode = config.Bind(
            "UnlockableSuit",
            "UnlockableSuitUpdateGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the UnlockableSuit.Update hot-path guard. Auto is treated as disabled to avoid per-suit frame overhead. Requires restart.");

        NetworkObjectParentGuardMode = config.Bind(
            "NetworkObjectParent",
            "NetworkObjectParentGuardMode",
            PatchEnableMode.Auto,
            "Auto suppresses only the known Netcode unspawned reparent SpawnStateException on the verified game assembly. Enabled forces it on; Disabled turns it off.");

        SteamValveDamageTriggerSpawnGuardMode = config.Bind(
            "SteamValve",
            "SteamValveDamageTriggerSpawnGuardMode",
            PatchEnableMode.Disabled,
            "Disabled by default because the Netcode warning \"damageTrigger is disabled! Netcode for GameObjects does not support spawning disabled NetworkBehaviours\" is usually a one-time spawn lifecycle warning, not a performance issue. Only Enabled installs the experimental SteamValveHazard damageTrigger spawn guard, which temporarily activates the inactive damageTrigger InteractTrigger during Netcode spawn and then restores it inactive. Auto is treated as disabled for this guard to avoid changing object activation unless a SteamValve damageTrigger gameplay issue is confirmed. Requires restart.");

        EnemyHealthBarsLateUpdateGuardMode = config.Bind(
            "OptionalCompatibility",
            "EnemyHealthBarsLateUpdateGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the EnemyHealthBars HealthBar.LateUpdate compatibility guard. Auto is treated as disabled to avoid optional-mod per-frame overhead.");

        ShipLootPlusUiHelperGuardMode = config.Bind(
            "OptionalCompatibility",
            "ShipLootPlusUiHelperGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the ShipLootPlus UiHelper compatibility guard. Auto is treated as disabled to avoid optional-mod UI refresh overhead.");

        NightVisionInsideLightingPostfixGuardMode = config.Bind(
            "OptionalCompatibility",
            "NightVisionInsideLightingPostfixGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the ToggleableNightVision InsideLightingPostfix compatibility guard. Auto is treated as disabled to avoid optional-mod lighting update overhead.");

        ChatCommandsStartHostPostfixGuardMode = config.Bind(
            "OptionalCompatibility",
            "ChatCommandsStartHostPostfixGuardMode",
            PatchEnableMode.Auto,
            "Auto enables the ChatCommands StartHost postfix compatibility guard only on a verified game assembly when the expected target signature is present. Enabled forces it on; Disabled turns it off.");

        EnemyAINavMeshGuardMode = config.Bind(
            "EnemyAI.NavMesh",
            "EnemyAINavMeshGuardMode",
            PatchEnableMode.Disabled,
            "Only Enabled installs the EnemyAI.DoAIInterval hot-path guard. Auto is treated as disabled to avoid per-enemy AI tick overhead. Requires restart.");

        EnableEnemyAINavMeshGuard = config.Bind(
            "EnemyAI.NavMesh",
            "EnableEnemyAINavMeshGuard",
            true,
            "Legacy switch for the EnemyAI NavMesh guard. EnemyAINavMeshGuardMode can also disable this patch. Requires restart.");

        AllowEnemyAIWarp = config.Bind(
            "EnemyAI.NavMesh",
            "AllowEnemyAIWarp",
            false,
            "Allows the guard to warp an off-mesh enemy back to a nearby valid NavMesh point.");

        EnemyAINavMeshMaxWarpRadius = config.Bind(
            "EnemyAI.NavMesh",
            "EnemyAINavMeshMaxWarpRadius",
            16f,
            "Maximum radius used when looking for a nearby NavMesh recovery point.");

        EnemyAINavMeshHostServerOnly = config.Bind(
            "EnemyAI.NavMesh",
            "EnemyAINavMeshHostServerOnly",
            true,
            "Only allows active EnemyAI NavMesh recovery on host/server. Non-authority clients only suppress the unsafe tick.");
    }
}
