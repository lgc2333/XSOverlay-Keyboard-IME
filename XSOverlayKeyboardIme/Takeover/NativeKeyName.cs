namespace XSOverlayKeyboardIme.Takeover
{
    internal static class NativeKeyName
    {
        internal static bool IsCharacter(int key) =>
            key == 0x20
            || key >= 0x30 && key <= 0x39
            || key >= 0x41 && key <= 0x5A
            || key >= 0x60 && key <= 0x6F
            || key >= 0xBA && key <= 0xC0
            || key >= 0xDB && key <= 0xDF
            || key == 0xE2;

        internal static bool TryRead(int key, out string name)
        {
            if (key >= 0x30 && key <= 0x39 || key >= 0x41 && key <= 0x5A)
            {
                name = ((char)key).ToString();
                return true;
            }
            if (key >= 0x70 && key <= 0x87)
            {
                name = "F" + (key - 0x6F);
                return true;
            }
            if (key >= 0x60 && key <= 0x69)
            {
                name = (key - 0x60).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            switch (key)
            {
                case 0x08:
                    name = "Backspace";
                    break;
                case 0x09:
                    name = "Tab";
                    break;
                case 0x0D:
                    name = "Enter";
                    break;
                case 0x10:
                case 0xA0:
                case 0xA1:
                    name = "Shift";
                    break;
                case 0x11:
                case 0xA2:
                case 0xA3:
                    name = "Control";
                    break;
                case 0x12:
                case 0xA4:
                case 0xA5:
                    name = "Alt";
                    break;
                case 0x1B:
                    name = "Escape";
                    break;
                case 0x20:
                    name = " ";
                    break;
                case 0x21:
                    name = "PageUp";
                    break;
                case 0x22:
                    name = "PageDown";
                    break;
                case 0x23:
                    name = "End";
                    break;
                case 0x24:
                    name = "Home";
                    break;
                case 0x25:
                    name = "ArrowLeft";
                    break;
                case 0x26:
                    name = "ArrowUp";
                    break;
                case 0x27:
                    name = "ArrowRight";
                    break;
                case 0x28:
                    name = "ArrowDown";
                    break;
                case 0x2D:
                    name = "Insert";
                    break;
                case 0x2E:
                    name = "Delete";
                    break;
                case 0x5B:
                case 0x5C:
                    name = "Meta";
                    break;
                case 0x6A:
                    name = "*";
                    break;
                case 0x6B:
                    name = "+";
                    break;
                case 0x6D:
                    name = "-";
                    break;
                case 0x6E:
                    name = ".";
                    break;
                case 0x6F:
                    name = "/";
                    break;
                case 0xBA:
                    name = ";";
                    break;
                case 0xBB:
                    name = "=";
                    break;
                case 0xBC:
                    name = ",";
                    break;
                case 0xBD:
                    name = "-";
                    break;
                case 0xBE:
                    name = ".";
                    break;
                case 0xBF:
                    name = "/";
                    break;
                case 0xC0:
                    name = "`";
                    break;
                case 0xDB:
                    name = "[";
                    break;
                case 0xDC:
                    name = "\\";
                    break;
                case 0xDD:
                    name = "]";
                    break;
                case 0xDE:
                    name = "'";
                    break;
                case 0xE2:
                    name = "\\";
                    break;
                default:
                    name = null;
                    return false;
            }
            return true;
        }
    }
}
