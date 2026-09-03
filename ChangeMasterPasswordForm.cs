using System;
using System.Windows.Forms;

namespace PasswordManager
{
    /// Collects and validates a master password change. This form does not persist
    /// anything itself - it only verifies the current password and hands the new one
    /// back via NewPassword, leaving MainForm in control of the actual re-encryption
    /// order (vault first, then credential hash - see MainForm.ChangeMasterPassword_Click).
    public class ChangeMasterPasswordForm : Form
    {
        private TextBox txtCurrentPassword;
        private TextBox txtNewPassword;
        private TextBox txtRepeatNewPassword;
        private Label lblMessage;
        private Button btnSave;
        private Button btnCancel;

        public string NewPassword { get; private set; }

        public ChangeMasterPasswordForm()
        {
            this.Text = "Change Master Password";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen; // No owner is passed to ShowDialog() at the call site, so CenterParent wouldn't have anything to center on
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;
            this.Size = new System.Drawing.Size(440, 420);

            const int inputHeight = 30;

            txtCurrentPassword = new TextBox { PlaceholderText = "Current Master Password", PasswordChar = '*', BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtNewPassword = new TextBox { PlaceholderText = "New Master Password", PasswordChar = '*', BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtRepeatNewPassword = new TextBox { PlaceholderText = "Repeat New Master Password", PasswordChar = '*', BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };

            // Fixed-height (rather than AutoSize) so validation messages appearing/
            // disappearing don't reflow or resize the rest of the dialog around them.
            lblMessage = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                ForeColor = System.Drawing.Color.IndianRed, // Same semantic red as AddEntryForm's mismatch/weak-strength feedback
                Font = AppTheme.Base,
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };

            btnSave = DialogControls.CreatePrimaryButton("Change Password", 150, 32);
            btnSave.Click += BtnSave_Click;

            btnCancel = DialogControls.CreateSecondaryButton("Cancel", 96, 32);
            btnCancel.Click += (sender, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            var lblFormTitle = new Label
            {
                Text = "Change Master Password",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft,
                Padding = new Padding(20, 0, 0, 6)
            };

            var currentGroup = DialogControls.CreateFieldGroup("Current Master Password", txtCurrentPassword, inputHeight);
            var newGroup = DialogControls.CreateFieldGroup("New Master Password", txtNewPassword, inputHeight);
            var repeatGroup = DialogControls.CreateFieldGroup("Repeat New Master Password", txtRepeatNewPassword, inputHeight);

            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 2, 20, 2), BackColor = AppTheme.Background };

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(20, 8, 20, 12), BackColor = AppTheme.Background };
            btnCancel.Margin = new Padding(0, 3, 0, 3);
            btnSave.Margin = new Padding(10, 3, 0, 3);
            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnSave);

            // Dock=Top siblings stack in reverse of add order (last added ends up closest to
            // the top edge) - same quirk called out in MainForm/AddEntryForm.
            contentPanel.Controls.Add(repeatGroup);
            contentPanel.Controls.Add(newGroup);
            contentPanel.Controls.Add(currentGroup);
            contentPanel.Controls.Add(lblMessage);

            Controls.Add(contentPanel);
            Controls.Add(actionPanel);
            Controls.Add(lblFormTitle);

            txtCurrentPassword.KeyDown += TextBox_KeyDown;
            txtNewPassword.KeyDown += TextBox_KeyDown;
            txtRepeatNewPassword.KeyDown += TextBox_KeyDown;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                BtnSave_Click(sender, e);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCurrentPassword.Text))
            {
                lblMessage.Text = "Please enter your current master password.";
                return;
            }

            string storedHash;
            try
            {
                storedHash = CredentialStore.LoadMasterPasswordHash();
            }
            catch (Exception ex)
            {
                lblMessage.Text = $"Could not verify your current password: {ex.Message}";
                return;
            }

            if (!PasswordHasher.VerifyPassword(txtCurrentPassword.Text, storedHash))
            {
                lblMessage.Text = "Current master password is incorrect.";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewPassword.Text))
            {
                lblMessage.Text = "Please enter a new master password.";
                return;
            }

            if (txtNewPassword.Text.Length < 8)
            {
                lblMessage.Text = "New master password must be at least 8 characters.";
                return;
            }

            if (txtNewPassword.Text != txtRepeatNewPassword.Text)
            {
                lblMessage.Text = "New passwords do not match.";
                return;
            }

            NewPassword = txtNewPassword.Text;
            lblMessage.Text = "";
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}