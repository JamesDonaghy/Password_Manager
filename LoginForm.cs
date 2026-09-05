using System;
using System.Windows.Forms;

namespace PasswordManager
{
    public class LoginForm : Form
    {
        private TextBox txtPassword;
        private TextBox txtRepeatPassword; // Only shown/used during first-run master password setup
        private Button btnLogin;
        private Label lblMessage;
        private Button btnTogglePasswordVisibility; // Button to toggle password visibility
        private bool isPasswordVisible = false; // Track visibility state

        // If no master password has been set up yet on this machine, the form switches
        // into "create a master password" mode instead of "log in" mode.
        private readonly bool isFirstRunSetup;

        public LoginForm()
        {
            isFirstRunSetup = !CredentialStore.CredentialExists();

            this.Text = isFirstRunSetup ? "Set Up Master Password" : "Login";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;

            const int contentPadding = 24;
            const int headerHeight = 44;
            const int introHeight = 40;
            const int messageHeight = 24;
            const int buttonHeight = 36;

            // Centered, branded heading rather than the other dialogs' left-aligned title -
            // a deliberate exception, since this is the app's actual entry screen rather
            // than a task dialog.
            var lblFormTitle = new Label
            {
                Text = isFirstRunSetup ? "🔒 Set Up Your Vault" : "🔒 Welcome Back",
                Dock = DockStyle.Top,
                Height = headerHeight,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            Label introLabel = null;
            if (isFirstRunSetup)
            {
                introLabel = new Label
                {
                    Text = "No master password is set up on this device yet.\nChoose one to secure your vault.",
                    Dock = DockStyle.Top,
                    Height = introHeight,
                    Font = AppTheme.Base,
                    ForeColor = AppTheme.TextSecondary,
                    TextAlign = System.Drawing.ContentAlignment.TopCenter
                };
            }

            // Fixed-height (rather than AutoSize) so validation messages appearing/
            // disappearing don't reflow the rest of the form - same reasoning as the
            // other dialogs' lblMessage.
            lblMessage = new Label
            {
                Dock = DockStyle.Top,
                Height = messageHeight,
                ForeColor = System.Drawing.Color.IndianRed,
                Font = AppTheme.Base,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            txtPassword = new TextBox { PlaceholderText = "Master Password", PasswordChar = '*', BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtRepeatPassword = new TextBox { PlaceholderText = "Repeat Master Password", PasswordChar = '*', BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base, Visible = isFirstRunSetup };

            // A single-line TextBox always renders at its own font/border-derived height -
            // PreferredHeight - and silently ignores any Height set on it (same issue fixed
            // on ManageUsernamesForm's Add row). Sizing the eye-toggle button, and the field
            // groups below, to that same value is what keeps everything vertically aligned,
            // rather than asking the textbox to match a separate fixed button height.
            int inputHeight = txtPassword.PreferredHeight;
            int fieldGroupHeight = 18 + inputHeight + 10; // Matches DialogControls.CreateFieldGroup's own label/margin constants

            btnTogglePasswordVisibility = DialogControls.CreateIconButton("👁️", inputHeight);
            btnTogglePasswordVisibility.Click += BtnTogglePasswordVisibility_Click;

            // Textbox fills the remaining width, eye-toggle button docked to its right -
            // same pattern as AddEntryForm's password row.
            var passwordRow = new Panel { BackColor = AppTheme.Background };
            passwordRow.Controls.Add(txtPassword);
            txtPassword.Dock = DockStyle.Fill;
            passwordRow.Controls.Add(btnTogglePasswordVisibility);

            var passwordGroup = DialogControls.CreateFieldGroup("Master Password", passwordRow, inputHeight);
            var repeatGroup = isFirstRunSetup ? DialogControls.CreateFieldGroup("Repeat Master Password", txtRepeatPassword, inputHeight) : null;

            // Full-width primary button rather than the other dialogs' right-aligned
            // Save/Cancel row - there's no secondary/cancel action on a login screen, so
            // one full-width call-to-action reads better here.
            btnLogin = DialogControls.CreatePrimaryButton(isFirstRunSetup ? "Create Master Password" : "Login", 0, buttonHeight);
            btnLogin.Dock = DockStyle.Top;

            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(contentPadding, 8, contentPadding, 8), BackColor = AppTheme.Background };

            // Dock=Top siblings stack in reverse of add order (last added ends up closest
            // to the top edge) - same quirk called out throughout the other dialogs.
            contentPanel.Controls.Add(btnLogin);
            if (repeatGroup != null)
            {
                contentPanel.Controls.Add(repeatGroup);
            }
            contentPanel.Controls.Add(passwordGroup);
            contentPanel.Controls.Add(lblMessage);
            if (introLabel != null)
            {
                contentPanel.Controls.Add(introLabel);
            }

            this.Controls.Add(contentPanel);
            this.Controls.Add(lblFormTitle);

            // isFirstRunSetup is fixed for this form's whole lifetime (unlike AddEntryForm's
            // length slider), so the exact content height can just be computed once here
            // instead of needing a resizable/scrollable safety net.
            int contentHeight = headerHeight + messageHeight + fieldGroupHeight + buttonHeight + 16 /* contentPanel top+bottom padding */
                + (introLabel != null ? introHeight : 0)
                + (repeatGroup != null ? fieldGroupHeight : 0);
            this.ClientSize = new System.Drawing.Size(380, contentHeight);

            // Set event for button click - which handler runs depends on whether we're
            // setting up a master password for the first time or logging in with one
            // that already exists.
            btnLogin.Click += isFirstRunSetup ? (EventHandler)BtnCreateMasterPassword_Click : BtnLogin_Click;

            // Handle key down event for text boxes
            txtPassword.KeyDown += TextBox_KeyDown;
            txtRepeatPassword.KeyDown += TextBox_KeyDown;

            // So the person can start typing their master password immediately on launch,
            // without needing to click into the field first. Setting ActiveControl (rather
            // than calling txtPassword.Focus() here) is what actually works before the form
            // has a window handle - WinForms applies it once the form is shown.
            this.ActiveControl = txtPassword;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Prevent the sound by marking the event as handled
                e.SuppressKeyPress = true;

                if (isFirstRunSetup)
                {
                    BtnCreateMasterPassword_Click(sender, e);
                }
                else
                {
                    BtnLogin_Click(sender, e);
                }
            }
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string storedHash;
            try
            {
                storedHash = CredentialStore.LoadMasterPasswordHash();
            }
            catch (Exception ex)
            {
                lblMessage.Text = $"Could not read your saved master password: {ex.Message}";
                return;
            }

            // Verify the entered password against the stored hash (see PasswordHasher for
            // how hashing/verification works).
            if (PasswordHasher.VerifyPassword(txtPassword.Text, storedHash))
            {
                lblMessage.Text = "";

                // MainForm sets its own size/position in its own constructor (1190x650,
                // centered) - it doesn't need LoginForm's now-much-smaller dimensions
                // copied onto it, which is what the old Size/Location overrides here did.
                MainForm mainForm = new MainForm(txtPassword.Text);

                mainForm.Show(); // Show the main form
                this.Hide(); // Hide the login form
            }
            else
            {
                lblMessage.Text = "Incorrect master password";
            }
        }

        private void BtnCreateMasterPassword_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                lblMessage.Text = "Please enter a master password.";
                return;
            }

            if (txtPassword.Text.Length < 8)
            {
                lblMessage.Text = "Master password must be at least 8 characters.";
                return;
            }

            if (txtPassword.Text != txtRepeatPassword.Text)
            {
                lblMessage.Text = "Passwords do not match.";
                return;
            }

            try
            {
                CredentialStore.SaveMasterPasswordHash(PasswordHasher.HashPassword(txtPassword.Text));
            }
            catch (Exception ex)
            {
                lblMessage.Text = $"Could not save your master password: {ex.Message}";
                return;
            }

            lblMessage.Text = "";

            // Proceed straight into the app now that the master password is set up,
            // rather than making the user immediately re-enter it to log in again.
            // MainForm sets its own size/position in its own constructor - see BtnLogin_Click.
            MainForm mainForm = new MainForm(txtPassword.Text);

            mainForm.Show();
            this.Hide();
        }

        private void BtnTogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            isPasswordVisible = !isPasswordVisible; // Toggle visibility state
            txtPassword.PasswordChar = isPasswordVisible ? '\0' : '*'; // Show or hide password
            txtRepeatPassword.PasswordChar = isPasswordVisible ? '\0' : '*'; // Show or hide repeat password
            btnTogglePasswordVisibility.Text = isPasswordVisible ? "🙈" : "👁️"; // Update button icon
        }
    }
}