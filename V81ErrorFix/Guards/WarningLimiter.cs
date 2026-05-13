using System;
using System.Collections.Generic;

namespace V81ErrorFix;

internal sealed class WarningLimiter
{
    private readonly int _maxWarnings;
    private readonly int _maxKeyCount;
    private readonly Dictionary<string, int> _warningCounts = new();

    internal WarningLimiter(int maxWarnings = 5, int maxKeyCount = 512)
    {
        _maxWarnings = maxWarnings;
        _maxKeyCount = Math.Max(1, maxKeyCount);
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
        _warningCounts.TryGetValue(NormalizeKey(key), out int warningCount);
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

    private bool TryIncrement(string key, out int warningCount)
    {
        key = NormalizeKey(key);
        EnforceKeyLimit(key);
        _warningCounts.TryGetValue(key, out warningCount);
        if (warningCount >= _maxWarnings)
        {
            return false;
        }

        warningCount++;
        _warningCounts[key] = warningCount;
        return true;
    }

    private void EnforceKeyLimit(string incomingKey)
    {
        if (_warningCounts.ContainsKey(incomingKey) || _warningCounts.Count < _maxKeyCount)
        {
            return;
        }

        string keyToRemove = null;
        foreach (string key in _warningCounts.Keys)
        {
            keyToRemove = key;
            break;
        }

        if (keyToRemove != null)
        {
            _warningCounts.Remove(keyToRemove);
        }
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrEmpty(key) ? "unknown" : key;
    }
}
