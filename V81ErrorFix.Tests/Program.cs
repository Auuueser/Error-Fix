using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private static int Main()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveGameAssembly;
            TestGameAssemblyPathFallback();
            TestLogNoiseFilters();
            TestRuntimePatchModeGate();
            TestEnemyAINavMeshGuardPatchInstallGate();
            TestHotPathPatchInstallGates();
            Console.WriteLine("All helper tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void TestEnemyAINavMeshGuardPatchInstallGate()
    {
        Type patchType = GetType("V81ErrorFix.EnemyAINavMeshGuardPatch");
        Type modeType = GetType("V81ErrorFix.PatchEnableMode");
        object auto = Enum.Parse(modeType, "Auto");
        object enabled = Enum.Parse(modeType, "Enabled");
        object disabled = Enum.Parse(modeType, "Disabled");

        AssertEqual(false, InvokeStatic<bool>(patchType, "ShouldPatch", auto, true), "Auto should not install EnemyAI hot-path guard");
        AssertEqual(false, InvokeStatic<bool>(patchType, "ShouldPatch", disabled, true), "Disabled should not install EnemyAI hot-path guard");
        AssertEqual(false, InvokeStatic<bool>(patchType, "ShouldPatch", enabled, false), "Legacy switch should still disable EnemyAI hot-path guard");
        AssertEqual(true, InvokeStatic<bool>(patchType, "ShouldPatch", enabled, true), "Enabled should install EnemyAI hot-path guard");
    }

    private static void TestRuntimePatchModeGate()
    {
        Type pluginType = GetType("V81ErrorFix.Plugin");
        Type modeType = GetType("V81ErrorFix.PatchEnableMode");
        object auto = Enum.Parse(modeType, "Auto");
        object enabled = Enum.Parse(modeType, "Enabled");
        object disabled = Enum.Parse(modeType, "Disabled");

        AssertEqual(false, InvokeStatic<bool>(pluginType, "ShouldInstallRuntimePatches", disabled, true), "Disabled RuntimePatchMode should install no runtime patches");
        AssertEqual(false, InvokeStatic<bool>(pluginType, "ShouldInstallRuntimePatches", auto, false), "Auto RuntimePatchMode should require verified assembly");
        AssertEqual(true, InvokeStatic<bool>(pluginType, "ShouldInstallRuntimePatches", auto, true), "Auto RuntimePatchMode should install on verified assembly");
        AssertEqual(true, InvokeStatic<bool>(pluginType, "ShouldInstallRuntimePatches", enabled, false), "Enabled RuntimePatchMode should force runtime patches on");
    }

    private static void TestGameAssemblyPathFallback()
    {
        Type type = GetType("V81ErrorFix.GameAssemblyIdentity");
        string managedPath = Path.Combine("D:\\Games", "Lethal Company", "Lethal Company_Data", "Managed");
        string fallbackPath = Path.Combine(managedPath, "Assembly-CSharp.dll");
        string directPath = Path.Combine("C:\\Direct", "Assembly-CSharp.dll");
        Func<string, bool> exists = path =>
            string.Equals(path, fallbackPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, directPath, StringComparison.OrdinalIgnoreCase);

        AssertEqual(fallbackPath, InvokeStatic<string>(type, "ResolveAssemblyPath", string.Empty, "Assembly-CSharp.dll", managedPath, exists), "Assembly path should fall back to managed path when Assembly.Location is empty");
        AssertEqual(directPath, InvokeStatic<string>(type, "ResolveAssemblyPath", directPath, "Assembly-CSharp.dll", managedPath, exists), "Assembly path should prefer existing Assembly.Location");
        AssertEqual(null, InvokeStatic<string>(type, "ResolveAssemblyPath", string.Empty, "Assembly-CSharp.dll", string.Empty, exists), "Assembly path should return null when no candidate exists");
    }

    private static void TestLogNoiseFilters()
    {
        Type bepInExFilterType = GetType("V81ErrorFix.BepInExKnownLogNoiseFilterPatch");
        AssertEqual(true, InvokeStatic<bool>(bepInExFilterType, "ShouldSuppressForTest", "RuntimeIcons", "Debug", "[debit_card_debit-RuntimeIcons_BetterRotations] Overriding Vanilla/Key with priority 0"), "RuntimeIcons override debug noise should be suppressed");
        AssertEqual(true, InvokeStatic<bool>(bepInExFilterType, "ShouldSuppressForTest", "LethalPerformance", "Warning", "EntranceTeleport search called with inactive objects, probably will cause incompatibility!"), "LethalPerformance inactive search warning should be suppressed");
        AssertEqual(true, InvokeStatic<bool>(bepInExFilterType, "ShouldSuppressForTest", "LethalPerformance.Patcher", "Info", "Saved 1 config(s)"), "LethalPerformance config save info should be suppressed");
        AssertEqual(false, InvokeStatic<bool>(bepInExFilterType, "ShouldSuppressForTest", "RuntimeIcons", "Warning", "Key foo does not exist"), "RuntimeIcons warnings should stay visible");
        AssertEqual(false, InvokeStatic<bool>(bepInExFilterType, "ShouldSuppressForTest", "V81 Error Fix", "Info", "important"), "Own plugin logs should stay visible");

        Type unityFilterType = GetType("V81ErrorFix.UnityKnownWarningFilterPatch");
        AssertEqual(true, InvokeStatic<bool>(unityFilterType, "ShouldFilterMessageForTest", "Can not play a disabled audio source"), "Disabled AudioSource Unity warning should be suppressible");
    }

    private static void TestHotPathPatchInstallGates()
    {
        AssertHotPathGate("V81ErrorFix.PlayerControllerBNearOtherPlayersPatch");
        AssertHotPathGate("V81ErrorFix.TerminalAccessibleObjectUpdatePatch");
        AssertHotPathGate("V81ErrorFix.EntranceTeleportUpdatePatch");
        AssertHotPathGate("V81ErrorFix.GameplayEnemyUpdatePatchGate");
        AssertHotPathGate("V81ErrorFix.UnlockableSuitUpdatePatch");
    }

    private static void AssertHotPathGate(string fullName)
    {
        Type patchType = GetType(fullName);
        Type modeType = GetType("V81ErrorFix.PatchEnableMode");
        object auto = Enum.Parse(modeType, "Auto");
        object enabled = Enum.Parse(modeType, "Enabled");
        object disabled = Enum.Parse(modeType, "Disabled");

        AssertEqual(false, InvokeStatic<bool>(patchType, "ShouldPatch", auto, false), $"{fullName} Auto should not install hot-path guard when assembly is unverified");
        AssertEqual(false, InvokeStatic<bool>(patchType, "ShouldPatch", auto, true), $"{fullName} Auto should not install hot-path guard even when assembly is verified");
        AssertEqual(false, InvokeStatic<bool>(patchType, "ShouldPatch", disabled, true), $"{fullName} Disabled should not install hot-path guard");
        AssertEqual(true, InvokeStatic<bool>(patchType, "ShouldPatch", enabled, false), $"{fullName} Enabled should install hot-path guard");
    }

    private static Type GetType(string fullName)
    {
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "V81ErrorFix") ?? Assembly.Load("V81ErrorFix");
        return assembly.GetType(fullName, throwOnError: true);
    }

    private static T InvokeStatic<T>(Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
        if (method == null)
        {
            throw new MissingMethodException(type.FullName, methodName);
        }

        return (T)method.Invoke(null, args);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!object.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}. Expected: {expected}, Actual: {actual}");
        }
    }

    private static Assembly ResolveGameAssembly(object sender, ResolveEventArgs args)
    {
        string assemblyFileName = new AssemblyName(args.Name).Name + ".dll";
        string lethalCompanyDir = Environment.GetEnvironmentVariable("LETHAL_COMPANY_DIR") ?? @"D:\Steam\steamapps\common\Lethal Company";
        string candidate = Path.Combine(lethalCompanyDir, "Lethal Company_Data", "Managed", assemblyFileName);
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }
}
