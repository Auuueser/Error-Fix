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
        ErrorFixConfig.Bind(Config);
        GameAssemblyIdentity.Initialize();
        SceneLifecycle.Register();
        PatchAllWithIsolation(new Harmony(PluginGuid));
        if (PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.ParticleMeshShapeGuardMode))
        {
            ParticleMeshShapeGuard.EnsureCreated();
        }

        Logger.LogInfo(
            $"Performance-sensitive guards: AudioSource={ErrorFixConfig.AudioSourcePlaybackGuardMode.Value}; " +
            $"KnownUnityWarningFilter={ErrorFixConfig.KnownUnityWarningFilterMode.Value}; " +
            $"PlayerRagdollGlobalTag={ErrorFixConfig.PlayerRagdollGlobalTagGuardMode.Value}; " +
            $"ParticleMeshShape={ErrorFixConfig.ParticleMeshShapeGuardMode.Value}; " +
            $"GlobalDestroy={ErrorFixConfig.GlobalDestroyGuardMode.Value} " +
            $"(installed={NetworkObjectDestroyGuardPatch.ShouldPatch(ErrorFixConfig.GlobalDestroyGuardMode.Value, ErrorFixConfig.EnableGlobalDestroyGuard.Value)}).");
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Assembly verified: {GameAssemblyIdentity.IsVerified}; MVID: {GameAssemblyIdentity.CurrentAssemblyMvid}; SHA256: {GameAssemblyIdentity.CurrentAssemblySha256 ?? "unknown"}.");
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
