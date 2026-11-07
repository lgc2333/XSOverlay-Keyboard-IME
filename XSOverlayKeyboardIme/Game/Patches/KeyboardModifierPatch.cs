using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using WindowsInput;
using WindowsInput.Native;
using XSOverlay;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>Redirects the existing modifier state machine to the host queue. See ADR 0040.</summary>
    [HarmonyPatch]
    internal static class KeyboardModifierPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(InputManager), "HandleKeypress");
            yield return AccessTools.Method(typeof(InputManager), "ReleaseStickyKeys");
            yield return AccessTools.Method(typeof(InputManager), "ReleaseToggledKeys");
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original
        )
        {
            MethodInfo focus = AccessTools.PropertyGetter(typeof(KeyboardInputFocusManager), "HasFocus");
            int focusReads = 0;
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                bool focusRead = original.Name == "HandleKeypress" && instruction.Calls(focus);
                if (
                    instruction.operand is MethodInfo method
                    && method.DeclaringType == typeof(IKeyboardSimulator)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(VirtualKeyCode)
                    && method.Name is "KeyDown" or "KeyUp" or "KeyPress"
                )
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(KeyboardModifierInput), method.Name);
                    replaced++;
                }

                yield return instruction;
                // Keep the first check: Enter/Escape still release focus through XSOverlay.
                if (focusRead && ++focusReads == 2)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(
                        typeof(KeyboardModifierInput),
                        nameof(KeyboardModifierInput.UseWebFocus)
                    );
                }
            }

            if (replaced == 0 || (original.Name == "HandleKeypress" && focusReads != 2))
            {
                throw new InvalidOperationException("XSOverlay modifier input changed: " + original.Name);
            }
        }
    }
}
