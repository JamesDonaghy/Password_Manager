using System;
using System.Drawing;
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
        /// Set when unlock/setup succeeds and MainForm is shown — FormClosed skips Exit then.
        private bool unlockSucceeded = false;

        // If no master password has been set up yet on this machine, the form switches
        // into "create a master password" mode instead of "log in" mode.
        private readonly bool isFirstRunSetup;

        public LoginForm()
        {
            isFirstRunSetup = !CredentialStore.CredentialExists();

            this.Text = "Password Manager";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;
            // Compact window — less empty space above/below the card.
            this.ClientSize = new Size(440, isFirstRunSetup ? 460 : 380);

            // Closing the login window must end the process. After a successful unlock this
            // form is only Hidden (Application.Run still owns it); after Lock() a *second*
            // LoginForm may be shown. Either way, closing without unlocking should exit.
            this.FormClosed += (s, e) =>
            {
                if (!unlockSucceeded)
                {
                    Application.Exit();
                }
            };

            // --- Centered card content ---
            var card = new Panel
            {
                Width = 340,
                Height = isFirstRunSetup ? 400 : 320,
                BackColor = AppTheme.Background
            };

            // Soft accent tile with lock icon (matches reference welcome screen).
            var iconTile = new Panel
            {
                Size = new Size(52, 52),
                BackColor = AppTheme.AccentSubtle
            };
            var lblIcon = new Label
            {
                Text = "🔒",
                Dock = DockStyle.Fill,
                Font = new Font(AppTheme.Base.FontFamily, 18f),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = AppTheme.AccentSubtle,
                ForeColor = AppTheme.Accent
            };
            iconTile.Controls.Add(lblIcon);

            var lblTitle = new Label
            {
                Text = isFirstRunSetup ? "Set Up Your Vault" : "Welcome Back",
                AutoSize = false,
                Size = new Size(340, 30),
                Font = new Font(AppTheme.Base.FontFamily, 16f, FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = AppTheme.Background
            };

            var lblSubtitle = new Label
            {
                Text = isFirstRunSetup
                    ? "Choose a master password to secure your vault."
                    : "Enter your master password to unlock your vault.",
                AutoSize = false,
                Size = new Size(340, 22),
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = ContentAlignment.TopCenter,
                BackColor = AppTheme.Background
            };

            lblMessage = new Label
            {
                AutoSize = false,
                Size = new Size(340, 18),
                ForeColor = Color.IndianRed,
                Font = AppTheme.Base,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = AppTheme.Background
            };

            // Borderless inputs inside the shared bordered field chrome.
            txtPassword = new TextBox
            {
                PlaceholderText = isFirstRunSetup ? "Master password" : "Enter your master password",
                PasswordChar = '*',
                BorderStyle = BorderStyle.None,
                Font = AppTheme.Base,
                BackColor = AppTheme.Surface
            };
            txtRepeatPassword = new TextBox
            {
                PlaceholderText = "Repeat master password",
                PasswordChar = '*',
                BorderStyle = BorderStyle.None,
                Font = AppTheme.Base,
                BackColor = AppTheme.Surface,
                Visible = isFirstRunSetup
            };

            const int fieldHeight = 36;
            const int cardWidth = 340;
            btnTogglePasswordVisibility = DialogControls.CreateIconButton("👁", 32);
            btnTogglePasswordVisibility.BackColor = AppTheme.Surface;
            btnTogglePasswordVisibility.Click += BtnTogglePasswordVisibility_Click;
            var visibilityTip = new ToolTip();
            visibilityTip.SetToolTip(btnTogglePasswordVisibility, "Show password");
            btnTogglePasswordVisibility.Tag = visibilityTip;

            var passwordField = DialogControls.CreateBorderedFieldRow(txtPassword, btnTogglePasswordVisibility);
            passwordField.Size = new Size(cardWidth, fieldHeight);

            Panel repeatField = null;
            if (isFirstRunSetup)
            {
                repeatField = DialogControls.CreateBorderedFieldRow(txtRepeatPassword);
                repeatField.Size = new Size(cardWidth, fieldHeight);
            }

            var lblPasswordCaption = new Label
            {
                Text = "Master Password",
                AutoSize = false,
                Size = new Size(cardWidth, 18),
                Font = AppTheme.Caption,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = ContentAlignment.BottomLeft,
                BackColor = AppTheme.Background
            };

            Label lblRepeatCaption = null;
            if (isFirstRunSetup)
            {
                lblRepeatCaption = new Label
                {
                    Text = "Repeat Master Password",
                    AutoSize = false,
                    Size = new Size(cardWidth, 18),
                    Font = AppTheme.Caption,
                    ForeColor = AppTheme.TextSecondary,
                    TextAlign = ContentAlignment.BottomLeft,
                    BackColor = AppTheme.Background
                };
            }

            // Full-width primary action.
            btnLogin = DialogControls.CreatePrimaryButton(
                isFirstRunSetup ? "Create Master Password" : "Unlock",
                cardWidth,
                38);
            btnLogin.Click += isFirstRunSetup ? (EventHandler)BtnCreateMasterPassword_Click : BtnLogin_Click;

            var lblEnterHint = new Label
            {
                Text = "⌨  Press Enter to unlock",
                AutoSize = false,
                Size = new Size(cardWidth, 20),
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = AppTheme.Background,
                Visible = !isFirstRunSetup
            };

            // Vertical stack inside the card (manual Y positions for predictable centering).
            int y = 0;
            void Place(Control c, int height, int gapAfter = 0)
            {
                c.Location = new Point(0, y);
                c.Width = cardWidth;
                if (height > 0)
                {
                    c.Height = height;
                }

                card.Controls.Add(c);
                y += (height > 0 ? height : c.Height) + gapAfter;
            }

            iconTile.Location = new Point((cardWidth - iconTile.Width) / 2, y);
            card.Controls.Add(iconTile);
            y += iconTile.Height + 10;

            Place(lblTitle, 28, 4);
            Place(lblSubtitle, 20, 4);
            Place(lblMessage, 18, 4);
            Place(lblPasswordCaption, 18, 2);
            Place(passwordField, fieldHeight, isFirstRunSetup ? 8 : 12);

            if (isFirstRunSetup && lblRepeatCaption != null && repeatField != null)
            {
                Place(lblRepeatCaption, 18, 2);
                Place(repeatField, fieldHeight, 12);
            }

            Place(btnLogin, 38, 10);
            if (!isFirstRunSetup)
            {
                Place(lblEnterHint, 20, 0);
            }

            card.Height = y + 4;

            // Center the card when the form resizes / on first layout.
            void CenterCard()
            {
                card.Left = Math.Max(0, (ClientSize.Width - card.Width) / 2);
                card.Top = Math.Max(0, (ClientSize.Height - card.Height) / 2);
            }

            Controls.Add(card);
            Load += (s, e) => CenterCard();
            Resize += (s, e) => CenterCard();
            CenterCard();

            txtPassword.KeyDown += TextBox_KeyDown;
            txtRepeatPassword.KeyDown += TextBox_KeyDown;

            // Start typing immediately without clicking the field.
            ActiveControl = txtPassword;
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
                // centered) - it doesn't need LoginForm's dimensions copied onto it.
                MainForm mainForm = new MainForm(txtPassword.Text);

                unlockSucceeded = true;
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
            MainForm mainForm = new MainForm(txtPassword.Text);

            unlockSucceeded = true;
            mainForm.Show();
            this.Hide();
        }

        private void BtnTogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;
            txtPassword.PasswordChar = isPasswordVisible ? '\0' : '*';
            txtRepeatPassword.PasswordChar = isPasswordVisible ? '\0' : '*';
            btnTogglePasswordVisibility.Text = isPasswordVisible ? "🙈" : "👁";
            if (btnTogglePasswordVisibility.Tag is ToolTip tip)
            {
                tip.SetToolTip(btnTogglePasswordVisibility, isPasswordVisible ? "Hide password" : "Show password");
            }
        }
    }
}
