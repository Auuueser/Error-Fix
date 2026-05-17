using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;

namespace V81ErrorFix;

internal static class RpcExecStageUtility
{
    // Keep FieldInfo access centralized. Harmony FieldRefAccess would require binding to
    // Netcode's non-public enum field type, which is more version-fragile than this small
    // reflection boundary; these checks only run on guarded RPC paths, not every frame.
    private static readonly FieldInfo RpcExecStageField = AccessTools.Field(typeof(NetworkBehaviour), "__rpc_exec_stage");
    private static bool _initialized;
    private static bool _ready;
    private static object _executeStage;
    private static object _sendStage;

    internal static bool TryIsExecuting(NetworkBehaviour behaviour, out bool isExecuting)
    {
        isExecuting = false;
        if (behaviour == null || !EnsureReady())
        {
            return false;
        }

        object currentStage = RpcExecStageField.GetValue(behaviour);
        isExecuting = Equals(currentStage, _executeStage);
        return true;
    }

    internal static bool TryGetSendStage(out object sendStage)
    {
        sendStage = null;
        if (!EnsureReady())
        {
            return false;
        }

        sendStage = _sendStage;
        return true;
    }

    internal static bool TryGetStage(NetworkBehaviour behaviour, out object stage)
    {
        stage = null;
        if (behaviour == null || !EnsureReady())
        {
            return false;
        }

        stage = RpcExecStageField.GetValue(behaviour);
        return true;
    }

    internal static bool TrySetStage(NetworkBehaviour behaviour, object stage)
    {
        if (behaviour == null || stage == null || !EnsureReady())
        {
            return false;
        }

        RpcExecStageField.SetValue(behaviour, stage);
        return true;
    }

    internal static bool ShouldAllowClientRpcSuppression(NetworkBehaviour behaviour, string targetKey, Exception exception, WarningLimiter warnings)
    {
        if (TryIsExecuting(behaviour, out bool isExecuting) && isExecuting)
        {
            return true;
        }

        warnings?.Warn($"{targetKey}|not-execute-stage", () => $"Returning original {targetKey} {exception?.GetType().Name ?? "exception"} because the generated RPC stage was not confirmed Execute; send-stage RPC exceptions must not be suppressed. First stack fingerprint: {Fingerprint(exception)}.");
        return false;
    }

    private static bool EnsureReady()
    {
        if (_initialized)
        {
            return _ready;
        }

        _initialized = true;
        Type stageType = RpcExecStageField?.FieldType;
        _ready = TryParseEnumValue(stageType, "Execute", out _executeStage)
            && TryParseEnumValue(stageType, "Send", out _sendStage);
        if (!_ready)
        {
            Plugin.Log?.LogWarning("RPC stage utility disabled because NetworkBehaviour.__rpc_exec_stage or expected enum values were not found.");
        }

        return _ready;
    }

    internal static bool TryParseEnumValue(Type enumType, string name, out object value)
    {
        value = null;
        if (enumType == null || string.IsNullOrEmpty(name) || !enumType.IsEnum)
        {
            return false;
        }

        try
        {
            if (!Enum.IsDefined(enumType, name))
            {
                return false;
            }

            value = Enum.Parse(enumType, name);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static string Fingerprint(Exception exception)
    {
        string stackTrace = exception?.StackTrace;
        if (!string.IsNullOrEmpty(stackTrace))
        {
            string[] stackLines = stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            if (stackLines.Length > 0)
            {
                return stackLines[0];
            }
        }

        return exception?.GetType().Name ?? "unknown";
    }
}
