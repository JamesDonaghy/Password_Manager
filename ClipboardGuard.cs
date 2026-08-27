using System;
using System.Windows.Forms;

namespace PasswordManager
{
    /// <summary>
    /// Copies a value to the clipboard and automatically clears it again a short time
    /// later, so a copied password doesn't sit there indefinitely for other apps/clipboard
    /// history tools to read. Only clears the clipboard if it still holds the value this
    /// guard copied - if the user has since copied something else, that's left alone.
    /// Has no knowledge of Account, the grid, or anything else - just "copy this string
    /// safely."
    /// </summary>
    public class ClipboardGuard
    {
        private readonly System.Windows.Forms.Timer clearTimer;
        private string lastCopiedValue;

        public ClipboardGuard(int autoClearMilliseconds = 25000)
        {
            clearTimer = new System.Windows.Forms.Timer { Interval = autoClearMilliseconds };
            clearTimer.Tick += (sender, e) => ClearIfStillCopied();
        }

        /// Copies the given value to the clipboard and (re)starts the countdown to clear
        /// it again. Repeated calls restart the countdown rather than stacking timers.
        public void CopyAndAutoClear(string value)
        {
            try
            {
                Clipboard.SetText(value);
                lastCopiedValue = value;

                clearTimer.Stop();
                clearTimer.Start();
            }
            catch (Exception ex)
            {
                // The clipboard can occasionally be locked by another app - worth telling
                // the user directly here, since a silent failure would look like copying
                // just didn't do anything.
                MessageBox.Show($"Could not copy to clipboard: {ex.Message}", "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// Clears the clipboard if it still holds the value this guard last copied, and
        /// stops any pending auto-clear timer. Call this when the owning form is closing,
        /// in case the timer didn't get the chance to fire yet - also what the timer's own
        /// Tick handler calls.
        public void ClearIfStillCopied()
        {
            clearTimer.Stop(); // No need for a pending auto-clear if we're clearing right now anyway

            if (lastCopiedValue == null)
            {
                return;
            }

            try
            {
                // Only clear if the clipboard still holds what we copied - if the user has
                // since copied something else, wiping it out would be surprising and unwelcome.
                if (Clipboard.ContainsText() && Clipboard.GetText() == lastCopiedValue)
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception)
            {
                // Best-effort cleanup, whether from the timer or on the way out - not worth
                // interrupting the user (or blocking shutdown) over this failing.
            }

            lastCopiedValue = null;
        }
    }
}
