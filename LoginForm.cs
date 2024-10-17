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

        // In-memory user store for demonstration (username:password)
        private Dictionary<string, string> users = new Dictionary<string, string>
        {
            { "admin", "password" }, // Replace with secure storage in production
            { "user1", "pass123" }
        };

        public LoginForm()
        {
            // Initialize components
            txtUsername = new TextBox { PlaceholderText = "Username", Dock = DockStyle.Top };
            txtPassword = new TextBox { PlaceholderText = "Password", Dock = DockStyle.Top, PasswordChar = '*' };
            btnLogin = new Button { Text = "Login", Dock = DockStyle.Top };
            lblMessage = new Label { Dock = DockStyle.Top, ForeColor = System.Drawing.Color.Red };

            // Add components to the form
            Controls.Add(lblMessage);
            Controls.Add(btnLogin);
            Controls.Add(txtPassword);
            Controls.Add(txtUsername);

            // Set form properties
            Text = "Login";
            Size = new System.Drawing.Size(300, 200);

            // Button click event
            btnLogin.Click += BtnLogin_Click;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            // Validate credentials
            if (users.TryGetValue(txtUsername.Text, out var password) && password == txtPassword.Text)
            {
                lblMessage.Text = "";
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                lblMessage.Text = "Invalid username or password.";
            }
        }
    }
}