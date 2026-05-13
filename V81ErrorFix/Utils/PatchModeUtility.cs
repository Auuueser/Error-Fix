using BepInEx.Configuration;

namespace V81ErrorFix;

internal static class PatchModeUtility
{
    internal static bool IsEnabled(ConfigEntry<PatchEnableMode> modeEntry, bool requireVerifiedAssembly = true)
    {
        PatchEnableMode mode = modeEntry?.Value ?? PatchEnableMode.Auto;
        return mode == PatchEnableMode.Enabled || (mode == PatchEnableMode.Auto && (!requireVerifiedAssembly || GameAssemblyIdentity.IsVerified));
    }
}
