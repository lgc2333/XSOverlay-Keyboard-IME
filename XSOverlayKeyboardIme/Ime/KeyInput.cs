using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>One keyboard entry for SendInput, built the same way wherever it is used.</summary>
    internal static class KeyInput
    {
        internal static NativeInput Create(VirtualKey key, uint flags)
        {
            uint scan = Win32Native.MapVirtualKeyW((uint)key, Win32Native.MapVirtualKeyToExtendedScan);
            var input = new NativeInput { Type = Win32Native.InputKeyboard };
            input.Data.Keyboard = new KeyboardInput
            {
                VirtualKey = (ushort)key,
                ScanCode = (ushort)(scan & 0xFF),
                Flags = flags | ((scan & 0xFF00) == 0xE000 ? Win32Native.KeyEventExtendedKey : 0),
            };
            return input;
        }
    }
}
