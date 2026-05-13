using System;
using System.Collections.Generic;

namespace V81ErrorFix;

internal static class NullRefGuard
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly HashSet<string> LoggedExceptionDetails = new();

    internal static Exception Suppress(Exception exception, string key, Func<bool> isKnownSafeCase)
    {
        if (exception == null)
        {
            return null;
        }

        if (exception is not NullReferenceException)
        {
            return exception;
        }

        if (!IsKnownSafeCase(key, isKnownSafeCase))
        {
            return exception;
        }

        Warnings.Warn(key, () =>
        {
            if (LoggedExceptionDetails.Add(key))
            {
                return $"Suppressed known NullReferenceException in {key}. First exception detail: {exception}";
            }

            return $"Suppressed known NullReferenceException in {key}; the object will safely retry on later updates.";
        });
        return null;
    }

    internal static void Clear()
    {
        Warnings.Clear();
        LoggedExceptionDetails.Clear();
    }

    private static bool IsKnownSafeCase(string key, Func<bool> isKnownSafeCase)
    {
        if (isKnownSafeCase == null)
        {
            return false;
        }

        try
        {
            return isKnownSafeCase();
        }
        catch (Exception ex)
        {
            Warnings.Warn($"classifier-failed|{key}", $"NullRefGuard classifier failed for {key}; returning original exception: {ex.GetType().Name}.");
            return false;
        }
    }
}
