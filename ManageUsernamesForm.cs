using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace PasswordManager
{
    /// Lets the user manually add or delete usernames/emails that show up as autocomplete
    /// suggestions in AddEntryForm, independent of what's actually been saved to the vault.
    /// Every add/delete is saved immediately via UsernameSuggestionsStore - there's no
    /// separate "Save" step to forget.
    public class ManageUsernamesForm : Form
    {
        private ListBox lstUsernames;
        private TextBox txtNewUsername;
        private Button btnAdd;
        private Button btnDelete;
        private Button btnClose;
        private Label lblMessage;
        private BindingList<string> usernames;

        public ManageUsernamesForm()
        {
            this.Text = "Manage Suggested Usernames";
            this.Size = new System.Drawing.Size(400, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            List<string> loadedUsernames;
            try
            {
                loadedUsernames = UsernameSuggestionsStore.LoadUsernames();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load your saved suggestions, so starting with an empty list: {ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                loadedUsernames = new List<string>();
            }

            usernames = new BindingList<string>(loadedUsernames.OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList());

            lstUsernames = new ListBox { Width = 300, Height = 200, DataSource = usernames };

            txtNewUsername = new TextBox { PlaceholderText = "New username/email", Width = 220 };
            txtNewUsername.KeyDown += TxtNewUsername_KeyDown;

            btnAdd = new Button { Text = "Add", Width = 70 };
            btnAdd.Click += BtnAdd_Click;

            var addRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            addRow.Controls.Add(txtNewUsername);
            addRow.Controls.Add(btnAdd);

            btnDelete = new Button { Text = "Delete Selected", Width = 300 };
            btnDelete.Click += BtnDelete_Click;

            btnClose = new Button { Text = "Close", Width = 300 };
            btnClose.Click += (sender, e) => this.Close();

            lblMessage = new Label
            {
                ForeColor = System.Drawing.Color.Red,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(300, 0)
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10)
            };

            flowPanel.Controls.Add(lblMessage);
            flowPanel.Controls.Add(lstUsernames);
            flowPanel.Controls.Add(addRow);
            flowPanel.Controls.Add(btnDelete);
            flowPanel.Controls.Add(btnClose);

            this.Controls.Add(flowPanel);

            // Center the FlowLayoutPanel manually
            flowPanel.Anchor = AnchorStyles.None;
            flowPanel.Left = (this.ClientSize.Width - flowPanel.Width) / 2;
            flowPanel.Top = (this.ClientSize.Height - flowPanel.Height) / 2;
        }

        private void TxtNewUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                BtnAdd_Click(sender, e);
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string newUsername = txtNewUsername.Text.Trim();

            if (string.IsNullOrWhiteSpace(newUsername))
            {
                lblMessage.Text = "Please enter a username.";
                return;
            }

            if (usernames.Any(u => string.Equals(u, newUsername, StringComparison.OrdinalIgnoreCase)))
            {
                lblMessage.Text = "That username is already in the list.";
                return;
            }

            usernames.Add(newUsername); // New entries are appended, not re-sorted into place
            SaveUsernames();
            txtNewUsername.Text = "";
            lblMessage.Text = "";
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (lstUsernames.SelectedItem is string selected)
            {
                usernames.Remove(selected);
                SaveUsernames();
                lblMessage.Text = "";
            }
            else
            {
                lblMessage.Text = "Select a username to delete first.";
            }
        }

        private void SaveUsernames()
        {
            try
            {
                UsernameSuggestionsStore.SaveUsernames(new List<string>(usernames));
            }
            catch (Exception ex)
            {
                lblMessage.Text = $"Could not save: {ex.Message}";
            }
        }
    }
}