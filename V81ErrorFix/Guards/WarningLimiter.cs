using System;
using System.Collections.Generic;

namespace V81ErrorFix;

internal sealed class WarningLimiter
{
    private const string OverflowKey = "__overflow__";
    private static readonly List<WarningLimiter> SceneScopedLimiters = new();
    private readonly int _maxWarnings;
    private readonly int _maxKeyCount;
    private readonly Dictionary<string, int> _warningCounts = new();

    internal WarningLimiter(int maxWarnings = 5, int maxKeyCount = 512, bool clearOnSceneChange = true)
    {
        _maxWarnings = maxWarnings;
        _maxKeyCount = Math.Max(1, maxKeyCount);
        if (clearOnSceneChange)
        {
            SceneScopedLimiters.Add(this);
        }
    }

    internal void Warn(string key, string message)
    {
        if (!TryIncrement(key, out int warningCount))
        {
            return;
        }

        Plugin.Log?.LogWarning($"{message} ({warningCount}/{_maxWarnings})");
    }

    internal void Warn(string key, Func<string> messageFactory)
    {
        if (!TryIncrement(key, out int warningCount))
        {
            return;
        }

        Plugin.Log?.LogWarning($"{messageFactory()} ({warningCount}/{_maxWarnings})");
    }

    internal bool CanWarn(string key)
    {
        _warningCounts.TryGetValue(GetEffectiveKeyForRead(NormalizeKey(key)), out int warningCount);
        return warningCount < _maxWarnings;
    }

    internal int KeyCount()
    {
        return _warningCounts.Count;
    }

    internal void Clear()
    {
        _warningCounts.Clear();
    }

    internal void ClearPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || _warningCounts.Count == 0)
        {
            return;
        }

        List<string> keysToRemove = null;
        foreach (string key in _warningCounts.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            keysToRemove ??= new List<string>();
            keysToRemove.Add(key);
        }

        if (keysToRemove == null)
        {
            return;
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            _warningCounts.Remove(keysToRemove[i]);
        }
    }

    internal static void ClearSceneScopedLimiters()
    {
        for (int i = 0; i < SceneScopedLimiters.Count; i++)
        {
            SceneScopedLimiters[i]?.Clear();
        }
    }

    private bool TryIncrement(string key, out int warningCount)
    {
        key = NormalizeKey(key);
        key = ApplyKeyLimit(key);
        _warningCounts.TryGetValue(key, out warningCount);
        if (warningCount >= _maxWarnings)
        {
            return false;
        }

        warningCount++;
        _warningCounts[key] = warningCount;
        return true;
    }

    private string ApplyKeyLimit(string incomingKey)
    {
        if (_warningCounts.ContainsKey(incomingKey) || _warningCounts.Count < _maxKeyCount)
        {
            return incomingKey;
        }

        if (_warningCounts.ContainsKey(OverflowKey) || _warningCounts.Count < _maxKeyCount + 1)
        {
            return OverflowKey;
        }

        string keyToRemove = null;
        foreach (string key in _warningCounts.Keys)
        {
            if (key == OverflowKey)
            {
                continue;
            }

            keyToRemove = key;
            break;
        }

        if (keyToRemove != null)
        {
            _warningCounts.Remove(keyToRemove);
        }

        return OverflowKey;
    }

    private string GetEffectiveKeyForRead(string incomingKey)
    {
        if (_warningCounts.ContainsKey(incomingKey) || _warningCounts.Count < _maxKeyCount)
        {
            return incomingKey;
        }

        return OverflowKey;
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrEmpty(key) ? "unknown" : key;
    }
}
