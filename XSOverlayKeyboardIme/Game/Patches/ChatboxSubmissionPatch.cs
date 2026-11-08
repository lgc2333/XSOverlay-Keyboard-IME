using System.Threading.Tasks;
using HarmonyLib;
using XSOverlay;

namespace XSOverlayKeyboardIme.Game.Patches
{
    [HarmonyPatch(typeof(VRChatOscService), nameof(VRChatOscService.SendChatboxAsync))]
    internal static class ChatboxSubmissionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(VRChatOscService __instance, out bool __state)
        {
            __state = ChatboxIntegration.BeginSubmission(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(bool __state, ref Task<VRChatChatboxSendResult> __result)
        {
            if (__state)
                __result = ChatboxIntegration.CompleteSubmission(__result);
        }
    }
}
