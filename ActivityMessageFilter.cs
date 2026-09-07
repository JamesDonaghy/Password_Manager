using System;
using System.Windows.Forms;

namespace PasswordManager
{
    /// Detects user input (mouse/keyboard) across the whole application, for MainForm's
    /// auto-lock timer.
    ///
    /// A plain Control.MouseMove/KeyDown handler on MainForm itself only fires for input
    /// that lands directly on MainForm's own background, not on any of its child controls
    /// (the grid, search box, nav buttons, etc.) - those capture the input themselves and
    /// it never bubbles up to the parent Form as an event. Hooking every child control
    /// individually would work but is fragile and easy to miss one. Application.AddMessageFilter
    /// instead taps into the whole application's message pump - including messages destined
    /// for modal dialogs owned by MainForm - so a single filter reliably sees all activity
    /// regardless of which control it lands on.
    public class ActivityMessageFilter : IMessageFilter
    {
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;

        public event Action ActivityDetected;

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOUSEMOVE:
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MOUSEWHEEL:
                case WM_KEYDOWN:
                    ActivityDetected?.Invoke();
                    break;
            }

            return false; // Never swallow the message - this filter only observes, it doesn't intercept
        }
    }
}