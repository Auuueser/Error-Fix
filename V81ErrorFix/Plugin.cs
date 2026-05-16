using BepInEx;
using HarmonyLib;

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
        Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginGuid);
        if (PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.ParticleMeshShapeGuardMode))
        {
            ParticleMeshShapeGuard.EnsureCreated();
        }

        Logger.LogInfo(
            $"Performance-sensitive guards: AudioSource={ErrorFixConfig.AudioSourcePlaybackGuardMode.Value}; " +
            $"PlayerRagdollGlobalTag={ErrorFixConfig.PlayerRagdollGlobalTagGuardMode.Value}; " +
            $"ParticleMeshShape={ErrorFixConfig.ParticleMeshShapeGuardMode.Value}; " +
            $"GlobalDestroy={ErrorFixConfig.GlobalDestroyGuardMode.Value} " +
            $"(installed={NetworkObjectDestroyGuardPatch.ShouldPatch(ErrorFixConfig.GlobalDestroyGuardMode.Value, ErrorFixConfig.EnableGlobalDestroyGuard.Value, GameAssemblyIdentity.IsVerified)}).");
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Assembly verified: {GameAssemblyIdentity.IsVerified}; MVID: {GameAssemblyIdentity.CurrentAssemblyMvid}; SHA256: {GameAssemblyIdentity.CurrentAssemblySha256 ?? "unknown"}.");
    }
}
