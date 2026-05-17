using System;
using System.Collections.Generic;
using UnityEngine;

namespace V81ErrorFix;

internal sealed class ParticleMeshShapeGuard : MonoBehaviour
{
    private const int ScanBatchSize = 64;
    private const float ScanFrameBudgetSeconds = 0.0025f;
    private const int MeshInspectionCacheCleanupThreshold = 256;
    private const int MaxFullAreaScanVertices = 32768;
    private const int MaxFullAreaScanIndices = 98304;
    private const int MaxPendingWarningBatches = 64;
    private const int MaxParticleSystemNamesPerWarning = 16;
    private static ParticleMeshShapeGuard _instance;
    private bool _scanRequested;
    private ParticleSystem[] _scanQueue;
    private int _scanIndex;
    private readonly Dictionary<int, ParticleSystem> _patchedParticleSystems = new();
    private readonly WarningLimiter _warnings = new();
    private readonly Dictionary<string, ParticleMeshWarningBatch> _pendingWarnings = new();
    private readonly Dictionary<int, CachedMeshInspection> _meshInspectionCache = new();
    private readonly Dictionary<int, MeshInspectionProgress> _meshInspectionProgress = new();
    private List<Vector3> _meshVertices = new();
    private List<int> _meshTriangles = new();

    internal static void EnsureCreated()
    {
        ParticleMeshShapeGuard existingGuard = FindObjectOfType<ParticleMeshShapeGuard>();
        if (existingGuard != null)
        {
            _instance = existingGuard;
            _instance.RequestSceneScan();
            return;
        }

        GameObject guardObject = new("V81ErrorFix.ParticleMeshShapeGuard");
        DontDestroyOnLoad(guardObject);
        guardObject.hideFlags = HideFlags.HideAndDontSave;
        _instance = guardObject.AddComponent<ParticleMeshShapeGuard>();
        _instance.RequestSceneScan();
    }

    internal static void NotifySceneLoaded()
    {
        if (_instance == null)
        {
            return;
        }

        _instance.RequestSceneScan();
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

        if (!_scanRequested)
        {
            return;
        }

        _scanRequested = false;
        BeginParticleSystemScan();
        ContinueParticleSystemScan();
    }

    private void RequestSceneScan()
    {
        _scanRequested = true;
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
        float scanDeadline = Time.realtimeSinceStartup + ScanFrameBudgetSeconds;
        for (; _scanIndex < endIndex && Time.realtimeSinceStartup < scanDeadline; _scanIndex++)
        {
            TryPatchParticleSystem(_scanQueue[_scanIndex], scanDeadline);
        }

        if (_scanIndex < _scanQueue.Length)
        {
            return;
        }

        _scanQueue = null;
        _scanIndex = 0;
        FlushParticleMeshWarnings();
    }

    private void TryPatchParticleSystem(ParticleSystem particleSystem, float scanDeadline)
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

            MeshInspectionStatus inspectionStatus = GetInvalidMeshReasonCached(mesh, scanDeadline, out string disableReason);
            if (inspectionStatus == MeshInspectionStatus.Incomplete)
            {
                _scanIndex--;
                return;
            }

            if (inspectionStatus == MeshInspectionStatus.Valid)
            {
                return;
            }

            if (!IsDryRun())
            {
                shape.enabled = false;
            }

            _patchedParticleSystems[particleSystemId] = particleSystem;
            QueueParticleMeshWarning(mesh.name, disableReason, particleSystem.name, IsDryRun());
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
        _scanRequested = false;
        _pendingWarnings.Clear();
        _patchedParticleSystems.Clear();
        _meshInspectionCache.Clear();
        _meshInspectionProgress.Clear();
        _warnings.Clear();
    }

    private void QueueParticleMeshWarning(string meshName, string reason, string particleSystemName, bool dryRun = false)
    {
        string key = $"{meshName}|{reason}|{dryRun}";
        if (!_warnings.CanWarn(key))
        {
            return;
        }

        if (!_pendingWarnings.TryGetValue(key, out ParticleMeshWarningBatch warningBatch))
        {
            if (_pendingWarnings.Count >= MaxPendingWarningBatches)
            {
                return;
            }

            warningBatch = new ParticleMeshWarningBatch(meshName, reason, dryRun);
            _pendingWarnings[key] = warningBatch;
        }

        warningBatch.AddParticleSystemName(particleSystemName);
    }

    private void FlushParticleMeshWarnings()
    {
        foreach (KeyValuePair<string, ParticleMeshWarningBatch> pendingWarning in _pendingWarnings)
        {
            ParticleMeshWarningBatch warningBatch = pendingWarning.Value;
            _warnings.Warn(pendingWarning.Key, () =>
            {
                string particleSystemNames = string.Join(", ", warningBatch.ParticleSystemNames);
                if (warningBatch.OmittedParticleSystemCount > 0)
                {
                    particleSystemNames = $"{particleSystemNames}, and {warningBatch.OmittedParticleSystemCount} more";
                }

                string action = warningBatch.DryRun ? "Would disable" : "Disabled";
                return $"{action} mesh shape because mesh '{warningBatch.MeshName}' {warningBatch.Reason} on particle systems: {particleSystemNames}.";
            });
        }
    }

    private MeshInspectionStatus GetInvalidMeshReasonCached(Mesh mesh, float scanDeadline, out string invalidReason)
    {
        invalidReason = null;
        int meshId = mesh.GetInstanceID();
        if (_meshInspectionCache.TryGetValue(meshId, out CachedMeshInspection cachedInspection) && cachedInspection.Mesh == mesh)
        {
            invalidReason = cachedInspection.InvalidReason;
            return cachedInspection.Status;
        }

        MeshInspectionStatus inspectionStatus = GetInvalidMeshReason(mesh, meshId, scanDeadline, out invalidReason);
        if (inspectionStatus == MeshInspectionStatus.Incomplete)
        {
            return inspectionStatus;
        }

        _meshInspectionProgress.Remove(meshId);
        _meshInspectionCache[meshId] = new CachedMeshInspection(mesh, inspectionStatus, invalidReason);
        CleanupMeshInspectionCacheIfNeeded();
        return inspectionStatus;
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

    private MeshInspectionStatus GetInvalidMeshReason(Mesh mesh, int meshId, float scanDeadline, out string invalidReason)
    {
        invalidReason = null;
        if (!mesh.isReadable)
        {
            invalidReason = "is not readable";
            return MeshInspectionStatus.Invalid;
        }

        int vertexCount = mesh.vertexCount;
        int subMeshCount = mesh.subMeshCount;
        if (vertexCount == 0 || subMeshCount <= 0)
        {
            invalidReason = "has zero surface area";
            return MeshInspectionStatus.Invalid;
        }

        try
        {
            bool hasTriangleCandidate = false;
            ulong totalIndexCount = 0;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                uint indexCount = mesh.GetIndexCount(subMeshIndex);
                if (indexCount >= 3)
                {
                    hasTriangleCandidate = true;
                }

                totalIndexCount += indexCount;
            }

            if (!hasTriangleCandidate)
            {
                invalidReason = "has zero surface area";
                return MeshInspectionStatus.Invalid;
            }

            if (vertexCount > MaxFullAreaScanVertices || totalIndexCount > MaxFullAreaScanIndices)
            {
                return MeshInspectionStatus.Valid;
            }

            MeshInspectionProgress progress = null;
            if (_meshInspectionProgress.TryGetValue(meshId, out MeshInspectionProgress cachedProgress)
                && cachedProgress.Mesh == mesh
                && cachedProgress.VertexCount == vertexCount
                && cachedProgress.SubMeshCount == subMeshCount)
            {
                progress = cachedProgress;
            }

            _meshVertices.Clear();
            _meshTriangles.Clear();
            mesh.GetVertices(_meshVertices);
            int startSubMeshIndex = Math.Min(progress?.SubMeshIndex ?? 0, subMeshCount - 1);
            for (int subMeshIndex = startSubMeshIndex; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                _meshTriangles.Clear();
                mesh.GetTriangles(_meshTriangles, subMeshIndex);
                if (_meshTriangles.Count < 3)
                {
                    continue;
                }

                double area = subMeshIndex == startSubMeshIndex ? progress?.Area ?? 0.0 : 0.0;
                int startTriangleIndex = subMeshIndex == startSubMeshIndex ? progress?.TriangleIndex ?? 0 : 0;
                startTriangleIndex -= startTriangleIndex % 3;
                for (int i = startTriangleIndex; i + 2 < _meshTriangles.Count; i += 3)
                {
                    Vector3 a = _meshVertices[_meshTriangles[i]];
                    Vector3 b = _meshVertices[_meshTriangles[i + 1]];
                    Vector3 c = _meshVertices[_meshTriangles[i + 2]];
                    area += Vector3.Cross(b - a, c - a).magnitude * 0.5;
                    if (area > 0.0001)
                    {
                        return MeshInspectionStatus.Valid;
                    }

                    if (Time.realtimeSinceStartup >= scanDeadline)
                    {
                        _meshInspectionProgress[meshId] = new MeshInspectionProgress(
                            mesh,
                            vertexCount,
                            subMeshCount,
                            subMeshIndex,
                            Math.Min(i + 3, _meshTriangles.Count),
                            area);
                        return MeshInspectionStatus.Incomplete;
                    }
                }
            }
        }
        catch
        {
            invalidReason = "could not be inspected safely";
            return MeshInspectionStatus.Invalid;
        }
        finally
        {
            TrimMeshScratchLists();
        }

        invalidReason = "has zero surface area";
        return MeshInspectionStatus.Invalid;
    }

    private void TrimMeshScratchLists()
    {
        const int MaxRetainedCapacity = 65536;
        if (_meshVertices.Capacity > MaxRetainedCapacity)
        {
            _meshVertices = new List<Vector3>();
        }
        else
        {
            _meshVertices.Clear();
        }

        if (_meshTriangles.Capacity > MaxRetainedCapacity)
        {
            _meshTriangles = new List<int>();
        }
        else
        {
            _meshTriangles.Clear();
        }
    }

    private static bool IsDryRun()
    {
        return ErrorFixConfig.ParticleMeshShapeGuardDryRun != null && ErrorFixConfig.ParticleMeshShapeGuardDryRun.Value;
    }

    private sealed class ParticleMeshWarningBatch
    {
        internal readonly string MeshName;
        internal readonly string Reason;
        internal readonly bool DryRun;
        internal readonly HashSet<string> ParticleSystemNames = new();
        internal int OmittedParticleSystemCount;

        internal ParticleMeshWarningBatch(string meshName, string reason, bool dryRun)
        {
            MeshName = meshName;
            Reason = reason;
            DryRun = dryRun;
        }

        internal void AddParticleSystemName(string particleSystemName)
        {
            if (ParticleSystemNames.Count < MaxParticleSystemNamesPerWarning)
            {
                ParticleSystemNames.Add(particleSystemName);
                return;
            }

            if (!ParticleSystemNames.Contains(particleSystemName))
            {
                OmittedParticleSystemCount++;
            }
        }
    }

    private sealed class CachedMeshInspection
    {
        internal readonly Mesh Mesh;
        internal readonly MeshInspectionStatus Status;
        internal readonly string InvalidReason;

        internal CachedMeshInspection(Mesh mesh, MeshInspectionStatus status, string invalidReason)
        {
            Mesh = mesh;
            Status = status;
            InvalidReason = invalidReason;
        }
    }

    private sealed class MeshInspectionProgress
    {
        internal readonly Mesh Mesh;
        internal readonly int VertexCount;
        internal readonly int SubMeshCount;
        internal readonly int SubMeshIndex;
        internal readonly int TriangleIndex;
        internal readonly double Area;

        internal MeshInspectionProgress(Mesh mesh, int vertexCount, int subMeshCount, int subMeshIndex, int triangleIndex, double area)
        {
            Mesh = mesh;
            VertexCount = vertexCount;
            SubMeshCount = subMeshCount;
            SubMeshIndex = subMeshIndex;
            TriangleIndex = triangleIndex;
            Area = area;
        }
    }

    private enum MeshInspectionStatus
    {
        Valid,
        Invalid,
        Incomplete
    }
}
