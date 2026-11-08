using System.Runtime.InteropServices;

namespace SNAP
{
    /// <summary>
    /// Throws away mouse clicks and key presses Windows queued while the UI thread was busy.
    ///
    /// Login and the first Dashboard load both run on the UI thread, so the window is frozen for a
    /// few seconds and it's natural to click "Sign In" again. Windows keeps those clicks and hands
    /// them to whatever window is under the cursor once the thread is free - which is the freshly
    /// opened Dashboard, whose Total Income card / Revenue chart sit exactly where Sign In was, so
    /// the app "opened on Payments by itself". Call this the moment a new main screen appears.
    /// </summary>
    internal static class InputQueue
    {
        private const uint WM_KEYFIRST = 0x0100;
        private const uint WM_KEYLAST = 0x0109;
        private const uint WM_MOUSEFIRST = 0x0200;
        private const uint WM_MOUSELAST = 0x020E;
        private const uint PM_REMOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

        public static void DiscardPendingUserInput()
        {
            while (PeekMessage(out _, IntPtr.Zero, WM_MOUSEFIRST, WM_MOUSELAST, PM_REMOVE)) { }
            while (PeekMessage(out _, IntPtr.Zero, WM_KEYFIRST, WM_KEYLAST, PM_REMOVE)) { }
        }
    }
}
