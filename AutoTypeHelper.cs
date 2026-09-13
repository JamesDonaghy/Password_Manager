using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PasswordManager
{
    /// <summary>
    /// Sends keystrokes to the currently focused window (e.g. a browser login form).
    /// Uses SendInput with Unicode so special characters in passwords are typed correctly,
    /// unlike SendKeys which requires escaping +^%~(){}[] etc.
    /// </summary>
    internal static class AutoTypeHelper
    {
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const ushort VK_TAB = 0x09;
        private const ushort VK_RETURN = 0x0D;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        /// <summary>
        /// Types username, Tab, password into the foreground window.
        /// Optionally presses Enter afterwards.
        /// </summary>
        public static void TypeCredentials(string username, string password, bool pressEnter = false, int interKeyDelayMs = 15)
        {
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                return;
            }

            TypeText(username ?? string.Empty, interKeyDelayMs);
            TypeVirtualKey(VK_TAB);
            Thread.Sleep(interKeyDelayMs);
            TypeText(password ?? string.Empty, interKeyDelayMs);

            if (pressEnter)
            {
                Thread.Sleep(interKeyDelayMs);
                TypeVirtualKey(VK_RETURN);
            }
        }

        private static void TypeText(string text, int interKeyDelayMs)
        {
            foreach (char c in text)
            {
                TypeUnicodeChar(c);
                if (interKeyDelayMs > 0)
                {
                    Thread.Sleep(interKeyDelayMs);
                }
            }
        }

        private static void TypeUnicodeChar(char c)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0;
            inputs[0].U.ki.wScan = c;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = 0;
            inputs[1].U.ki.wScan = c;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(2, inputs, INPUT.Size);
        }

        private static void TypeVirtualKey(ushort vk)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = vk;
            inputs[0].U.ki.wScan = 0;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = vk;
            inputs[1].U.ki.wScan = 0;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(2, inputs, INPUT.Size);
        }

        public static void DelayBeforeTyping(int milliseconds = 500)
        {
            Thread.Sleep(milliseconds);
        }
    }
}