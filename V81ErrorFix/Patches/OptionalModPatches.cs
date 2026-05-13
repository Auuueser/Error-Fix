using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class EnemyHealthBarsLateUpdatePatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return TargetMethod() != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type healthBarType = AccessTools.TypeByName("EnemyHealthBars.Scripts.HealthBar");
        return OptionalPatchTargetResolver.FindMethod(healthBarType, "LateUpdate", isStatic: false, typeof(void));
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("EnemyHealthBars.HealthBar.LateUpdate", "Suppressed EnemyHealthBars.HealthBar.LateUpdate NullReferenceException.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch]
internal static class ShipLootPlusUiHelperPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return AccessTools.TypeByName("ShipLootPlus.Utils.UiHelper") != null;
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type uiHelperType = AccessTools.TypeByName("ShipLootPlus.Utils.UiHelper");
        if (uiHelperType == null)
        {
            yield break;
        }

        HashSet<MethodBase> yieldedMethods = new();
        MethodBase calculateLootValue = OptionalPatchTargetResolver.FindMethod(uiHelperType, "CalculateLootValue", isStatic: true, null, typeof(List<GrabbableObject>), typeof(string));
        if (calculateLootValue != null && yieldedMethods.Add(calculateLootValue))
        {
            yield return calculateLootValue;
        }

        MethodBase refreshElementValues = OptionalPatchTargetResolver.FindMethod(uiHelperType, "RefreshElementValues", isStatic: true, typeof(void));
        if (refreshElementValues != null && yieldedMethods.Add(refreshElementValues))
        {
            yield return refreshElementValues;
        }

        foreach (Type nestedType in uiHelperType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (MethodInfo method in nestedType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (!ShouldPatchGeneratedMethod(nestedType, method) || !yieldedMethods.Add(method))
                {
                    continue;
                }

                yield return method;
            }
        }
    }

    private static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            string methodName = __originalMethod != null ? __originalMethod.Name : "unknown";
            Warnings.Warn($"ShipLootPlus.UiHelper.{methodName}", $"Compatibility guard handled ShipLootPlus.UiHelper.{methodName} NullReferenceException while refreshing loot UI.");
            return null;
        }

        return __exception;
    }

    private static bool ShouldPatchGeneratedMethod(Type nestedType, MethodInfo method)
    {
        if (nestedType == null || method == null)
        {
            return false;
        }

        return method.ReturnType == typeof(bool)
            && (method.Name.Contains("<CalculateLootValue>")
                || (method.Name == "MoveNext" && nestedType.Name.Contains("<UpdateDatapoints>")));
    }
}

[HarmonyPatch]
internal static class NightVisionInsideLightingPostfixPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return TargetMethod() != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type nightVisionOutdoorsType = AccessTools.TypeByName("NightVision.Patches.NightVisionOutdoors");
        return OptionalPatchTargetResolver.FindMethod(nightVisionOutdoorsType, "InsideLightingPostfix", isStatic: true, typeof(void), typeof(TimeOfDay));
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("NightVision.InsideLightingPostfix", "Compatibility guard handled NightVision outdoors lighting NullReferenceException while inside lighting changed.");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch]
internal static class ChatCommandApiStartHostPostfixPatch
{
    private static readonly WarningLimiter Warnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return TargetMethod() != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type startHostPatchType = AccessTools.TypeByName("ChatCommandAPI.Patches.GameNetworkManager_StartHost");
        return OptionalPatchTargetResolver.FindMethod(startHostPatchType, "Postfix", isStatic: true, typeof(void));
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            Warnings.Warn("ChatCommandAPI.GameNetworkManager_StartHost.Postfix", "Compatibility guard handled ChatCommandAPI StartHost Postfix NullReferenceException while hosting a lobby.");
            return null;
        }

        return __exception;
    }
}

internal static class OptionalPatchTargetResolver
{
    internal static MethodBase FindMethod(Type type, string methodName, bool isStatic, Type returnType, params Type[] parameterTypes)
    {
        if (type == null)
        {
            return null;
        }

        MethodInfo method = AccessTools.Method(type, methodName, parameterTypes);
        if (method == null || (method.IsStatic != isStatic) || (returnType != null && method.ReturnType != returnType))
        {
            return null;
        }

        return method;
    }
}
