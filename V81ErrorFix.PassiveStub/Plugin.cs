using BepInEx;

namespace V81ErrorFix;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "codex.v81errorfix";
    private const string PluginName = "V81 Error Fix";
    private const string PluginVersion = "0.0.4";

    private void Awake()
    {
        Logger.LogInfo("V81 Error Fix 0.0.4 passive stub loaded. No config binding, game assembly references, Harmony references, runtime patches, or scene hooks.");
    }
}
