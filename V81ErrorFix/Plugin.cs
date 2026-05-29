using BepInEx;
using HarmonyLib;
using System;
using System.Linq;

namespace V81ErrorFix;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = VersionInfo.PluginGuid;
    public const string PluginName = VersionInfo.PluginName;
    public const string PluginVersion = VersionInfo.PluginVersion;

    internal static BepInEx.Logging.ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        PatchEnableMode runtimePatchMode = ErrorFixConfig.BindRuntimePatchMode(Config).Value;
        if (runtimePatchMode == PatchEnableMode.Disabled)
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded passive. RuntimePatchMode=Disabled; no Harmony patches, scene lifecycle hooks, extra config bindings, or Assembly-CSharp verification.");
            return;
        }

        GameAssemblyIdentity.Initialize();
        if (!ShouldInstallRuntimePatches(runtimePatchMode, GameAssemblyIdentity.IsVerified))
        {
            Logger.LogInfo(
                $"{PluginName} {PluginVersion} loaded passive. RuntimePatchMode={runtimePatchMode}; " +
                $"no Harmony patches or scene lifecycle hooks installed. Assembly verified: {GameAssemblyIdentity.IsVerified}; " +
                $"MVID: {GameAssemblyIdentity.CurrentAssemblyMvid}; SHA256: {GameAssemblyIdentity.CurrentAssemblySha256 ?? "unknown"}.");
            return;
        }

        ErrorFixConfig.Bind(Config);
        SceneLifecycle.Register();
        PatchAllWithIsolation(new Harmony(PluginGuid));
        if (PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.ParticleMeshShapeGuardMode))
        {
            ParticleMeshShapeGuard.EnsureCreated();
        }

        Logger.LogInfo(
            $"Performance-sensitive guards: AudioSource={ErrorFixConfig.AudioSourcePlaybackGuardMode.Value}; " +
            $"KnownUnityWarningFilter={ErrorFixConfig.KnownUnityWarningFilterMode.Value}; " +
            $"KnownBepInExLogNoiseFilter={ErrorFixConfig.KnownBepInExLogNoiseFilterMode.Value}; " +
            $"PlayerRagdollGlobalTag={ErrorFixConfig.PlayerRagdollGlobalTagGuardMode.Value}; " +
            $"ParticleMeshShape={ErrorFixConfig.ParticleMeshShapeGuardMode.Value}; " +
            $"PlayerNearOtherPlayers={ErrorFixConfig.PlayerNearOtherPlayersGuardMode.Value}; " +
            $"TerminalAccessibleObjectUpdate={ErrorFixConfig.TerminalAccessibleObjectUpdateGuardMode.Value}; " +
            $"EntranceTeleportUpdate={ErrorFixConfig.EntranceTeleportUpdateGuardMode.Value}; " +
            $"GameplayEnemyUpdate={ErrorFixConfig.GameplayEnemyUpdateGuardMode.Value}; " +
            $"UnlockableSuitUpdate={ErrorFixConfig.UnlockableSuitUpdateGuardMode.Value}; " +
            $"EnemyAINavMesh={ErrorFixConfig.EnemyAINavMeshGuardMode.Value} " +
            $"(installed={EnemyAINavMeshGuardPatch.ShouldPatch(ErrorFixConfig.EnemyAINavMeshGuardMode.Value, ErrorFixConfig.EnableEnemyAINavMeshGuard.Value)}); " +
            $"GlobalDestroy={ErrorFixConfig.GlobalDestroyGuardMode.Value} " +
            $"(installed={NetworkObjectDestroyGuardPatch.ShouldPatch(ErrorFixConfig.GlobalDestroyGuardMode.Value, ErrorFixConfig.EnableGlobalDestroyGuard.Value)}).");
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Assembly verified: {GameAssemblyIdentity.IsVerified}; MVID: {GameAssemblyIdentity.CurrentAssemblyMvid}; SHA256: {GameAssemblyIdentity.CurrentAssemblySha256 ?? "unknown"}.");
    }

    internal static bool ShouldInstallRuntimePatches(PatchEnableMode mode, bool isVerifiedAssembly)
    {
        return mode == PatchEnableMode.Enabled || (mode == PatchEnableMode.Auto && isVerifiedAssembly);
    }

    private void PatchAllWithIsolation(Harmony harmony)
    {
        int processedCount = 0;
        int failedCount = 0;
        foreach (Type patchType in typeof(Plugin).Assembly.GetTypes().Where(HasHarmonyPatchAttribute).OrderBy(type => type.FullName))
        {
            try
            {
                harmony.CreateClassProcessor(patchType).Patch();
                processedCount++;
            }
            catch (HarmonyException ex)
            {
                failedCount++;
                Logger.LogError($"Disabled Harmony patch class {patchType.FullName} because Harmony failed to install it: {ex.Message}");
            }
            catch (Exception ex)
            {
                failedCount++;
                Logger.LogError($"Disabled Harmony patch class {patchType.FullName} because it failed to install: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Logger.LogInfo($"Harmony patch classes processed with isolation: {processedCount} succeeded, {failedCount} failed.");
    }

    private static bool HasHarmonyPatchAttribute(Type type)
    {
        return type != null && type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0;
    }
}
