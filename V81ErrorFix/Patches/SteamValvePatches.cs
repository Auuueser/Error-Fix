using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace V81ErrorFix;

[HarmonyPatch]
internal static class SteamValveDamageTriggerSpawnPatch
{
    private const string DamageTriggerName = "damageTrigger";
    private static bool loggedActivation;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        // The matching Netcode warning is normally a single startup/spawn lifecycle warning.
        // Keep this behavior-changing activation guard opt-in until a real SteamValve trigger
        // gameplay failure is confirmed, so Auto does not add spawn-path work for log noise.
        return PatchModeUtility.IsExplicitlyEnabled(ErrorFixConfig.SteamValveDamageTriggerSpawnGuardMode)
            && TargetMethod() != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(NetworkObject), "InvokeBehaviourNetworkSpawn");
    }

    private static void Prefix(NetworkObject __instance, ref List<GameObject> __state)
    {
        if (__instance == null || __instance.transform == null)
        {
            return;
        }

        Transform damageTriggerTransform = FindSelfOrChildByName(__instance.transform, DamageTriggerName);
        if (damageTriggerTransform == null)
        {
            return;
        }

        InteractTrigger trigger = damageTriggerTransform.GetComponent<InteractTrigger>();
        if (!IsSteamValveDamageTrigger(__instance, trigger))
        {
            return;
        }

        __state ??= new List<GameObject>(capacity: 1);
        ActivateInactiveHierarchy(damageTriggerTransform, __instance.transform, __state);
        LogActivationOnce(__instance, trigger);
    }

    private static System.Exception Finalizer(List<GameObject> __state, System.Exception __exception)
    {
        RestoreInactive(__state);
        return __exception;
    }

    private static void RestoreInactive(List<GameObject> gameObjects)
    {
        if (gameObjects == null)
        {
            return;
        }

        for (int i = 0; i < gameObjects.Count; i++)
        {
            GameObject gameObject = gameObjects[i];
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }
    }

    private static void ActivateInactiveHierarchy(Transform target, Transform root, List<GameObject> activatedObjects)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            GameObject gameObject = current.gameObject;
            if (gameObject != null && !gameObject.activeSelf)
            {
                activatedObjects.Add(gameObject);
                gameObject.SetActive(true);
            }

            if (current == root)
            {
                break;
            }
        }
    }

    private static void LogActivationOnce(NetworkObject networkObject, InteractTrigger trigger)
    {
        if (loggedActivation)
        {
            return;
        }

        loggedActivation = true;
        Plugin.Log?.LogInfo($"Temporarily activated SteamValve damageTrigger '{GetPath(trigger.transform)}' during Netcode spawn for NetworkObject #{networkObject.NetworkObjectId}.");
    }

    private static bool IsSteamValveDamageTrigger(NetworkObject networkObject, InteractTrigger trigger)
    {
        if (trigger == null ||
            trigger.NetworkObject != networkObject ||
            trigger.gameObject == null ||
            trigger.gameObject.activeInHierarchy ||
            !string.Equals(trigger.gameObject.name, DamageTriggerName, System.StringComparison.Ordinal))
        {
            return false;
        }

        return HasSteamValveHazardParent(trigger.transform);
    }

    private static Transform FindSelfOrChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, childName, System.StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (string.Equals(child.name, childName, System.StringComparison.Ordinal))
            {
                return child;
            }

            Transform match = FindSelfOrChildByName(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool HasSteamValveHazardParent(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.GetComponent<SteamValveHazard>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return "unknown";
        }

        string path = transform.name;
        for (Transform current = transform.parent; current != null; current = current.parent)
        {
            path = current.name + "/" + path;
        }

        return path;
    }
}
