using System;

namespace V81ErrorFix;

internal static class ArrayUtility
{
    internal static T[] EnsureLength<T>(T[] values, int requiredLength, T defaultValue)
    {
        if (requiredLength <= 0)
        {
            return values ?? Array.Empty<T>();
        }

        if (values != null && values.Length >= requiredLength)
        {
            return values;
        }

        T[] resizedValues = new T[requiredLength];
        for (int i = 0; i < resizedValues.Length; i++)
        {
            resizedValues[i] = i < (values?.Length ?? 0) ? values[i] : defaultValue;
        }

        return resizedValues;
    }
}
