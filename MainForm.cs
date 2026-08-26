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
        private TextBox txtSearch;
        private BindingList<Account> accounts; // Use BindingList for automatic updates
        private ContextMenuStrip contextMenu;
        private MenuStrip menuStrip;
        private ToolStripMenuItem editEntryMenuItem;
        private ToolStripMenuItem deleteEntryMenuItem;
        private ToolStripMenuItem togglePasswordMenuItem;
        private ToolStripMenuItem copyPasswordMenuItem;

        // Which column the grid is currently sorted by (an Account property name, e.g.
        // "Service"), and in which direction. Null means unsorted (insertion order).
        private string currentSortColumn;
        private bool currentSortAscending = true;

        // Clears the clipboard a short time after Copy Password, so a copied password
        // doesn't sit there indefinitely for other apps/clipboard history tools to read.
        private readonly System.Windows.Forms.Timer clipboardClearTimer;
        private string lastCopiedPassword;

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

            clipboardClearTimer = new System.Windows.Forms.Timer { Interval = 25000 }; // 25 seconds
            clipboardClearTimer.Tick += ClipboardClearTimer_Tick;

            this.SuspendLayout();
            
            InitializeComponent();
            InitializeDataGridView();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void InitializeComponent()
        {
            this.dgvAccounts = new DataGridView();
            this.contextMenu = new ContextMenuStrip();
            this.contextMenu.Items.Add("Add Entry", null, AddEntry_Click);
            this.editEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Edit Entry", null, EditEntry_Click);
            this.deleteEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Delete Entry", null, DeleteEntry_Click);
            this.togglePasswordMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Show Password", null, TogglePasswordVisibility_Click);
            this.copyPasswordMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Copy Password", null, CopyPassword_Click);
            this.contextMenu.Opening += ContextMenu_Opening; // Enable/disable menu items based on whether a row is selected
            this.dgvAccounts.ContextMenuStrip = this.contextMenu;

            // App-level actions that don't depend on a selected row live in a proper menu
            // bar, not the row context menu.
            this.menuStrip = new MenuStrip { Dock = DockStyle.Top };
            var settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.DropDownItems.Add("Change Master Password", null, ChangeMasterPassword_Click);
            settingsMenu.DropDownItems.Add("Backup Vault...", null, BackupVault_Click);
            settingsMenu.DropDownItems.Add("Restore Vault...", null, RestoreVault_Click);
            this.menuStrip.Items.Add(settingsMenu);
            this.MainMenuStrip = this.menuStrip;

            this.txtSearch = new TextBox { PlaceholderText = "Search by service, username, URL, or notes...", Dock = DockStyle.Top };
            this.txtSearch.TextChanged += TxtSearch_TextChanged;

            // Right-clicking a row doesn't select it by default in a DataGridView, so without
            // this, Edit/Delete could act on whatever row was last left-clicked instead of the
            // one the user just right-clicked.
            this.dgvAccounts.CellMouseDown += DgvAccounts_CellMouseDown;

            // Masks the Password column's displayed text unless the row has been revealed.
            this.dgvAccounts.CellFormatting += DgvAccounts_CellFormatting;

            // Click a column header to sort by it (see ApplyFilter/ApplySort - BindingList<T>
            // doesn't support sorting on its own, so this is handled manually).
            this.dgvAccounts.ColumnHeaderMouseClick += DgvAccounts_ColumnHeaderMouseClick;

            // If a copied password is still sitting on the clipboard when the app closes,
            // the auto-clear timer never gets the chance to fire - clear it here instead.
            this.FormClosing += MainForm_FormClosing;

            // Top-docked controls must be added before the Fill-docked grid - a Fill-docked
            // control added first claims all the space, leaving nothing for a Top-docked
            // control added afterward. Multiple Top-docked controls stack in the order
            // added, so menuStrip ends up above txtSearch.
            this.Controls.Add(this.dgvAccounts);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.menuStrip);

            this.Text = "Password Manager";
            this.Size = new System.Drawing.Size(800, 600);
            this.MinimumSize = new System.Drawing.Size(600, 400); // Keep the grid/search/menu bar usable at small sizes
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
        }

        private void InitializeDataGridView()
        {
            List<Account> loadedAccounts;
            try
            {
                loadedAccounts = VaultStorage.LoadVault(masterPassword, out bool loadedFromBackup);

                if (loadedFromBackup)
                {
                    // The primary vault file failed to decrypt but the backup left behind by
                    // the last save worked, so we recovered - but anything changed since that
                    // backup was written (i.e. the most recent save) won't be reflected here.
                    MessageBox.Show(
                        "Your main vault file couldn't be opened, so it was recovered from a backup. " +
                        "Any changes made in your very last session may be missing.",
                        "Vault Recovered From Backup",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                // Neither the primary vault file nor its backup could be decrypted. There's
                // no further recovery mechanism by design, so we fall back to an empty vault
                // rather than crashing - but this does mean the user's previous entries are
                // effectively gone, which is why we tell them clearly.
                MessageBox.Show(
                    $"Your saved vault could not be opened, so it's starting empty: {ex.Message}",
                    "Vault Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                loadedAccounts = new List<Account>();
            }

            accounts = new BindingList<Account>(loadedAccounts);

            try
            {
                // Backfill: make sure every username already in the vault is captured as a
                // suggestion. Only matters the first time this runs after upgrading, since
                // AddEntry_Click/EditEntry_Click keep the store up to date from here on -
                // but harmless to call every time, since UpsertUsernames only writes when
                // something's actually new.
                UsernameSuggestionsStore.UpsertUsernames(loadedAccounts.Select(a => a.Username));
            }
            catch (Exception)
            {
                // Suggestions are a convenience, not core functionality - don't block
                // startup over this failing.
            }

            accounts.ListChanged += Accounts_ListChanged; // Persist the vault after every Add/Edit/Delete

            dgvAccounts.Dock = DockStyle.Fill;
            dgvAccounts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            ApplyFilter(); // Sets DataSource - starts unfiltered since txtSearch is empty

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

            // The grid is bound to a filtered copy, not accounts directly (see ApplyFilter),
            // so it needs an explicit refresh to pick up the change that just happened.
            ApplyFilter();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filterText = txtSearch.Text.Trim();

            // Deliberately not searching Password - matching against plaintext passwords in
            // a search box isn't something a password manager should be doing, even locally.
            IEnumerable<Account> filtered = string.IsNullOrEmpty(filterText)
                ? accounts
                : accounts.Where(a =>
                    Contains(a.Service, filterText) ||
                    Contains(a.Username, filterText) ||
                    Contains(a.Url, filterText) ||
                    Contains(a.Notes, filterText));

            filtered = ApplySort(filtered);

            dgvAccounts.DataSource = new BindingList<Account>(filtered.ToList());

            // Columns are regenerated whenever DataSource is reassigned (AutoGenerateColumns
            // is on), so both of these need reapplying every time, not just once at startup.
            foreach (DataGridViewColumn column in dgvAccounts.Columns)
            {
                // Programmatic means DataGridView won't attempt its own automatic sorting -
                // which BindingList<T> doesn't support anyway - and instead leaves header
                // clicks entirely to DgvAccounts_ColumnHeaderMouseClick.
                column.SortMode = DataGridViewColumnSortMode.Programmatic;
                column.HeaderCell.SortGlyphDirection = column.Name == currentSortColumn
                    ? (currentSortAscending ? SortOrder.Ascending : SortOrder.Descending)
                    : SortOrder.None;
            }

            // Rows also get rebuilt every time DataSource changes, so re-highlight stale
            // entries here too. Selecting a row still shows the normal selection highlight
            // on top of this - that takes precedence, no conflict.
            foreach (DataGridViewRow row in dgvAccounts.Rows)
            {
                if (row.DataBoundItem is Account account && IsStale(account))
                {
                    row.DefaultCellStyle.BackColor = StaleRowColor;

                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        cell.ToolTipText = $"This entry has no recorded update, or hasn't been " +
                            $"changed in over {StaleEntryThresholdDays} days - consider reviewing it.";
                    }
                }
            }
        }

        // ~6 months - a sensible default, easy to tune if it doesn't feel right in practice.
        private const int StaleEntryThresholdDays = 180;
        private static readonly System.Drawing.Color StaleRowColor = System.Drawing.Color.LightYellow;

        private static bool IsStale(Account account)
        {
            return account.ModifiedAt == null || (DateTime.Now - account.ModifiedAt.Value).TotalDays > StaleEntryThresholdDays;
        }

        private IEnumerable<Account> ApplySort(IEnumerable<Account> source)
        {
            switch (currentSortColumn)
            {
                case nameof(Account.Service):
                    return Sort(source, a => a.Service, StringComparer.OrdinalIgnoreCase);
                case nameof(Account.Username):
                    return Sort(source, a => a.Username, StringComparer.OrdinalIgnoreCase);
                case nameof(Account.Url):
                    return Sort(source, a => a.Url, StringComparer.OrdinalIgnoreCase);
                case nameof(Account.Notes):
                    return Sort(source, a => a.Notes, StringComparer.OrdinalIgnoreCase);
                // Nulls (entries with no timestamp) sort first in ascending order via the
                // default DateTime? comparer - reasonable as "unknown/oldest" by default.
                case nameof(Account.CreatedAt):
                    return Sort(source, a => a.CreatedAt, Comparer<DateTime?>.Default);
                case nameof(Account.ModifiedAt):
                    return Sort(source, a => a.ModifiedAt, Comparer<DateTime?>.Default);
                default:
                    return source;
            }
        }

        private IEnumerable<Account> Sort<TKey>(IEnumerable<Account> source, Func<Account, TKey> keySelector, IComparer<TKey> comparer)
        {
            return currentSortAscending
                ? source.OrderBy(keySelector, comparer)
                : source.OrderByDescending(keySelector, comparer);
        }

        private void DgvAccounts_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string columnName = dgvAccounts.Columns[e.ColumnIndex].Name;

            if (columnName == nameof(Account.Password))
            {
                return; // Deliberately not sortable, same reasoning as excluding it from search
            }

            if (currentSortColumn == columnName)
            {
                currentSortAscending = !currentSortAscending; // Clicking the same column again reverses direction
            }
            else
            {
                currentSortColumn = columnName;
                currentSortAscending = true;
            }

            ApplyFilter(); // Rebuilds the grid with the current filter and sort applied together
        }

        private static bool Contains(string value, string searchText)
        {
            return value != null && value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddEntry_Click(object sender, EventArgs e)
        {
            using (var addEntryForm = new AddEntryForm())
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
                        Notes = addEntryForm.Notes,
                        CreatedAt = DateTime.Now,
                        ModifiedAt = DateTime.Now
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
                    CaptureUsernameSuggestion(account.Username);
                    WarnIfPasswordReused(account);
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

        private void CaptureUsernameSuggestion(string username)
        {
            try
            {
                UsernameSuggestionsStore.UpsertUsername(username);
            }
            catch (Exception)
            {
                // Suggestions are a convenience, not core functionality - the account is
                // already saved regardless of whether this succeeds.
            }
        }

        private void WarnIfPasswordReused(Account savedAccount)
        {
            if (string.IsNullOrEmpty(savedAccount.Password))
            {
                return;
            }

            // Reference inequality (a != savedAccount) excludes the entry itself - for Add,
            // savedAccount is a new object not yet duplicated anywhere; for Edit,
            // savedAccount is the same object already in accounts, just mutated in place.
            List<string> otherServices = accounts
                .Where(a => a != savedAccount && a.Password == savedAccount.Password)
                .Select(a => a.Service)
                .ToList();

            if (otherServices.Count == 0)
            {
                return; // Non-blocking nudge, not a validation rule - nothing to do if unique
            }

            MessageBox.Show(
                $"This password is also used for: {string.Join(", ", otherServices)}.\n\n" +
                "Consider using a unique password for each account.",
                "Password Reused",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
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
            copyPasswordMenuItem.Enabled = hasSelection;
            togglePasswordMenuItem.Text = hasSelection && revealedPasswords.Contains(selectedAccount)
                ? "Hide Password"
                : "Show Password";
        }

        private void DgvAccounts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            string columnName = dgvAccounts.Columns[e.ColumnIndex].Name;

            if (columnName == nameof(Account.Password))
            {
                if (!(dgvAccounts.Rows[e.RowIndex].DataBoundItem is Account account) || revealedPasswords.Contains(account))
                {
                    return; // Either not a real data row, or this row has been revealed - show the real value
                }

                // Fixed-length mask regardless of the actual password's length, so the mask
                // itself doesn't leak how long the real password is.
                e.Value = "••••••••";
                e.FormattingApplied = true;
                return;
            }

            if (columnName == nameof(Account.CreatedAt) || columnName == nameof(Account.ModifiedAt))
            {
                // Null means this entry was saved before these fields existed - show a
                // placeholder rather than a misleading default date.
                e.Value = e.Value is DateTime dateValue ? dateValue.ToString("g") : "-";
                e.FormattingApplied = true;
            }
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

        private void CopyPassword_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            try
            {
                Clipboard.SetText(selectedAccount.Password);
                lastCopiedPassword = selectedAccount.Password;

                clipboardClearTimer.Stop(); // Restart the countdown rather than stacking timers if copied again
                clipboardClearTimer.Start();
            }
            catch (Exception ex)
            {
                // The clipboard can occasionally be locked by another app - worth telling
                // the user directly here, since a silent failure would look like copying
                // just didn't do anything.
                MessageBox.Show($"Could not copy password to clipboard: {ex.Message}", "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClipboardClearTimer_Tick(object sender, EventArgs e)
        {
            clipboardClearTimer.Stop(); // Only clear once per copy, not repeatedly
            ClearClipboardIfStillCopied();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            clipboardClearTimer.Stop();
            ClearClipboardIfStillCopied();
            Application.Exit();
        }

        private void ClearClipboardIfStillCopied()
        {
            if (lastCopiedPassword == null)
            {
                return;
            }

            try
            {
                // Only clear if the clipboard still holds what we copied - if the user has
                // since copied something else, wiping it out would be surprising and unwelcome.
                if (Clipboard.ContainsText() && Clipboard.GetText() == lastCopiedPassword)
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception)
            {
                // Best-effort cleanup, whether from the timer or on the way out - not worth
                // interrupting the user (or blocking shutdown) over this failing.
            }

            lastCopiedPassword = null;
        }

        private void EditEntry_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            using (var editEntryForm = new AddEntryForm(selectedAccount))
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
                    selectedAccount.ModifiedAt = DateTime.Now;

                    CaptureUsernameSuggestion(selectedAccount.Username);
                    WarnIfPasswordReused(selectedAccount);
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

        private void BackupVault_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog { Description = "Choose a folder to save the vault backup to" })
            {
                if (folderDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    string savedPath = VaultStorage.BackupVaultTo(folderDialog.SelectedPath);
                    MessageBox.Show(
                        $"Vault backed up to:\n{savedPath}\n\n" +
                        "This file is still encrypted with your master password - it can only be " +
                        "restored back into this app, not opened directly elsewhere.",
                        "Backup Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not back up the vault: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RestoreVault_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog
            {
                Title = "Choose a vault backup file to restore",
                Filter = "Vault backup files (*.dat)|*.dat|All files (*.*)|*.*"
            })
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var confirmResult = MessageBox.Show(
                    "This will replace your current vault with the selected backup. Your " +
                    "current vault will be kept as a .bak file, but the app needs to restart " +
                    "afterward to safely load the restored data. Continue?",
                    "Confirm Restore",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    // Validates the file decrypts with the current master password before
                    // touching anything - see VaultStorage.RestoreVaultFrom.
                    VaultStorage.RestoreVaultFrom(openFileDialog.FileName, masterPassword);

                    MessageBox.Show(
                        "Vault restored successfully. The app will now close - please reopen it to continue.",
                        "Restore Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not restore the vault: {ex.Message}", "Restore Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}