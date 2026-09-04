using System.Drawing;
using System.Windows.Forms;

namespace PasswordManager
{
    // A small modal confirmation dialog, styled to match the rest of the app via
    // DialogControls (previously left at native Windows dialog defaults - now themed
    // per request).
    //
    // We use this instead of the built-in MessageBox for confirmations like "Delete
    // Entry": MessageBox.Show(owner, ...) is supposed to center itself over the owner
    // window, but in practice that positioning isn't fully reliable (it can land in the
    // center of the screen instead, especially across multiple monitors or certain DPI
    // setups). A regular Form's StartPosition = CenterParent is a real WinForms layout
    // property rather than a native dialog hint, so as long as we call ShowDialog(owner)
    // it always centers over the owner window.
    public class ConfirmationDialog : Form
    {
        public ConfirmationDialog(string message, string title = "Confirm", string yesText = "Yes", string noText = "No")
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent; // Requires ShowDialog(owner) to have an effect
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;

            const int dialogWidth = 400;
            const int contentPadding = 20;
            int messageWidth = dialogWidth - contentPadding * 2;

            // Same warning emoji already used elsewhere in the app (e.g. the wrench/gear
            // icon buttons) rather than the native SystemIcons.Warning bitmap, now that this
            // dialog is themed instead of intentionally native-styled.
            var lblFormTitle = new Label
            {
                Text = "⚠️ " + title,
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(contentPadding, 0, 0, 6)
            };

            // Measure how tall the message needs to be at messageWidth before laying out
            // everything below it, so longer messages (like the restore confirmation)
            // grow the dialog instead of getting clipped at a fixed height.
            var measuredSize = TextRenderer.MeasureText(
                message,
                AppTheme.Base,
                new Size(messageWidth, int.MaxValue),
                TextFormatFlags.WordBreak);
            int messageHeight = measuredSize.Height + 8;

            var lblMessage = new Label
            {
                Text = message,
                Dock = DockStyle.Top,
                Height = messageHeight,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                Padding = new Padding(contentPadding, 0, contentPadding, 0)
            };

            // "Yes" uses the danger button style rather than the usual accent primary -
            // this dialog is only ever used for destructive confirmations (delete, restore/
            // overwrite), so it shouldn't look like the same "normal" action as Save.
            var btnYes = DialogControls.CreateDangerButton(yesText, 90, 32);
            btnYes.Click += (s, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };

            var btnNo = DialogControls.CreateSecondaryButton(noText, 90, 32);
            btnNo.Click += (s, e) => { this.DialogResult = DialogResult.No; this.Close(); };

            const int actionPanelHeight = 62;
            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(contentPadding, 8, contentPadding, 16), BackColor = AppTheme.Background };
            btnNo.Margin = new Padding(0, 3, 0, 3);
            btnYes.Margin = new Padding(10, 3, 0, 3);
            actionPanel.Controls.Add(btnNo);
            actionPanel.Controls.Add(btnYes);

            this.Controls.Add(lblMessage);
            this.Controls.Add(actionPanel);
            this.Controls.Add(lblFormTitle);

            this.AcceptButton = btnNo; // Enter defaults to the safe choice
            this.CancelButton = btnNo; // Esc always cancels the destructive action

            this.ClientSize = new Size(dialogWidth, lblFormTitle.Height + messageHeight + actionPanelHeight);
        }
    }
}