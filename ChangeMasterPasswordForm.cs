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
            this.Size = new System.Drawing.Size(450, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            txtCurrentPassword = new TextBox { PlaceholderText = "Current Master Password", PasswordChar = '*', TextAlign = HorizontalAlignment.Center, Width = 300 };
            txtNewPassword = new TextBox { PlaceholderText = "New Master Password", PasswordChar = '*', TextAlign = HorizontalAlignment.Center, Width = 300 };
            txtRepeatNewPassword = new TextBox { PlaceholderText = "Repeat New Master Password", PasswordChar = '*', TextAlign = HorizontalAlignment.Center, Width = 300 };

            lblMessage = new Label
            {
                ForeColor = System.Drawing.Color.Red,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(300, 0)
            };

            btnSave = new Button { Text = "Change Password", Width = 300 };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Width = 300 };
            btnCancel.Click += (sender, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            FlowLayoutPanel flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Padding(10)
            };

            flowPanel.Controls.Add(lblMessage);
            flowPanel.Controls.Add(txtCurrentPassword);
            flowPanel.Controls.Add(txtNewPassword);
            flowPanel.Controls.Add(txtRepeatNewPassword);
            flowPanel.Controls.Add(btnSave);
            flowPanel.Controls.Add(btnCancel);

            this.Controls.Add(flowPanel);

            // Center the FlowLayoutPanel manually
            flowPanel.Anchor = AnchorStyles.None;
            flowPanel.Left = (this.ClientSize.Width - flowPanel.Width) / 2;
            flowPanel.Top = (this.ClientSize.Height - flowPanel.Height) / 2;

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