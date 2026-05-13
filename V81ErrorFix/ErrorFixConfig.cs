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
    internal static ConfigEntry<bool> EnableGlobalDestroyGuard;
    internal static ConfigEntry<PatchEnableMode> GlobalDestroyGuardMode;
    internal static ConfigEntry<bool> AllowDestroyDuringSceneUnload;
    internal static ConfigEntry<float> LifecycleDestroyWindowSeconds;
    internal static ConfigEntry<bool> LogBlockedDestroyStackTraceOnce;
    internal static ConfigEntry<PatchEnableMode> EntranceTeleportUpdateGuardMode;
    internal static ConfigEntry<PatchEnableMode> ThrowObjectClientRpcGuardMode;
    internal static ConfigEntry<PatchEnableMode> VoiceRefreshFallbackMode;
    internal static ConfigEntry<PatchEnableMode> UnlockableSuitGuardMode;
    internal static ConfigEntry<bool> EnableEnemyAINavMeshGuard;
    internal static ConfigEntry<bool> AllowEnemyAIWarp;
    internal static ConfigEntry<float> EnemyAINavMeshMaxWarpRadius;
    internal static ConfigEntry<bool> EnemyAINavMeshHostServerOnly;

    internal static void Bind(ConfigFile config)
    {
        EnableGlobalDestroyGuard = config.Bind(
            "NetworkObjectDestroy",
            "EnableGlobalDestroyGuard",
            true,
            "Legacy switch for the global Destroy guard. GlobalDestroyGuardMode can also disable this patch.");

        GlobalDestroyGuardMode = config.Bind(
            "NetworkObjectDestroy",
            "GlobalDestroyGuardMode",
            PatchEnableMode.Auto,
            "Auto enables the spawned ragdoll Destroy guard only for the verified game assembly. Enabled forces it on; Disabled turns it off.");

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

        EntranceTeleportUpdateGuardMode = config.Bind(
            "EntranceTeleport",
            "EntranceTeleportUpdateGuardMode",
            PatchEnableMode.Auto,
            "Auto enables the guarded EntranceTeleport.Update replacement only for the verified game assembly.");

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

        EnableEnemyAINavMeshGuard = config.Bind(
            "EnemyAI.NavMesh",
            "EnableEnemyAINavMeshGuard",
            true,
            "Suppresses known EnemyAI SetDestination errors when an agent is off the NavMesh.");

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
