using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;

namespace V81ErrorFix;

internal static class GameAssemblyIdentity
{
    internal const string VerifiedAssemblySha256 = "5f7db5538b78dc408845a3002907619785ac9f9c6b6059d13dc9a602d9b65731";
    internal const string VerifiedAssemblyMvid = "aca1e98d-6f84-4d3f-85cd-22b6f7be2f9b";

    internal static string CurrentAssemblySha256 { get; private set; }
    internal static string CurrentAssemblyMvid { get; private set; }
    internal static bool IsVerified { get; private set; }

    internal static void Initialize()
    {
        Assembly assembly = typeof(StartOfRound).Assembly;
        CurrentAssemblyMvid = assembly.ManifestModule.ModuleVersionId.ToString();
        string assemblyPath = ResolveAssemblyPath(assembly.Location, assembly.GetName().Name + ".dll", GetManagedPath(), File.Exists);
        CurrentAssemblySha256 = TryComputeSha256(assemblyPath);
        IsVerified = string.Equals(CurrentAssemblyMvid, VerifiedAssemblyMvid, StringComparison.OrdinalIgnoreCase)
            && string.Equals(CurrentAssemblySha256, VerifiedAssemblySha256, StringComparison.OrdinalIgnoreCase);
    }

    internal static string ResolveAssemblyPath(string assemblyLocation, string assemblyFileName, string managedPath, Func<string, bool> fileExists)
    {
        if (!string.IsNullOrEmpty(assemblyLocation) && fileExists(assemblyLocation))
        {
            return assemblyLocation;
        }

        if (!string.IsNullOrEmpty(managedPath) && !string.IsNullOrEmpty(assemblyFileName))
        {
            string managedCandidate = Path.Combine(managedPath, assemblyFileName);
            if (fileExists(managedCandidate))
            {
                return managedCandidate;
            }
        }

        return null;
    }

    private static string GetManagedPath()
    {
        try
        {
            if (!string.IsNullOrEmpty(Paths.ManagedPath))
            {
                return Paths.ManagedPath;
            }
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrEmpty(Paths.GameRootPath))
            {
                return Path.Combine(Paths.GameRootPath, "Lethal Company_Data", "Managed");
            }
        }
        catch
        {
        }

        return null;
    }

    private static string TryComputeSha256(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Could not compute Assembly-CSharp SHA256: {ex.GetType().Name}.");
            return null;
        }
    }
}
