using System;
using HarmonyLib;

namespace V81ErrorFix;

[HarmonyPatch(typeof(SandSpiderAI), "PlayerLeaveWebClientRpc")]
internal static class SandSpiderAIPlayerLeaveWebClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();
    private static readonly WarningLimiter UnknownWarnings = new();
    private static readonly WarningLimiter NonExecuteStageWarnings = new(maxWarnings: 1);

    private static Exception Finalizer(SandSpiderAI __instance, int trapID, int playerNum, Exception __exception)
    {
        if (__exception is not ArgumentOutOfRangeException && __exception is not IndexOutOfRangeException)
        {
            return __exception;
        }

        if (!RpcExecStageUtility.ShouldAllowClientRpcSuppression(__instance, "SandSpiderAI.PlayerLeaveWebClientRpc", __exception, NonExecuteStageWarnings))
        {
            return __exception;
        }

        if (IsKnownDesync(__instance, trapID, playerNum, out string reason))
        {
            Warnings.Warn($"PlayerLeaveWebClientRpc|{reason}", $"Suppressed SandSpiderAI.PlayerLeaveWebClientRpc {__exception.GetType().Name} for known desync: {reason}.");
            return null;
        }

        UnknownWarnings.Warn($"PlayerLeaveWebClientRpc|trap:{trapID}|player:{playerNum}", $"Unhandled SandSpiderAI.PlayerLeaveWebClientRpc {__exception.GetType().Name}; returning original exception for trap {trapID}, player {playerNum}.");
        return __exception;
    }

    private static bool IsKnownDesync(SandSpiderAI spider, int trapID, int playerNum, out string reason)
    {
        if (spider == null || spider.webTraps == null)
        {
            reason = "spider web trap list was missing";
            return false;
        }

        if (trapID < 0 || trapID >= spider.webTraps.Count)
        {
            reason = $"trapID {trapID} outside webTraps count {spider.webTraps.Count}";
            return true;
        }

        if (StartOfRound.Instance == null || StartOfRound.Instance.allPlayerScripts == null)
        {
            reason = "player list was missing";
            return false;
        }

        if (playerNum < 0 || playerNum >= StartOfRound.Instance.allPlayerScripts.Length)
        {
            reason = $"playerNum {playerNum} outside allPlayerScripts length {StartOfRound.Instance.allPlayerScripts.Length}";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
