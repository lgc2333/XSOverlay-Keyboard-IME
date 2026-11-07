using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Vuplex.WebView;
using XSOverlay;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Page;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>Uses each ray's horizontal axis over the candidate bar. See ADR 0043.</summary>
    [HarmonyPatch(typeof(Raycaster), "HandleScrolling")]
    internal static class CandidateScrollPatch
    {
        private static readonly AccessTools.FieldRef<Raycaster, MouseInputDevice> Input = AccessTools.FieldRefAccess<
            Raycaster,
            MouseInputDevice
        >("InputDevice");
        private static readonly AccessTools.FieldRef<Raycaster, Vector2> Cursor = AccessTools.FieldRefAccess<
            Raycaster,
            Vector2
        >("CursorUVNormalized");
        private static readonly Action<Raycaster> Reset = AccessTools.MethodDelegate<Action<Raycaster>>(
            AccessTools.Method(typeof(Raycaster), "ResetScrollState")
        );
        private static readonly Func<Unity_Overlay, bool> CanInteract = AccessTools.MethodDelegate<
            Func<Unity_Overlay, bool>
        >(AccessTools.Method(typeof(Raycaster), "CanInteractWithOverlay"));
        private static readonly Func<float, float, float, float, float> UpdateVelocity = AccessTools.MethodDelegate<
            Func<float, float, float, float, float>
        >(AccessTools.Method(typeof(Raycaster), "UpdateScrollVelocity"));
        private static readonly ConditionalWeakTable<Raycaster, Motion> Motions =
            new ConditionalWeakTable<Raycaster, Motion>();

        [HarmonyPrefix]
        private static bool Prefix(Raycaster __instance)
        {
            Motion motion = Motions.GetValue(__instance, _ => new Motion());
            MouseInputDevice input = Input(__instance);
            Unity_Overlay overlay = __instance.HoveringOverlay;
            Vector2 cursor = Cursor(__instance);
            IWebView page = KeyboardOverlayScope.FindPage();
            CandidateScrollArea area = KeyboardPage.ScrollAreaFor(page);
            if (
                area == null
                || overlay != Overlay_Manager.Instance?.Keyboard_Overlay
                || !CanInteract(overlay)
                || input == null
                || !input.isActiveAndEnabled
                || !__instance.IsActiveRaycaster
                || overlay.IsHeld
                || overlay.IsLocked
                || overlay.IsBeingScaled
                || input.ScaleGracePeriodActive
                || overlay.OverlayWebView?.IsResolutionUpdatePending == true
                || !area.Contains(cursor.x, cursor.y)
            )
            {
                motion.Clear();
                return true;
            }

            // The native handler reads only Y and retains momentum; neither may leak into this bar.
            Reset(__instance);
            try
            {
                if (!ReferenceEquals(motion.Page, page))
                    motion.Clear();
                motion.Page = page;
                float elapsed = Mathf.Clamp(Time.deltaTime, 0, 0.05f);
                float axis = Mathf.Clamp(input.Scroll.GetAxis(input.InputSource).x, -1, 1);
                float strength = Mathf.Abs(axis) <= 0.1f ? 0 : Mathf.Sign(axis) * (Mathf.Abs(axis) - 0.1f) / 0.9f;
                float maximum = 4000 * Mathf.Clamp01(XSettingsManager.Instance.Settings.ScrollSpeed);
                float previous = Mathf.Clamp(motion.Velocity, -maximum, maximum);
                motion.Velocity = UpdateVelocity(previous, strength * maximum, maximum, elapsed);
                motion.Pixels += (previous + motion.Velocity) * 0.5f * elapsed;
                int pixels = (int)motion.Pixels;
                motion.Pixels -= pixels;
                if (pixels != 0)
                    page.Scroll(page.PointToNormalized(pixels, 0), new Vector2(area.ScrollX, area.ScrollY));
            }
            catch (Exception exception)
            {
                motion.Clear();
                HostLog.Warn("candidate scrolling failed: " + exception.Message);
            }
            return false;
        }

        private sealed class Motion
        {
            public IWebView Page;
            public float Velocity;
            public float Pixels;

            public void Clear()
            {
                Page = null;
                Velocity = 0;
                Pixels = 0;
            }
        }
    }
}
