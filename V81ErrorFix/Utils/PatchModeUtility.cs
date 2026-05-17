using BepInEx.Configuration;

namespace V81ErrorFix;

internal static class PatchModeUtility
{
    internal static bool IsEnabled(ConfigEntry<PatchEnableMode> modeEntry, bool requireVerifiedAssembly = true)
    {
        return IsEnabled(modeEntry, PatchEnableMode.Auto, requireVerifiedAssembly);
    }

    internal static bool IsEnabled(ConfigEntry<PatchEnableMode> modeEntry, PatchEnableMode defaultMode, bool requireVerifiedAssembly = true)
    {
        PatchEnableMode mode = modeEntry?.Value ?? defaultMode;
        return IsEnabled(mode, GameAssemblyIdentity.IsVerified, requireVerifiedAssembly);
    }

    internal static bool IsEnabled(PatchEnableMode mode, bool isVerifiedAssembly, bool requireVerifiedAssembly = true)
    {
        return mode == PatchEnableMode.Enabled || (mode == PatchEnableMode.Auto && (!requireVerifiedAssembly || isVerifiedAssembly));
    }

    internal static bool IsExplicitlyEnabled(ConfigEntry<PatchEnableMode> modeEntry, PatchEnableMode defaultMode = PatchEnableMode.Disabled)
    {
        return (modeEntry?.Value ?? defaultMode) == PatchEnableMode.Enabled;
    }
}
