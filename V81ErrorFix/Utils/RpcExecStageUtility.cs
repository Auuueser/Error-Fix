using System;

namespace V81ErrorFix;

internal static class RpcExecStageUtility
{
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
}
