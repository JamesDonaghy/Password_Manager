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

            // Set form properties
            this.Text = isFirstRunSetup ? "Set Up Master Password" : "Login";
            this.Size = new System.Drawing.Size(1000, 600); // Set size to 1000x600
            this.FormBorderStyle = FormBorderStyle.FixedDialog; // Prevent resizing
            this.MaximizeBox = false; // Disable maximize button
            this.StartPosition = FormStartPosition.CenterScreen; // Center on screen

            // Initialize components
            txtPassword = new TextBox { PlaceholderText = "Master Password", PasswordChar = '*', TextAlign = HorizontalAlignment.Center, Width = 300 };
            txtRepeatPassword = new TextBox { PlaceholderText = "Repeat Master Password", PasswordChar = '*', TextAlign = HorizontalAlignment.Center, Width = 300, Visible = isFirstRunSetup };
            btnLogin = new Button { Text = isFirstRunSetup ? "Create Master Password" : "Login", Width = 300 };
            lblMessage = new Label
            {
                ForeColor = System.Drawing.Color.Red,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = true, // Enable AutoSize
                MaximumSize = new System.Drawing.Size(300, 0) // Set maximum width to prevent cutting off
            };

            // Create the password visibility toggle button
            btnTogglePasswordVisibility = new Button { Text = "👁️", Width = 40, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
            btnTogglePasswordVisibility.Click += BtnTogglePasswordVisibility_Click;

            // Create a FlowLayoutPanel to arrange controls
            FlowLayoutPanel flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Padding(10)
            };

            // Create a panel for the password field and toggle button
            FlowLayoutPanel passwordPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight
            };
            passwordPanel.Controls.Add(txtPassword);
            passwordPanel.Controls.Add(btnTogglePasswordVisibility);

            // Add controls to the FlowLayoutPanel
            flowPanel.Controls.Add(lblMessage);

            if (isFirstRunSetup)
            {
                var introLabel = new Label
                {
                    Text = "No master password is set up on this device yet.\nChoose one to secure your vault.",
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    MaximumSize = new System.Drawing.Size(300, 0)
                };
                flowPanel.Controls.Add(introLabel);
            }

            flowPanel.Controls.Add(passwordPanel); // Add the password panel

            if (isFirstRunSetup)
            {
                flowPanel.Controls.Add(txtRepeatPassword);
            }

            flowPanel.Controls.Add(btnLogin);

            // Add the FlowLayoutPanel to the form
            this.Controls.Add(flowPanel);

            // Center the FlowLayoutPanel manually
            flowPanel.Anchor = AnchorStyles.None;
            flowPanel.Left = (this.ClientSize.Width - flowPanel.Width) / 2;
            flowPanel.Top = (this.ClientSize.Height - flowPanel.Height) / 2;

            // Set event for button click - which handler runs depends on whether we're
            // setting up a master password for the first time or logging in with one
            // that already exists.
            btnLogin.Click += isFirstRunSetup ? (EventHandler)BtnCreateMasterPassword_Click : BtnLogin_Click;

            // Handle key down event for text boxes
            txtPassword.KeyDown += TextBox_KeyDown;
            txtRepeatPassword.KeyDown += TextBox_KeyDown;
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

                // Create and show MainForm with the same size and position
                MainForm mainForm = new MainForm(txtPassword.Text)
                {
                    Size = this.Size, // Set size to 1000x600
                    StartPosition = FormStartPosition.Manual,
                    Location = this.Location // Set location to the same as LoginForm
                };

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
            MainForm mainForm = new MainForm(txtPassword.Text)
            {
                Size = this.Size,
                StartPosition = FormStartPosition.Manual,
                Location = this.Location
            };

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