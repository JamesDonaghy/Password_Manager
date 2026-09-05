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
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen; // No owner is passed at the call site (see AddEntryForm), so CenterParent wouldn't have anything to center on
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;
            this.Size = new System.Drawing.Size(400, 480);

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

            lstUsernames = new ListBox { DataSource = usernames, BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };

            txtNewUsername = new TextBox { PlaceholderText = "New username/email", BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtNewUsername.KeyDown += TxtNewUsername_KeyDown;

            // A single-line TextBox always renders at its own font/border-derived height -
            // PreferredHeight - and silently ignores any Height we set on it. Sizing the Add
            // button (and the field group below) to that same value, instead of a separate
            // hardcoded number, is what actually keeps them visually aligned.
            int addRowHeight = txtNewUsername.PreferredHeight;

            btnAdd = DialogControls.CreatePrimaryButton("Add", 70, addRowHeight);
            btnAdd.Dock = DockStyle.Right; // CreatePrimaryButton doesn't set Dock itself (most callers place it in a FlowLayoutPanel action row instead) - this usage needs it explicitly, same as btnDelete below
            btnAdd.Click += BtnAdd_Click;

            // Textbox fills the remaining width, Add button docked to its right - same
            // pattern as AddEntryForm's username row.
            var addRow = new Panel { BackColor = AppTheme.Background };
            addRow.Controls.Add(txtNewUsername);
            txtNewUsername.Dock = DockStyle.Fill;
            addRow.Controls.Add(btnAdd);

            // "Delete Selected" removes data (destructive), so it gets the same danger-red
            // treatment as ConfirmationDialog's Yes button rather than a plain button.
            btnDelete = DialogControls.CreateDangerButton("Delete Selected", 0, 32);
            btnDelete.Dock = DockStyle.Top;
            btnDelete.Click += BtnDelete_Click;

            btnClose = DialogControls.CreateSecondaryButton("Close", 96, 32);
            btnClose.Click += (sender, e) => this.Close();

            // Fixed-height (rather than AutoSize) so validation messages appearing/
            // disappearing don't reflow the rest of the dialog - same reasoning as
            // ChangeMasterPasswordForm's lblMessage.
            lblMessage = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = System.Drawing.Color.IndianRed, // Same semantic red used throughout for validation/mismatch feedback
                Font = AppTheme.Base,
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };

            var lblFormTitle = new Label
            {
                Text = "Manage Suggested Usernames",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft,
                Padding = new Padding(20, 0, 0, 6)
            };

            var listGroup = DialogControls.CreateFieldGroup("Suggested Usernames", lstUsernames, 180);
            var addGroup = DialogControls.CreateFieldGroup("Add New Username", addRow, addRowHeight);

            // Scrollable body so a longer-than-expected list of controls doesn't clip -
            // same safety net used on the other dialogs.
            var contentPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 2, 20, 2), BackColor = AppTheme.Background };

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(20, 8, 20, 12), BackColor = AppTheme.Background };
            actionPanel.Controls.Add(btnClose);

            // Dock=Top siblings stack in reverse of add order (last added ends up closest
            // to the top edge) - same quirk called out throughout the other dialogs.
            contentPanel.Controls.Add(btnDelete);
            contentPanel.Controls.Add(addGroup);
            contentPanel.Controls.Add(listGroup);
            contentPanel.Controls.Add(lblMessage);

            this.Controls.Add(contentPanel);
            this.Controls.Add(actionPanel);
            this.Controls.Add(lblFormTitle);
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