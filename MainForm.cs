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
        private DataGridView dgvPasswords;
        private TableLayoutPanel tableLayoutPanel;

        public MainForm()
        {
            // Initialize components
            txtService = new TextBox { PlaceholderText = "Service Name", Dock = DockStyle.Fill };
            txtUsername = new TextBox { PlaceholderText = "Username", Dock = DockStyle.Fill };
            txtPassword = new TextBox { PlaceholderText = "Password", Dock = DockStyle.Fill, PasswordChar = '*' };
            txtNotes = new TextBox { PlaceholderText = "Notes", Dock = DockStyle.Fill, Multiline = true, Height = 80 };
            dgvPasswords = new DataGridView { Dock = DockStyle.Fill };

            // Create TableLayoutPanel for better layout management
            tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4, // Adjusted row count
                AutoSize = true,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            // Add controls to the TableLayoutPanel
            tableLayoutPanel.Controls.Add(txtService, 0, 0);
            tableLayoutPanel.Controls.Add(txtUsername, 0, 1);
            tableLayoutPanel.Controls.Add(txtPassword, 0, 2);
            tableLayoutPanel.Controls.Add(txtNotes, 0, 3);
            tableLayoutPanel.SetColumnSpan(txtNotes, 2); // Span notes across two columns
            tableLayoutPanel.SetColumnSpan(dgvPasswords, 2); // Span DataGridView across two columns

            // Add TableLayoutPanel and DataGridView to the form
            Controls.Add(tableLayoutPanel);
            Controls.Add(dgvPasswords); // DataGridView at the bottom

            // Set form properties
            Text = "Password Manager";
            Size = new System.Drawing.Size(1000, 600); // Set size to 1000x600
            StartPosition = FormStartPosition.Manual; // Allow manual positioning
            FormBorderStyle = FormBorderStyle.FixedDialog; // Prevent resizing
            MaximizeBox = false; // Disable maximize button

            // Event handlers
            this.FormClosing += MainForm_FormClosing; // Handle form closing event
        }

        // Handle form closing to stop the application
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit(); // Stop the application
        }
    }
}