using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PasswordManager
{
    public class LoginForm : Form
    {
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblMessage;
        private Button btnTogglePasswordVisibility; // Button to toggle password visibility
        private bool isPasswordVisible = false; // Track visibility state

        // In-memory credential store for demonstration. Passwords are hashed with PBKDF2
        // (see PasswordHasher) rather than kept in plain text. This mirrors how credentials
        // would look once loaded from a persisted store - the login logic below reads the
        // same way regardless of where the hashes ultimately come from.
        // TODO: Replace with a persisted credential store once account persistence is added.
        private readonly Dictionary<string, string> userCredentials = new Dictionary<string, string>
        {
            { "admin", PasswordHasher.HashPassword("password") },
            { "user1", PasswordHasher.HashPassword("pass123") }
        };

        public LoginForm()
        {
            // Set form properties
            this.Text = "Login";
            this.Size = new System.Drawing.Size(1000, 600); // Set size to 1000x600
            this.FormBorderStyle = FormBorderStyle.FixedDialog; // Prevent resizing
            this.MaximizeBox = false; // Disable maximize button
            this.StartPosition = FormStartPosition.CenterScreen; // Center on screen

            // Initialize components
            txtUsername = new TextBox { PlaceholderText = "Username", TextAlign = HorizontalAlignment.Center, Width = 300 };
            txtPassword = new TextBox { PlaceholderText = "Password", PasswordChar = '*', TextAlign = HorizontalAlignment.Center, Width = 300 };
            btnLogin = new Button { Text = "Login", Width = 300 };
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
            flowPanel.Controls.Add(txtUsername);
            flowPanel.Controls.Add(passwordPanel); // Add the password panel
            flowPanel.Controls.Add(btnLogin);

            // Add the FlowLayoutPanel to the form
            this.Controls.Add(flowPanel);

            // Center the FlowLayoutPanel manually
            flowPanel.Anchor = AnchorStyles.None;
            flowPanel.Left = (this.ClientSize.Width - flowPanel.Width) / 2;
            flowPanel.Top = (this.ClientSize.Height - flowPanel.Height) / 2;

            // Set event for button click
            btnLogin.Click += BtnLogin_Click;

            // Handle key down event for text boxes
            txtUsername.KeyDown += TextBox_KeyDown;
            txtPassword.KeyDown += TextBox_KeyDown;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Prevent the sound by marking the event as handled
                e.SuppressKeyPress = true;
                BtnLogin_Click(sender, e);
            }
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            // Validate credentials: look up the stored hash for this username, then verify
            // the entered password against it (see PasswordHasher for how hashing works).
            if (userCredentials.TryGetValue(txtUsername.Text, out var storedHash) && PasswordHasher.VerifyPassword(txtPassword.Text, storedHash))
            {
                lblMessage.Text = "";

                // Create and show MainForm with the same size and position
                MainForm mainForm = new MainForm
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
                lblMessage.Text = "Invalid username or password"; // Updated error message
            }
        }

        private void BtnTogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            isPasswordVisible = !isPasswordVisible; // Toggle visibility state
            txtPassword.PasswordChar = isPasswordVisible ? '\0' : '*'; // Show or hide password
            btnTogglePasswordVisibility.Text = isPasswordVisible ? "🙈" : "👁️"; // Update button icon
        }
    }
}