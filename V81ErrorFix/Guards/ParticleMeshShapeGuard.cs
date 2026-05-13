using System;
using System.Collections.Generic;
using UnityEngine;

namespace V81ErrorFix;

internal sealed class ParticleMeshShapeGuard : MonoBehaviour
{
    private const float ScanInterval = 5f;
    private const int ScanBatchSize = 64;
    private const int MeshInspectionCacheCleanupThreshold = 256;
    private static ParticleMeshShapeGuard _instance;
    private float _nextScanTime;
    private ParticleSystem[] _scanQueue;
    private int _scanIndex;
    private readonly Dictionary<int, ParticleSystem> _patchedParticleSystems = new();
    private readonly WarningLimiter _warnings = new();
    private readonly Dictionary<string, ParticleMeshWarningBatch> _pendingWarnings = new();
    private readonly Dictionary<int, CachedMeshInspection> _meshInspectionCache = new();

    internal static void EnsureCreated()
    {
        ParticleMeshShapeGuard existingGuard = FindObjectOfType<ParticleMeshShapeGuard>();
        if (existingGuard != null)
        {
            _instance = existingGuard;
            return;
        }

        GameObject guardObject = new("V81ErrorFix.ParticleMeshShapeGuard");
        DontDestroyOnLoad(guardObject);
        guardObject.hideFlags = HideFlags.HideAndDontSave;
        _instance = guardObject.AddComponent<ParticleMeshShapeGuard>();
    }

    internal static void NotifySceneLoaded()
    {
        if (_instance == null)
        {
            return;
        }

        _instance._nextScanTime = 0f;
    }

    internal static void NotifySceneUnloaded()
    {
        if (_instance == null)
        {
            return;
        }

        _instance.ClearSceneCaches();
    }

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        if (_scanQueue != null)
        {
            ContinueParticleSystemScan();
            return;
        }

        if (Time.realtimeSinceStartup < _nextScanTime)
        {
            return;
        }

        _nextScanTime = Time.realtimeSinceStartup + ScanInterval;
        BeginParticleSystemScan();
        ContinueParticleSystemScan();
    }

    private void BeginParticleSystemScan()
    {
        _pendingWarnings.Clear();
        _scanQueue = FindObjectsOfType<ParticleSystem>(includeInactive: true);
        _scanIndex = 0;
    }

    private void ContinueParticleSystemScan()
    {
        if (_scanQueue == null)
        {
            return;
        }

        int endIndex = Math.Min(_scanIndex + ScanBatchSize, _scanQueue.Length);
        for (; _scanIndex < endIndex; _scanIndex++)
        {
            TryPatchParticleSystem(_scanQueue[_scanIndex]);
        }

        if (_scanIndex < _scanQueue.Length)
        {
            return;
        }

        _scanQueue = null;
        _scanIndex = 0;
        FlushParticleMeshWarnings();
    }

    private void TryPatchParticleSystem(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        int particleSystemId = particleSystem.GetInstanceID();
        if (_patchedParticleSystems.TryGetValue(particleSystemId, out ParticleSystem patchedParticleSystem) && patchedParticleSystem == particleSystem)
        {
            return;
        }

        try
        {
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            if (!shape.enabled || !TryGetShapeMesh(shape, out Mesh mesh) || mesh == null)
            {
                return;
            }

            string disableReason = GetInvalidMeshReasonCached(mesh);
            if (disableReason == null)
            {
                return;
            }

            shape.enabled = false;
            _patchedParticleSystems[particleSystemId] = particleSystem;
            QueueParticleMeshWarning(mesh.name, disableReason, particleSystem.name);
        }
        catch (Exception ex)
        {
            _patchedParticleSystems[particleSystemId] = particleSystem;
            QueueParticleMeshWarning("inspection", $"could not be inspected safely: {ex.GetType().Name}", particleSystem.name);
        }
    }

    private void ClearSceneCaches()
    {
        _scanQueue = null;
        _scanIndex = 0;
        _pendingWarnings.Clear();
        _patchedParticleSystems.Clear();
        _meshInspectionCache.Clear();
        _warnings.Clear();
    }

    private void QueueParticleMeshWarning(string meshName, string reason, string particleSystemName)
    {
        string key = $"{meshName}|{reason}";
        if (!_warnings.CanWarn(key))
        {
            return;
        }

        if (!_pendingWarnings.TryGetValue(key, out ParticleMeshWarningBatch warningBatch))
        {
            warningBatch = new ParticleMeshWarningBatch(meshName, reason);
            _pendingWarnings[key] = warningBatch;
        }

        warningBatch.ParticleSystemNames.Add(particleSystemName);
    }

    private void FlushParticleMeshWarnings()
    {
        foreach (KeyValuePair<string, ParticleMeshWarningBatch> pendingWarning in _pendingWarnings)
        {
            ParticleMeshWarningBatch warningBatch = pendingWarning.Value;
            _warnings.Warn(pendingWarning.Key, () =>
            {
                string particleSystemNames = string.Join(", ", warningBatch.ParticleSystemNames);
                return $"Disabled mesh shape because mesh '{warningBatch.MeshName}' {warningBatch.Reason} on particle systems: {particleSystemNames}.";
            });
        }
    }

    private string GetInvalidMeshReasonCached(Mesh mesh)
    {
        int meshId = mesh.GetInstanceID();
        if (_meshInspectionCache.TryGetValue(meshId, out CachedMeshInspection cachedInspection) && cachedInspection.Mesh == mesh)
        {
            return cachedInspection.InvalidReason;
        }

        string invalidReason = GetInvalidMeshReason(mesh);
        _meshInspectionCache[meshId] = new CachedMeshInspection(mesh, invalidReason);
        CleanupMeshInspectionCacheIfNeeded();
        return invalidReason;
    }

    private void CleanupMeshInspectionCacheIfNeeded()
    {
        if (_meshInspectionCache.Count < MeshInspectionCacheCleanupThreshold)
        {
            return;
        }

        List<int> staleMeshIds = null;
        foreach (KeyValuePair<int, CachedMeshInspection> cachedInspection in _meshInspectionCache)
        {
            if (cachedInspection.Value.Mesh != null)
            {
                continue;
            }

            staleMeshIds ??= new List<int>();
            staleMeshIds.Add(cachedInspection.Key);
        }

        if (staleMeshIds == null)
        {
            return;
        }

        for (int i = 0; i < staleMeshIds.Count; i++)
        {
            _meshInspectionCache.Remove(staleMeshIds[i]);
        }
    }

    private static bool TryGetShapeMesh(ParticleSystem.ShapeModule shape, out Mesh mesh)
    {
        mesh = null;
        switch (shape.shapeType)
        {
            case ParticleSystemShapeType.Mesh:
                mesh = shape.mesh;
                return mesh != null;
            case ParticleSystemShapeType.MeshRenderer:
                mesh = shape.meshRenderer != null ? shape.meshRenderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                return mesh != null;
            case ParticleSystemShapeType.SkinnedMeshRenderer:
                mesh = shape.skinnedMeshRenderer != null ? shape.skinnedMeshRenderer.sharedMesh : null;
                return mesh != null;
            default:
                return false;
        }
    }

    private static string GetInvalidMeshReason(Mesh mesh)
    {
        if (!mesh.isReadable)
        {
            return "is not readable";
        }

        try
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length < 3)
            {
                return "has zero surface area";
            }

            double area = 0.0;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5;
                if (area > 0.0001)
                {
                    return null;
                }
            }
        }
        catch
        {
            return "could not be inspected safely";
        }

        return "has zero surface area";
    }

    private sealed class ParticleMeshWarningBatch
    {
        internal readonly string MeshName;
        internal readonly string Reason;
        internal readonly HashSet<string> ParticleSystemNames = new();

        internal ParticleMeshWarningBatch(string meshName, string reason)
        {
            MeshName = meshName;
            Reason = reason;
        }
    }

    private sealed class CachedMeshInspection
    {
        internal readonly Mesh Mesh;
        internal readonly string InvalidReason;

        internal CachedMeshInspection(Mesh mesh, string invalidReason)
        {
            Mesh = mesh;
            InvalidReason = invalidReason;
        }
    }
}
