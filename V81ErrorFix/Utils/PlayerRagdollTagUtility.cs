using System;
using System.Collections.Generic;
using UnityEngine;

namespace V81ErrorFix;

internal static class PlayerRagdollTagUtility
{
    private const string Prefix = "PlayerRagdoll";
    private static readonly Dictionary<string, bool> TagExistsCache = new();

    [ThreadStatic]
    private static bool _isTagExistenceProbe;

    internal static bool IsTagExistenceProbe => _isTagExistenceProbe;

    internal static bool IsPlayerRagdollTag(string tag)
    {
        return TryGetPlayerRagdollIndex(tag, out _);
    }

    internal static bool TryGetPlayerRagdollIndex(string tag, out int ragdollIndex)
    {
        ragdollIndex = -1;
        if (string.IsNullOrEmpty(tag) || !tag.StartsWith(Prefix, StringComparison.Ordinal) || tag.Length == Prefix.Length)
        {
            return false;
        }

        for (int i = Prefix.Length; i < tag.Length; i++)
        {
            if (!char.IsDigit(tag[i]))
            {
                return false;
            }
        }

        return int.TryParse(tag.Substring(Prefix.Length), out ragdollIndex);
    }

    internal static bool IsUndefinedPlayerRagdollTagException(Exception exception, string expectedTag = null)
    {
        return exception is UnityException && IsUndefinedPlayerRagdollTagMessage(exception.Message, expectedTag);
    }

    internal static bool IsUndefinedPlayerRagdollTagMessage(string message, string expectedTag = null)
    {
        if (string.IsNullOrEmpty(message) || !message.StartsWith("Tag: PlayerRagdoll", StringComparison.Ordinal) || !message.Contains("not defined"))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(expectedTag))
        {
            return IsPlayerRagdollTag(expectedTag) && message.IndexOf(expectedTag, StringComparison.Ordinal) >= 0;
        }

        int tagStart = "Tag: ".Length;
        int tagEnd = message.IndexOf(' ', tagStart);
        if (tagEnd <= tagStart)
        {
            return false;
        }

        return IsPlayerRagdollTag(message.Substring(tagStart, tagEnd - tagStart));
    }

    internal static bool TagExists(string tag)
    {
        return TagExistsCached(tag, ProbeUnityTagExists);
    }

    internal static bool TagExistsCached(string tag, Func<string, bool> probe)
    {
        if (string.IsNullOrEmpty(tag) || probe == null)
        {
            return false;
        }

        if (TagExistsCache.TryGetValue(tag, out bool exists))
        {
            return exists;
        }

        exists = probe(tag);
        TagExistsCache[tag] = exists;
        return exists;
    }

    private static bool ProbeUnityTagExists(string tag)
    {
        GameObject probeObject = null;
        try
        {
            _isTagExistenceProbe = true;
            probeObject = new GameObject("V81ErrorFix.TagProbe")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            probeObject.tag = tag;
            return true;
        }
        catch (UnityException ex) when (IsUndefinedPlayerRagdollTagException(ex, tag))
        {
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isTagExistenceProbe = false;
            if (probeObject != null)
            {
                UnityEngine.Object.Destroy(probeObject);
            }
        }
    }
}
