using System;
using HarmonyLib;

namespace V81ErrorFix;

[HarmonyPatch(typeof(SandSpiderAI), "PlayerLeaveWebClientRpc")]
internal static class SandSpiderAIPlayerLeaveWebClientRpcPatch
{
    private static readonly WarningLimiter Warnings = new();

    private static Exception Finalizer(int trapID, int playerNum, Exception __exception)
    {
        if (__exception is ArgumentOutOfRangeException)
        {
            Warnings.Warn($"PlayerLeaveWebClientRpc|trap:{trapID}|player:{playerNum}", $"Suppressed SandSpiderAI.PlayerLeaveWebClientRpc ArgumentOutOfRangeException for trap {trapID}, player {playerNum}.");
            return null;
        }

        return __exception;
    }
}
