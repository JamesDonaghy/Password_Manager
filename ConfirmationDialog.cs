using System.Drawing;
using System.Windows.Forms;

namespace PasswordManager
{
    // A small modal confirmation dialog, styled similarly to the rest of the app.
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
            // Leave BackColor/Font at their WinForms defaults (SystemColors.Control,
            // SystemFonts.MessageBoxFont) rather than overriding them, so this blends in
            // with standard Windows dialogs instead of looking custom-skinned.

            var font = SystemFonts.MessageBoxFont;
            const int messageWidth = 270;
            const int messageLeft = 60;
            const int messageTop = 20;

            // The system warning icon (same one MessageBox uses) instead of an emoji glyph,
            // so this reads as a native dialog rather than a custom one.
            var picIcon = new PictureBox
            {
                Image = SystemIcons.Warning.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Location = new Point(20, 20)
            };

            // Measure how tall the message needs to be at messageWidth before laying out
            // everything below it, so longer messages (like the restore confirmation)
            // grow the dialog instead of getting clipped at a fixed height.
            var measuredSize = TextRenderer.MeasureText(
                message,
                font,
                new Size(messageWidth, int.MaxValue),
                TextFormatFlags.WordBreak);
            int messageHeight = measuredSize.Height;

            var lblMessage = new Label
            {
                Text = message,
                Font = font,
                AutoSize = false,
                Size = new Size(messageWidth, messageHeight),
                Location = new Point(messageLeft, messageTop)
            };

            int buttonsTop = messageTop + messageHeight + 25;

            var btnYes = new Button
            {
                Text = yesText,
                Width = 90,
                Height = 28,
                Font = font,
                Location = new Point(150, buttonsTop)
            };
            btnYes.Click += (s, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };

            var btnNo = new Button
            {
                Text = noText,
                Width = 90,
                Height = 28,
                Font = font,
                Location = new Point(245, buttonsTop)
            };
            btnNo.Click += (s, e) => { this.DialogResult = DialogResult.No; this.Close(); };

            this.Controls.Add(picIcon);
            this.Controls.Add(lblMessage);
            this.Controls.Add(btnYes);
            this.Controls.Add(btnNo);

            this.AcceptButton = btnNo; // Enter defaults to the safe choice
            this.CancelButton = btnNo; // Esc always cancels the destructive action

            this.ClientSize = new Size(350, buttonsTop + 28 + 20);
        }
    }
}