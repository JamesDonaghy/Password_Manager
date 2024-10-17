using System;
using System.Windows.Forms;

namespace PasswordManager
{
    public class MainForm : Form
    {
        private TextBox txtService;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtNotes;
        private Button btnAdd;
        private Button btnView;
        private Button btnDelete;
        private DataGridView dgvPasswords;

        public MainForm()
        {
            // Initialize components
            txtService = new TextBox { PlaceholderText = "Service Name", Dock = DockStyle.Top };
            txtUsername = new TextBox { PlaceholderText = "Username", Dock = DockStyle.Top };
            txtPassword = new TextBox { PlaceholderText = "Password", Dock = DockStyle.Top, PasswordChar = '*' };
            txtNotes = new TextBox { PlaceholderText = "Notes", Dock = DockStyle.Top, Multiline = true, Height = 60 };
            btnAdd = new Button { Text = "Add Password", Dock = DockStyle.Top };
            btnView = new Button { Text = "View Passwords", Dock = DockStyle.Top };
            btnDelete = new Button { Text = "Delete Password", Dock = DockStyle.Top };
            dgvPasswords = new DataGridView { Dock = DockStyle.Fill };

            // Add components to the form
            Controls.Add(dgvPasswords);
            Controls.Add(btnDelete);
            Controls.Add(btnView);
            Controls.Add(txtNotes);
            Controls.Add(txtPassword);
            Controls.Add(txtUsername);
            Controls.Add(txtService);
            Controls.Add(btnAdd);

            // Set form properties
            Text = "Password Manager";
            Size = new System.Drawing.Size(400, 600);
        }
    }
}