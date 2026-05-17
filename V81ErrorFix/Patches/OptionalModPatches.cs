using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class EnemyHealthBarsLateUpdatePatch
{
    private const string TargetKey = "EnemyHealthBars.HealthBar.LateUpdate";
    private const string TargetSignature = "EnemyHealthBars.Scripts.HealthBar::LateUpdate() : void instance";
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return OptionalCompatibilityUtility.ShouldResolveTarget(ErrorFixConfig.EnemyHealthBarsLateUpdateGuardMode, TargetSignature)
            && OptionalCompatibilityUtility.ShouldPatchResolvedTarget(TargetMethod() != null, TargetKey, TargetSignature);
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type healthBarType = OptionalCompatibilityUtility.GetTypeByName("EnemyHealthBars.Scripts.HealthBar");
        return OptionalPatchTargetResolver.FindMethod(healthBarType, "LateUpdate", isStatic: false, typeof(void));
    }

    private static Exception Finalizer(object __instance, MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return OptionalCompatibilityUtility.HandleNullReference(
            TargetKey,
            __originalMethod,
            __exception,
            () => IsKnownSafeNullReference(__instance),
            Warnings,
            UnknownWarnings);
    }

    private static bool IsKnownSafeNullReference(object healthBar)
    {
        if (OptionalCompatibilityUtility.IsUnityObjectMissing(healthBar))
        {
            return true;
        }

        if (!OptionalCompatibilityUtility.TryGetInstanceMemberValue(TargetKey, healthBar, "CurLayout", out object currentLayout))
        {
            return false;
        }

        if (OptionalCompatibilityUtility.IsUnityObjectMissing(currentLayout))
        {
            return true;
        }

        return currentLayout is Component layoutComponent && layoutComponent.gameObject == null;
    }
}

internal static class ShipLootPlusUiHelperPatchSupport
{
    internal const string TargetKey = "ShipLootPlus.UiHelper";
    private const string CalculateLootValueSignature = "ShipLootPlus.Utils.UiHelper::CalculateLootValue(List<GrabbableObject>|List<string>, string) : unknown static";
    private const string GeneratedMethodsSignature = "ShipLootPlus.Utils.UiHelper generated CalculateLootValue/UpdateDatapoints methods";
    private const string RefreshElementValuesSignature = "ShipLootPlus.Utils.UiHelper::RefreshElementValues() : void static";
    private const double HeldScrapScanCacheSeconds = 0.25d;
    private static readonly Type[] IgnoredLootListTypes = { typeof(List<GrabbableObject>), typeof(List<string>) };
    internal static readonly WarningLimiter Warnings = new();
    internal static readonly WarningLimiter UnknownWarnings = new();
    private static readonly object HeldScrapScanLock = new();
    private static DateTime heldScrapLastScanUtc = DateTime.MinValue;
    private static bool heldScrapLastScanResult;
    private static MethodBase[] calculateLootValueTargets;
    private static MethodBase[] generatedTargets;
    private static bool refreshElementValuesTargetResolved;
    private static MethodBase refreshElementValuesTarget;

    internal static bool ShouldPatchCalculateLootValue()
    {
        return OptionalCompatibilityUtility.ShouldResolveTarget(ErrorFixConfig.ShipLootPlusUiHelperGuardMode, CalculateLootValueSignature)
            && OptionalCompatibilityUtility.ShouldPatchResolvedTarget(GetCalculateLootValueTargets().Length > 0, TargetKey, CalculateLootValueSignature);
    }

    internal static bool ShouldPatchRefreshElementValues()
    {
        return OptionalCompatibilityUtility.ShouldResolveTarget(ErrorFixConfig.ShipLootPlusUiHelperGuardMode, RefreshElementValuesSignature)
            && OptionalCompatibilityUtility.ShouldPatchResolvedTarget(GetRefreshElementValuesTarget() != null, TargetKey, RefreshElementValuesSignature);
    }

    internal static bool ShouldPatchGeneratedMethods()
    {
        return OptionalCompatibilityUtility.ShouldResolveTarget(ErrorFixConfig.ShipLootPlusUiHelperGuardMode, GeneratedMethodsSignature)
            && OptionalCompatibilityUtility.ShouldPatchResolvedTarget(GetGeneratedTargets().Length > 0, TargetKey, GeneratedMethodsSignature);
    }

    internal static IEnumerable<MethodBase> ResolveCalculateLootValueTargets(bool logResolvedTargets)
    {
        MethodBase[] targets = GetCalculateLootValueTargets();
        for (int i = 0; i < targets.Length; i++)
        {
            if (logResolvedTargets)
            {
                OptionalCompatibilityUtility.LogResolvedTarget(TargetKey, targets[i]);
            }

            yield return targets[i];
        }
    }

    internal static MethodBase ResolveRefreshElementValuesTarget(bool logResolvedTarget)
    {
        MethodBase refreshElementValues = GetRefreshElementValuesTarget();
        if (refreshElementValues != null && logResolvedTarget)
        {
            OptionalCompatibilityUtility.LogResolvedTarget(TargetKey, refreshElementValues);
        }

        return refreshElementValues;
    }

    internal static IEnumerable<MethodBase> ResolveGeneratedTargets(bool logResolvedTargets)
    {
        MethodBase[] targets = GetGeneratedTargets();
        for (int i = 0; i < targets.Length; i++)
        {
            if (logResolvedTargets)
            {
                OptionalCompatibilityUtility.LogResolvedTarget(TargetKey, targets[i]);
            }

            yield return targets[i];
        }
    }

    private static MethodBase[] GetCalculateLootValueTargets()
    {
        if (calculateLootValueTargets != null)
        {
            return calculateLootValueTargets;
        }

        Type uiHelperType = OptionalCompatibilityUtility.GetTypeByName("ShipLootPlus.Utils.UiHelper");
        if (uiHelperType == null)
        {
            calculateLootValueTargets = Array.Empty<MethodBase>();
            return calculateLootValueTargets;
        }

        List<MethodBase> targets = new(capacity: IgnoredLootListTypes.Length);
        HashSet<MethodBase> yieldedMethods = new();
        for (int i = 0; i < IgnoredLootListTypes.Length; i++)
        {
            MethodBase calculateLootValue = OptionalPatchTargetResolver.FindMethod(uiHelperType, "CalculateLootValue", isStatic: true, null, IgnoredLootListTypes[i], typeof(string));
            if (calculateLootValue != null && yieldedMethods.Add(calculateLootValue))
            {
                targets.Add(calculateLootValue);
            }
        }

        calculateLootValueTargets = targets.ToArray();
        return calculateLootValueTargets;
    }

    private static MethodBase GetRefreshElementValuesTarget()
    {
        if (refreshElementValuesTargetResolved)
        {
            return refreshElementValuesTarget;
        }

        refreshElementValuesTargetResolved = true;
        Type uiHelperType = OptionalCompatibilityUtility.GetTypeByName("ShipLootPlus.Utils.UiHelper");
        refreshElementValuesTarget = uiHelperType != null
            ? OptionalPatchTargetResolver.FindMethod(uiHelperType, "RefreshElementValues", isStatic: true, typeof(void))
            : null;

        return refreshElementValuesTarget;
    }

    private static MethodBase[] GetGeneratedTargets()
    {
        if (generatedTargets != null)
        {
            return generatedTargets;
        }

        Type uiHelperType = OptionalCompatibilityUtility.GetTypeByName("ShipLootPlus.Utils.UiHelper");
        if (uiHelperType == null)
        {
            generatedTargets = Array.Empty<MethodBase>();
            return generatedTargets;
        }

        List<MethodBase> targets = new();
        HashSet<MethodBase> yieldedMethods = new();
        foreach (Type nestedType in uiHelperType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (MethodInfo method in nestedType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (ShouldPatchGeneratedMethod(nestedType, method) && yieldedMethods.Add(method))
                {
                    targets.Add(method);
                }
            }
        }

        generatedTargets = targets.ToArray();
        return generatedTargets;
    }

    internal static bool IsKnownSafeCalculateLootValueNullReference(MethodBase originalMethod, object ignoredList, string ignoredCategory)
    {
        if (!string.Equals(originalMethod?.Name, "CalculateLootValue", StringComparison.Ordinal))
        {
            return false;
        }

        if (ignoredList == null || string.IsNullOrEmpty(ignoredCategory))
        {
            return true;
        }

        if (HasNullOrDestroyedLootEntry(ignoredList))
        {
            return true;
        }

        return string.Equals(ignoredCategory, "Inv", StringComparison.OrdinalIgnoreCase) && HasHeldScrapWithoutHolder();
    }

    internal static bool IsKnownSafeGeneratedNullReference(MethodBase originalMethod)
    {
        string methodName = originalMethod != null ? originalMethod.Name : string.Empty;
        return methodName.Contains("<CalculateLootValue>", StringComparison.Ordinal)
            ? HasHeldScrapWithoutHolder()
            : IsKnownSafeUiStateNullReference();
    }

    internal static bool IsKnownSafeUiStateNullReference()
    {
        Type uiHelperType = OptionalCompatibilityUtility.GetTypeByName("ShipLootPlus.Utils.UiHelper");
        if (uiHelperType == null)
        {
            OptionalCompatibilityUtility.LogMemberMismatch(TargetKey, null, "ShipLootPlus.Utils.UiHelper");
            return false;
        }

        if (!OptionalCompatibilityUtility.TryGetStaticMemberValue(TargetKey, uiHelperType, "UiElementList", out object uiElementList) ||
            !OptionalCompatibilityUtility.TryGetStaticMemberValue(TargetKey, uiHelperType, "ElementsToUpdate", out object elementsToUpdate) ||
            !OptionalCompatibilityUtility.TryGetStaticMemberValue(TargetKey, uiHelperType, "DataPoints", out object dataPoints) ||
            !OptionalCompatibilityUtility.TryGetStaticMemberValue(TargetKey, uiHelperType, "ContainerObject", out object containerObject))
        {
            return false;
        }

        if (uiElementList == null ||
            elementsToUpdate == null ||
            dataPoints == null ||
            OptionalCompatibilityUtility.IsUnityObjectMissing(containerObject))
        {
            return true;
        }

        return HasMissingUiElementReference(uiHelperType);
    }

    private static bool HasNullOrDestroyedLootEntry(object lootList)
    {
        if (lootList is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (object item in enumerable)
        {
            if (item == null || OptionalCompatibilityUtility.IsUnityObjectMissing(item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHeldScrapWithoutHolder()
    {
        DateTime nowUtc = DateTime.UtcNow;
        lock (HeldScrapScanLock)
        {
            if ((nowUtc - heldScrapLastScanUtc).TotalSeconds < HeldScrapScanCacheSeconds)
            {
                return heldScrapLastScanResult;
            }
        }

        bool result = false;
        PlayerControllerB[] players = StartOfRound.Instance != null ? StartOfRound.Instance.allPlayerScripts : null;
        if (players != null)
        {
            for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                GrabbableObject[] itemSlots = players[playerIndex] != null ? players[playerIndex].ItemSlots : null;
                if (itemSlots == null)
                {
                    continue;
                }

                for (int slotIndex = 0; slotIndex < itemSlots.Length; slotIndex++)
                {
                    GrabbableObject grabbableObject = itemSlots[slotIndex];
                    if (grabbableObject != null &&
                        grabbableObject.itemProperties != null &&
                        grabbableObject.itemProperties.isScrap &&
                        (grabbableObject.isHeld || grabbableObject.isPocketed) &&
                        grabbableObject.playerHeldBy == null)
                    {
                        result = true;
                        break;
                    }
                }

                if (result)
                {
                    break;
                }
            }
        }

        lock (HeldScrapScanLock)
        {
            heldScrapLastScanUtc = nowUtc;
            heldScrapLastScanResult = result;
        }

        return result;
    }

    private static bool HasMissingUiElementReference(Type uiHelperType)
    {
        if (!OptionalCompatibilityUtility.TryGetStaticMemberValue(TargetKey, uiHelperType, "ElementsToUpdate", out object elements))
        {
            return false;
        }

        if (elements is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (object element in enumerable)
        {
            if (element == null)
            {
                return true;
            }

            if (!TryGetUiElementGameObject(element, out object gameObject) ||
                !OptionalCompatibilityUtility.TryGetInstanceMemberValue(TargetKey, element, "textMeshProUGui", out object textMesh))
            {
                return false;
            }

            if (OptionalCompatibilityUtility.IsUnityObjectMissing(gameObject) || OptionalCompatibilityUtility.IsUnityObjectMissing(textMesh))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUiElementGameObject(object element, out object gameObject)
    {
        if (OptionalCompatibilityUtility.TryGetInstanceMemberValueSilently(element, "gameOjbect", out gameObject) ||
            OptionalCompatibilityUtility.TryGetInstanceMemberValueSilently(element, "gameObject", out gameObject))
        {
            return true;
        }

        OptionalCompatibilityUtility.LogMemberMismatch(TargetKey, element?.GetType(), "gameOjbect/gameObject");
        gameObject = null;
        return false;
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
internal static class ShipLootPlusCalculateLootValuePatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return ShipLootPlusUiHelperPatchSupport.ShouldPatchCalculateLootValue();
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return ShipLootPlusUiHelperPatchSupport.ResolveCalculateLootValueTargets(logResolvedTargets: true);
    }

    private static Exception Finalizer(MethodBase __originalMethod, object __0, string __1, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return OptionalCompatibilityUtility.HandleNullReference(
            ShipLootPlusUiHelperPatchSupport.TargetKey,
            __originalMethod,
            __exception,
            () => ShipLootPlusUiHelperPatchSupport.IsKnownSafeCalculateLootValueNullReference(__originalMethod, __0, __1),
            ShipLootPlusUiHelperPatchSupport.Warnings,
            ShipLootPlusUiHelperPatchSupport.UnknownWarnings);
    }
}

[HarmonyPatch]
internal static class ShipLootPlusRefreshElementValuesPatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return ShipLootPlusUiHelperPatchSupport.ShouldPatchRefreshElementValues();
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return ShipLootPlusUiHelperPatchSupport.ResolveRefreshElementValuesTarget(logResolvedTarget: true);
    }

    private static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return OptionalCompatibilityUtility.HandleNullReference(
            ShipLootPlusUiHelperPatchSupport.TargetKey,
            __originalMethod,
            __exception,
            ShipLootPlusUiHelperPatchSupport.IsKnownSafeUiStateNullReference,
            ShipLootPlusUiHelperPatchSupport.Warnings,
            ShipLootPlusUiHelperPatchSupport.UnknownWarnings);
    }
}

[HarmonyPatch]
internal static class ShipLootPlusGeneratedUiHelperPatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return ShipLootPlusUiHelperPatchSupport.ShouldPatchGeneratedMethods();
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return ShipLootPlusUiHelperPatchSupport.ResolveGeneratedTargets(logResolvedTargets: true);
    }

    private static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return OptionalCompatibilityUtility.HandleNullReference(
            ShipLootPlusUiHelperPatchSupport.TargetKey,
            __originalMethod,
            __exception,
            () => ShipLootPlusUiHelperPatchSupport.IsKnownSafeGeneratedNullReference(__originalMethod),
            ShipLootPlusUiHelperPatchSupport.Warnings,
            ShipLootPlusUiHelperPatchSupport.UnknownWarnings);
    }
}

[HarmonyPatch]
internal static class NightVisionInsideLightingPostfixPatch
{
    private const string TargetKey = "NightVision.InsideLightingPostfix";
    private const string TargetSignature = "NightVision.Patches.NightVisionOutdoors::InsideLightingPostfix(TimeOfDay) : void static";
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return OptionalCompatibilityUtility.ShouldResolveTarget(ErrorFixConfig.NightVisionInsideLightingPostfixGuardMode, TargetSignature)
            && OptionalCompatibilityUtility.ShouldPatchResolvedTarget(TargetMethod() != null, TargetKey, TargetSignature);
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type nightVisionOutdoorsType = OptionalCompatibilityUtility.GetTypeByName("NightVision.Patches.NightVisionOutdoors");
        return OptionalPatchTargetResolver.FindMethod(nightVisionOutdoorsType, "InsideLightingPostfix", isStatic: true, typeof(void), typeof(TimeOfDay));
    }

    private static Exception Finalizer(MethodBase __originalMethod, TimeOfDay __0, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return OptionalCompatibilityUtility.HandleNullReference(
            TargetKey,
            __originalMethod,
            __exception,
            () => IsKnownSafeNullReference(__0),
            Warnings,
            UnknownWarnings);
    }

    private static bool IsKnownSafeNullReference(TimeOfDay timeOfDay)
    {
        if (timeOfDay == null || timeOfDay.sunIndirect == null)
        {
            return true;
        }

        Type hdLightType = OptionalCompatibilityUtility.GetTypeByName("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
        return hdLightType != null && timeOfDay.sunIndirect.GetComponent(hdLightType) == null;
    }
}

[HarmonyPatch]
internal static class ChatCommandApiStartHostPostfixPatch
{
    private const string TargetKey = "ChatCommandAPI.GameNetworkManager_StartHost.Postfix";
    private const string TargetSignature = "ChatCommandAPI.Patches.GameNetworkManager_StartHost::Postfix() : void static";
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return OptionalCompatibilityUtility.ShouldResolveTarget(ErrorFixConfig.ChatCommandsStartHostPostfixGuardMode, TargetSignature)
            && OptionalCompatibilityUtility.ShouldPatchResolvedTarget(TargetMethod() != null, TargetKey, TargetSignature);
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type startHostPatchType = OptionalCompatibilityUtility.GetTypeByName("ChatCommandAPI.Patches.GameNetworkManager_StartHost");
        return OptionalPatchTargetResolver.FindMethod(startHostPatchType, "Postfix", isStatic: true, typeof(void));
    }

    private static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        return OptionalCompatibilityUtility.HandleNullReference(
            TargetKey,
            __originalMethod,
            __exception,
            IsKnownSafeNullReference,
            Warnings,
            UnknownWarnings);
    }

    private static bool IsKnownSafeNullReference()
    {
        Type chatCommandApiType = OptionalCompatibilityUtility.GetTypeByName("ChatCommandAPI.ChatCommandAPI");
        if (chatCommandApiType == null)
        {
            OptionalCompatibilityUtility.LogMemberMismatch(TargetKey, null, "ChatCommandAPI.ChatCommandAPI");
            return false;
        }

        if (!OptionalCompatibilityUtility.TryGetStaticMemberValue(TargetKey, chatCommandApiType, "confirmationRequests", out object confirmationRequests))
        {
            return false;
        }

        return confirmationRequests == null;
    }
}

internal static class OptionalCompatibilityUtility
{
    private static readonly WarningLimiter TargetWarnings = new();
    private static readonly object ReflectionCacheLock = new();
    private static readonly Dictionary<string, Type> TypeCache = new();
    private static readonly Dictionary<Tuple<Type, string>, MemberInfo> MemberCache = new();
    private static readonly Dictionary<MethodBase, string> SignatureCache = new();

    internal static bool ShouldPatch(BepInEx.Configuration.ConfigEntry<PatchEnableMode> modeEntry, MethodBase target, string targetKey, string expectedSignature)
    {
        return ShouldPatch(modeEntry, target != null, targetKey, expectedSignature);
    }

    internal static bool ShouldPatch(BepInEx.Configuration.ConfigEntry<PatchEnableMode> modeEntry, bool targetExists, string targetKey, string expectedSignature)
    {
        return ShouldResolveTarget(modeEntry, expectedSignature)
            && ShouldPatchResolvedTarget(targetExists, targetKey, expectedSignature);
    }

    internal static bool ShouldResolveTarget(BepInEx.Configuration.ConfigEntry<PatchEnableMode> modeEntry, string expectedSignature)
    {
        PatchEnableMode mode = modeEntry?.Value ?? PatchEnableMode.Auto;
        if (!PatchModeUtility.IsEnabled(mode, GameAssemblyIdentity.IsVerified))
        {
            if (mode == PatchEnableMode.Auto && !GameAssemblyIdentity.IsVerified)
            {
                Plugin.Log?.LogInfo($"Optional compatibility target disabled until the game assembly is verified: {expectedSignature}.");
            }

            return false;
        }

        return true;
    }

    internal static bool ShouldPatchResolvedTarget(bool targetExists, string targetKey, string expectedSignature)
    {
        if (!targetExists)
        {
            TargetWarnings.Warn($"optional-target-missing|{targetKey}", $"Optional compatibility target skipped because the expected signature was not found: {expectedSignature}.");
            return false;
        }

        Plugin.Log?.LogInfo($"Optional compatibility target enabled: {expectedSignature}.");
        return true;
    }

    internal static void LogResolvedTarget(string targetKey, MethodBase method)
    {
        Plugin.Log?.LogInfo($"Optional compatibility target resolved: {FormatSignature(method)}.");
    }

    internal static void LogMemberMismatch(string targetKey, Type type, string memberName)
    {
        string typeName = type != null ? type.FullName : "missing type";
        TargetWarnings.Warn($"optional-member-mismatch|{targetKey}|{typeName}|{memberName}", $"Optional compatibility member mismatch for {targetKey}: {typeName}::{memberName} was not found; returning original NullReferenceException.");
    }

    internal static Type GetTypeByName(string typeName)
    {
        lock (ReflectionCacheLock)
        {
            if (TypeCache.TryGetValue(typeName, out Type cachedType))
            {
                return cachedType;
            }
        }

        Type resolvedType = FindTypeByName(typeName);
        lock (ReflectionCacheLock)
        {
            TypeCache[typeName] = resolvedType;
        }

        return resolvedType;
    }

    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    internal static Exception HandleNullReference(string targetKey, MethodBase originalMethod, Exception exception, Func<bool> isKnownSafe, WarningLimiter knownWarnings, WarningLimiter unknownWarnings)
    {
        if (exception == null)
        {
            return null;
        }

        if (exception is not NullReferenceException)
        {
            return exception;
        }

        bool knownSafe;
        try
        {
            knownSafe = isKnownSafe != null && isKnownSafe();
        }
        catch (Exception classifierException)
        {
            unknownWarnings.Warn($"{targetKey}|classifier|{classifierException.GetType().Name}", $"Optional compatibility classifier failed for {FormatSignature(originalMethod)}; returning original NullReferenceException: {classifierException.GetType().Name}.");
            return exception;
        }

        if (knownSafe)
        {
            string signature = FormatSignature(originalMethod);
            knownWarnings.Warn($"{targetKey}|known|{signature}", () => $"Suppressed known-safe optional mod NullReferenceException in {signature}.");
            return null;
        }

        unknownWarnings.Warn($"{targetKey}|unknown|{exception.GetType().Name}", () => $"Unhandled optional mod NullReferenceException in {FormatSignature(originalMethod)}; returning original exception. First stack fingerprint: {Fingerprint(exception)}. First detail: {exception}");
        return exception;
    }

    internal static bool TryGetInstanceMemberValue(string targetKey, object instance, string memberName, out object value)
    {
        if (instance == null)
        {
            value = null;
            return true;
        }

        bool memberExists = TryGetMemberValue(instance.GetType(), instance, memberName, out value);
        if (!memberExists)
        {
            LogMemberMismatch(targetKey, instance.GetType(), memberName);
        }

        return memberExists;
    }

    internal static bool TryGetInstanceMemberValueSilently(object instance, string memberName, out object value)
    {
        if (instance == null)
        {
            value = null;
            return true;
        }

        return TryGetMemberValue(instance.GetType(), instance, memberName, out value);
    }

    internal static bool TryGetStaticMemberValue(string targetKey, Type type, string memberName, out object value)
    {
        bool memberExists = TryGetMemberValue(type, null, memberName, out value);
        if (!memberExists)
        {
            LogMemberMismatch(targetKey, type, memberName);
        }

        return memberExists;
    }

    internal static bool IsUnityObjectMissing(object value)
    {
        if (value == null)
        {
            return true;
        }

        return value is UnityEngine.Object unityObject && unityObject == null;
    }

    internal static bool TryGetMemberValue(Type type, object instance, string memberName, out object value)
    {
        value = null;
        if (type == null || string.IsNullOrEmpty(memberName))
        {
            return false;
        }

        MemberInfo member = GetCachedMember(type, memberName);
        if (member is PropertyInfo property)
        {
            value = property.GetValue(instance, null);
            return true;
        }

        if (member is FieldInfo field)
        {
            value = field.GetValue(instance);
            return true;
        }

        return false;
    }

    private static MemberInfo GetCachedMember(Type type, string memberName)
    {
        Tuple<Type, string> key = Tuple.Create(type, memberName);
        lock (ReflectionCacheLock)
        {
            if (MemberCache.TryGetValue(key, out MemberInfo cachedMember))
            {
                return cachedMember;
            }
        }

        PropertyInfo property = AccessTools.Property(type, memberName);
        if (property != null)
        {
            lock (ReflectionCacheLock)
            {
                MemberCache[key] = property;
            }

            return property;
        }

        FieldInfo field = AccessTools.Field(type, memberName);
        lock (ReflectionCacheLock)
        {
            MemberCache[key] = field;
        }

        return field;
    }

    private static string FormatSignature(MethodBase method)
    {
        if (method == null)
        {
            return "unknown";
        }

        lock (ReflectionCacheLock)
        {
            if (SignatureCache.TryGetValue(method, out string cachedSignature))
            {
                return cachedSignature;
            }
        }

        string parameters = string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name));
        string returnType = method is MethodInfo methodInfo ? methodInfo.ReturnType.Name : "void";
        string scope = method.IsStatic ? "static" : "instance";
        string signature = $"{method.DeclaringType?.FullName ?? "unknown"}::{method.Name}({parameters}) : {returnType} {scope}";
        lock (ReflectionCacheLock)
        {
            SignatureCache[method] = signature;
        }

        return signature;
    }

    private static string Fingerprint(Exception exception)
    {
        if (exception == null)
        {
            return "unknown";
        }

        string firstStackLine = exception.StackTrace?.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(firstStackLine) ? exception.GetType().Name : firstStackLine;
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
