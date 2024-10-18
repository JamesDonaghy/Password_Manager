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

        // In-memory user store for demonstration
        private Dictionary<string, string> users = new Dictionary<string, string>
        {
            { "admin", "password" }, // Replace with secure storage in production
            { "user1", "pass123" }
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
            lblMessage = new Label { ForeColor = System.Drawing.Color.Red, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };

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

            // Add controls to the FlowLayoutPanel
            flowPanel.Controls.Add(lblMessage);
            flowPanel.Controls.Add(txtUsername);
            flowPanel.Controls.Add(txtPassword);
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
            // Validate credentials
            if (users.TryGetValue(txtUsername.Text, out var password) && password == txtPassword.Text)
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
                lblMessage.Text = "Invalid username or password.";
            }
        }
    }
}