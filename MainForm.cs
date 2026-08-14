using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace PasswordManager
{
    public class MainForm : Form
    {
        private DataGridView dgvAccounts;
        private BindingList<Account> accounts; // Use BindingList for automatic updates
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem editEntryMenuItem;
        private ToolStripMenuItem deleteEntryMenuItem;
        private ToolStripMenuItem togglePasswordMenuItem;

        // Tracks which accounts currently have their password shown in the grid.
        // Reference equality (the default for a class with no overridden Equals) is exactly
        // what we want here - each Account is a distinct object, so this only tracks the
        // specific rows the user has chosen to reveal, not accounts with equal-looking data.
        private readonly HashSet<Account> revealedPasswords = new HashSet<Account>();

        // Needed to derive the vault's encryption key (see VaultStorage). Kept only in
        // memory for the lifetime of this form - never written to disk anywhere. Not
        // readonly: ChangeMasterPassword_Click updates it after a successful change so
        // later auto-saves use the new key.
        private string masterPassword;

        public MainForm(string masterPassword)
        {
            this.masterPassword = masterPassword;
            InitializeComponent();
            InitializeDataGridView();
        }

        private void InitializeComponent()
        {
            this.dgvAccounts = new DataGridView();
            this.contextMenu = new ContextMenuStrip();
            this.contextMenu.Items.Add("Add Entry", null, AddEntry_Click);
            this.editEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Edit Entry", null, EditEntry_Click);
            this.deleteEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Delete Entry", null, DeleteEntry_Click);
            this.togglePasswordMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Show Password", null, TogglePasswordVisibility_Click);
            this.contextMenu.Items.Add(new ToolStripSeparator()); // Visually separate row actions from app-level actions
            this.contextMenu.Items.Add("Change Master Password", null, ChangeMasterPassword_Click); // Always available, doesn't depend on row selection
            this.contextMenu.Opening += ContextMenu_Opening; // Enable/disable menu items based on whether a row is selected
            this.dgvAccounts.ContextMenuStrip = this.contextMenu;

            // Right-clicking a row doesn't select it by default in a DataGridView, so without
            // this, Edit/Delete could act on whatever row was last left-clicked instead of the
            // one the user just right-clicked.
            this.dgvAccounts.CellMouseDown += DgvAccounts_CellMouseDown;

            // Masks the Password column's displayed text unless the row has been revealed.
            this.dgvAccounts.CellFormatting += DgvAccounts_CellFormatting;

            this.Controls.Add(this.dgvAccounts);
            this.Text = "Password Manager";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
        }

        private void InitializeDataGridView()
        {
            List<Account> loadedAccounts;
            try
            {
                loadedAccounts = VaultStorage.LoadVault(masterPassword);
            }
            catch (Exception ex)
            {
                // A corrupted/tampered file or a decryption failure both surface here as an
                // exception. There's no recovery mechanism by design, so we fall back to an
                // empty vault rather than crashing - but this does mean the user's previous
                // entries are effectively gone, which is why we tell them clearly.
                MessageBox.Show(
                    $"Your saved vault could not be opened, so it's starting empty: {ex.Message}",
                    "Vault Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                loadedAccounts = new List<Account>();
            }

            accounts = new BindingList<Account>(loadedAccounts);
            accounts.ListChanged += Accounts_ListChanged; // Persist the vault after every Add/Edit/Delete

            dgvAccounts.DataSource = accounts; // Set up DataGridView data binding
            dgvAccounts.Dock = DockStyle.Fill;
            dgvAccounts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Entries are only ever added/edited through AddEntryForm (via the right-click
            // menu), never by typing directly into the grid. Leaving AllowUserToAddRows on
            // shows WinForms' built-in blank "new row" placeholder, and if the grid is sitting
            // on that placeholder when we add to the bound BindingList programmatically, it
            // throws InvalidOperationException. ReadOnly stops inline cell edits too, since
            // those wouldn't be validated or reflected back into the Account objects anyway.
            dgvAccounts.AllowUserToAddRows = false;
            dgvAccounts.AllowUserToDeleteRows = false;
            dgvAccounts.ReadOnly = true;
        }

        private void Accounts_ListChanged(object sender, ListChangedEventArgs e)
        {
            // Fires for every Add, Delete, and ResetItem (used by Edit) on the BindingList,
            // so saving here covers all three without needing a separate save call in each
            // click handler.
            try
            {
                VaultStorage.SaveVault(new List<Account>(accounts), masterPassword);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save your vault: {ex.Message}", "Vault Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddEntry_Click(object sender, EventArgs e)
        {
            using (var addEntryForm = new AddEntryForm(knownUsernames: accounts.Select(a => a.Username)))
            {
                // Get the current cursor position
                var mousePos = Control.MousePosition;
                addEntryForm.StartPosition = FormStartPosition.Manual; // Set to manual
                addEntryForm.Location = new System.Drawing.Point(mousePos.X, mousePos.Y); // Position next to the cursor

                // Show the form as a dialog
                if (addEntryForm.ShowDialog() == DialogResult.OK)
                {
                    var account = new Account
                    {
                        Service = addEntryForm.Service,
                        Username = addEntryForm.Username,
                        Password = addEntryForm.Password,
                        Url = addEntryForm.Url,
                        Notes = addEntryForm.Notes
                    };

                    AddAccount(account); // Attempt to add account
                }
            }
        }

        private void AddAccount(Account account)
        {
            if (account != null)
            {
                try
                {
                    accounts.Add(account); // Add to BindingList
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show($"InvalidOperationException: {ex.Message}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding account: {ex.Message}");
                }
            }
        }

        private void DgvAccounts_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                dgvAccounts.ClearSelection();
                dgvAccounts.Rows[e.RowIndex].Selected = true;
                dgvAccounts.CurrentCell = dgvAccounts.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];
            }
        }

        private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var selectedAccount = dgvAccounts.CurrentRow?.DataBoundItem as Account;
            bool hasSelection = selectedAccount != null;

            editEntryMenuItem.Enabled = hasSelection;
            deleteEntryMenuItem.Enabled = hasSelection;
            togglePasswordMenuItem.Enabled = hasSelection;
            togglePasswordMenuItem.Text = hasSelection && revealedPasswords.Contains(selectedAccount)
                ? "Hide Password"
                : "Show Password";
        }

        private void DgvAccounts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvAccounts.Columns[e.ColumnIndex].Name != nameof(Account.Password))
            {
                return;
            }

            if (!(dgvAccounts.Rows[e.RowIndex].DataBoundItem is Account account) || revealedPasswords.Contains(account))
            {
                return; // Either not a real data row, or this row has been revealed - show the real value
            }

            // Fixed-length mask regardless of the actual password's length, so the mask
            // itself doesn't leak how long the real password is.
            e.Value = "••••••••";
            e.FormattingApplied = true;
        }

        private void TogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            if (!revealedPasswords.Remove(selectedAccount))
            {
                revealedPasswords.Add(selectedAccount);
            }

            dgvAccounts.InvalidateRow(dgvAccounts.CurrentRow.Index); // Force this row's cells to re-format
        }

        private void EditEntry_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            using (var editEntryForm = new AddEntryForm(selectedAccount, accounts.Select(a => a.Username)))
            {
                var mousePos = Control.MousePosition;
                editEntryForm.StartPosition = FormStartPosition.Manual;
                editEntryForm.Location = new System.Drawing.Point(mousePos.X, mousePos.Y);

                if (editEntryForm.ShowDialog() == DialogResult.OK)
                {
                    selectedAccount.Service = editEntryForm.Service;
                    selectedAccount.Username = editEntryForm.Username;
                    selectedAccount.Password = editEntryForm.Password;
                    selectedAccount.Url = editEntryForm.Url;
                    selectedAccount.Notes = editEntryForm.Notes;

                    accounts.ResetItem(accounts.IndexOf(selectedAccount)); // Refresh the grid row to show the updated values
                }
            }
        }

        private void DeleteEntry_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Delete the entry for '{selectedAccount.Service}'? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult == DialogResult.Yes)
            {
                accounts.Remove(selectedAccount);
                revealedPasswords.Remove(selectedAccount);
            }
        }

        private void ChangeMasterPassword_Click(object sender, EventArgs e)
        {
            using (var changeForm = new ChangeMasterPasswordForm())
            {
                var mousePos = Control.MousePosition;
                changeForm.StartPosition = FormStartPosition.Manual;
                changeForm.Location = new System.Drawing.Point(mousePos.X, mousePos.Y);

                if (changeForm.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    // Re-encrypt the vault with the new password first. If this throws, the
                    // credential file below is never touched, so the old password keeps
                    // working for both login and vault decryption - the two can't end up
                    // out of sync with each other.
                    VaultStorage.SaveVault(new List<Account>(accounts), changeForm.NewPassword);
                    CredentialStore.SaveMasterPasswordHash(PasswordHasher.HashPassword(changeForm.NewPassword));
                    masterPassword = changeForm.NewPassword; // Future auto-saves this session should use the new key too

                    MessageBox.Show("Master password changed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not change your master password: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}