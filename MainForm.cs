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
        private Panel leftNavPanel;
        private Panel rightDetailsPanel;
        private Label rightDetailsPlaceholder;
        private Panel detailsContentPanel;
        private Label lblDetailsService;
        private Label lblDetailsUsername;
        private Label lblDetailsPassword;
        private Button btnDetailsTogglePassword;
        private Label lblDetailsUrl;
        private TextBox txtDetailsNotes;
        private Label lblDetailsCreated;
        private Label lblDetailsModified;
        private BindingList<Account> accounts; // Use BindingList for automatic updates
        private ContextMenuStrip contextMenu;
        private MenuStrip menuStrip;
        private ToolStripMenuItem editEntryMenuItem;
        private ToolStripMenuItem deleteEntryMenuItem;
        private ToolStripMenuItem togglePasswordMenuItem;
        private ToolStripMenuItem copyPasswordMenuItem;

        private AccountGridPresenter gridPresenter;

        // Clears the clipboard a short time after Copy Password, so a copied password
        // doesn't sit there indefinitely for other apps/clipboard history tools to read.
        private readonly ClipboardGuard clipboardGuard = new ClipboardGuard();

        // Tracks which accounts currently have their password shown in the grid.
        private readonly RevealedPasswordTracker passwordRevealTracker = new RevealedPasswordTracker();

        // Needed to derive the vault's encryption key (see VaultStorage). Kept only in
        // memory for the lifetime of this form - never written to disk anywhere. Not
        // readonly: ChangeMasterPassword_Click updates it after a successful change so
        // later auto-saves use the new key.
        private string masterPassword;

        public MainForm(string masterPassword)
        {
            this.masterPassword = masterPassword;

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

            this.txtSearch = new TextBox
            {
                PlaceholderText = "Search by service, username, URL, or notes...",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                Font = AppTheme.Base
            };
            this.txtSearch.TextChanged += TxtSearch_TextChanged;

            // A borderless TextBox can't show its own padding/border - a 1px coloured
            // outer panel plus a padded inner panel gives the inset "search field" look
            // (matching the reference apps) without an owner-drawn control. Same hairline-
            // via-BackColor trick used for the nav panel divider. The inset panel uses the
            // same white Surface colour as the TextBox itself (not the page background),
            // so the padding and the text field read as one uniform white field rather
            // than a white box sitting inside a mismatched gutter.
            var searchInset = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Surface,
                Padding = new Padding(10, 6, 10, 6)
            };
            searchInset.Controls.Add(this.txtSearch);

            var searchBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(1),
                BackColor = AppTheme.Border
            };
            searchBorder.Controls.Add(searchInset);

            // Empty spacer so the search field doesn't sit flush against the grid below it.
            var searchSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 12,
                BackColor = AppTheme.Background
            };

            // Right-clicking a row doesn't select it by default in a DataGridView, so without
            // this, Edit/Delete could act on whatever row was last left-clicked instead of the
            // one the user just right-clicked.
            this.dgvAccounts.CellMouseDown += DgvAccounts_CellMouseDown;

            // Owns filtering, sorting, column setup, password masking, date formatting,
            // and stale-entry highlighting - see AccountGridPresenter for why reveal-state
            // is passed in as a predicate rather than owned by it.
            this.gridPresenter = new AccountGridPresenter(this.dgvAccounts, passwordRevealTracker.IsRevealed);

            // If a copied password is still sitting on the clipboard when the app closes,
            // the auto-clear timer never gets the chance to fire - clear it here instead.
            this.FormClosing += MainForm_FormClosing;

            // --- Three-column layout ---
            // Using a TableLayoutPanel with explicit column positions rather than more
            // stacked Dock-style siblings, since we've already been burned once by
            // same-Dock-style sibling ordering being non-obvious (the menu bar/search box
            // overlap bug). Explicit cell positions avoid that ambiguity entirely.

            // Left column: navigation. Reuses the exact same handlers already wired to the
            // top Settings menu (Backup Vault, Restore Vault, Change Master Password) -
            // nothing new is being built here, just an additional way to reach it. The old
            // menu stays as-is for now, per instruction, until it's decided the sidebar
            // fully replaces it.
            this.leftNavPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.PanelBackground,
                BorderStyle = BorderStyle.None
            };

            // Thin hairline in place of the previous FixedSingle 3D border - separates
            // the nav panel from the grid without the harsh system-drawn bevel.
            var leftNavDivider = new Panel
            {
                Dock = DockStyle.Right,
                Width = 1,
                BackColor = AppTheme.Border
            };

            var btnSettings = CreateNavButton("Change Master Password", ChangeMasterPassword_Click); // Same handler as the existing Settings menu item
            var btnRestoreVault = CreateNavButton("Restore Vault", RestoreVault_Click); // Same handler as the existing menu item
            var btnBackupVault = CreateNavButton("Backup Vault", BackupVault_Click); // Same handler as the existing menu item

            // Stacked Dock=Top siblings render in reverse of the order added (last added
            // ends up closest to the top edge) - see the earlier menu bar/search box fix
            // for why this is called out explicitly rather than assumed.
            this.leftNavPanel.Controls.Add(btnSettings);
            this.leftNavPanel.Controls.Add(btnRestoreVault);
            this.leftNavPanel.Controls.Add(btnBackupVault);
            this.leftNavPanel.Controls.Add(leftNavDivider);

            // Middle column: search box + grid. Layout/columns unchanged from the original
            // three-column split - just moved into a narrower column instead of spanning
            // the whole window. Same fill/search/sort/reveal/copy behavior as before; only
            // the search box's own appearance and the spacing around it are new here.
            var middlePanel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, Padding = new Padding(16, 14, 16, 0) };
            middlePanel.Controls.Add(this.dgvAccounts);
            middlePanel.Controls.Add(searchSpacer);
            middlePanel.Controls.Add(searchBorder);

            // Right column: entry details. Content is built once here and just updated
            // in-place on selection change, rather than rebuilt each time.
            this.rightDetailsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.PanelBackground,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.rightDetailsPlaceholder = new Label
            {
                Text = "Select an entry to see details",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                ForeColor = AppTheme.TextSecondary
            };

            this.detailsContentPanel = new Panel { Dock = DockStyle.Fill, Visible = false };

            lblDetailsService = new Label
            {
                // AutoSize (rather than Dock = Fill) so the TableLayoutPanel's AutoSize row
                // can read this label's true preferred height for its 14pt bold font. With
                // Dock = Fill and AutoSize off, the row had no reliable height to measure
                // against and ended up too short, clipping the bottom of the text.
                AutoSize = true,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = new Padding(10, 10, 0, 4)
            };

            lblDetailsUsername = new Label
            {
                AutoSize = false,
                AutoEllipsis = true, // Long values (e.g. full emails) get "..." instead of forcing the row to wrap
                Width = 150,
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            var btnDetailsCopyUsername = new Button { Text = "Copy", Width = 60, Height = 24 };
            btnDetailsCopyUsername.Click += (sender, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount)
                {
                    clipboardGuard.CopyAndAutoClear(selectedAccount.Username);
                }
            };

            lblDetailsPassword = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Width = 100,
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            btnDetailsTogglePassword = new Button { Text = "Show", Width = 60, Height = 24 };
            btnDetailsTogglePassword.Click += (sender, e) =>
            {
                if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
                {
                    return;
                }

                passwordRevealTracker.Toggle(selectedAccount);
                dgvAccounts.InvalidateRow(dgvAccounts.CurrentRow.Index); // Keep the grid's own masking in sync too
                RefreshDetailsPanel();
            };
            var btnDetailsCopyPassword = new Button { Text = "Copy", Width = 60, Height = 24 };
            btnDetailsCopyPassword.Click += (sender, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount)
                {
                    clipboardGuard.CopyAndAutoClear(selectedAccount.Password);
                }
            };

            lblDetailsUrl = new Label { AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };

            txtDetailsNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Height = 60,
                Width = 160,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblDetailsCreated = new Label { AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            lblDetailsModified = new Label { AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };

            var detailsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            for (int i = 0; i < 7; i++)
            {
                detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Spacer - absorbs leftover space

            detailsLayout.Controls.Add(lblDetailsService, 0, 0);
            detailsLayout.Controls.Add(CreateDetailRow("Username:", lblDetailsUsername, btnDetailsCopyUsername), 0, 1);
            detailsLayout.Controls.Add(CreateDetailRow("Password:", lblDetailsPassword, btnDetailsTogglePassword, btnDetailsCopyPassword), 0, 2);
            detailsLayout.Controls.Add(CreateDetailRow("URL:", lblDetailsUrl), 0, 3);
            detailsLayout.Controls.Add(CreateDetailRow("Notes:", txtDetailsNotes), 0, 4);
            detailsLayout.Controls.Add(CreateDetailRow("Created:", lblDetailsCreated), 0, 5);
            detailsLayout.Controls.Add(CreateDetailRow("Modified:", lblDetailsModified), 0, 6);

            // Edit and Delete reuse the exact same handlers as the context menu's Edit
            // Entry/Delete Entry items - both already operate on whatever's currently
            // selected in the grid, which is exactly what this panel is showing.
            var btnDetailsEdit = new Button { Text = "Edit", Width = 80, Height = 32 };
            btnDetailsEdit.Click += EditEntry_Click;
            var btnDetailsDelete = new Button { Text = "Delete", Width = 80, Height = 32 };
            btnDetailsDelete.Click += DeleteEntry_Click;
            var detailsActionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(10)
            };
            detailsActionsPanel.Controls.Add(btnDetailsDelete);
            detailsActionsPanel.Controls.Add(btnDetailsEdit);

            this.detailsContentPanel.Controls.Add(detailsLayout);
            this.detailsContentPanel.Controls.Add(detailsActionsPanel);

            this.rightDetailsPanel.Controls.Add(this.detailsContentPanel);
            this.rightDetailsPanel.Controls.Add(this.rightDetailsPlaceholder);

            // Updates the details panel whenever the selected row changes - including
            // clearing back to the placeholder when nothing is selected (e.g. right after
            // the grid refreshes following an Add/Edit/Delete, which doesn't currently
            // preserve selection - a known rough edge for a later pass).
            this.dgvAccounts.SelectionChanged += (sender, e) => RefreshDetailsPanel();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // Left nav
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Middle - takes remaining space
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340)); // Right details - widened so the Copy/Show buttons fit next to the value on one line instead of wrapping below it
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.Controls.Add(this.leftNavPanel, 0, 0);
            mainLayout.Controls.Add(middlePanel, 1, 0);
            mainLayout.Controls.Add(this.rightDetailsPanel, 2, 0);

            this.Controls.Add(mainLayout);
            this.Controls.Add(this.menuStrip);

            this.Text = "Password Manager";
            this.Font = AppTheme.Base; // Applies to every child control that doesn't set its own Font
            this.BackColor = AppTheme.Background;
            this.Size = new System.Drawing.Size(1190, 650); // Wider than before - right column grew by 90px, so the window grows to match rather than squeezing the grid
            this.MinimumSize = new System.Drawing.Size(790, 450); // Keep all three columns usable at small sizes
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

            // --- Grid appearance ---
            // Grid-level styles (as opposed to per-column ones) survive AccountGridPresenter
            // rebinding DataSource on every Refresh(), so these only need setting once here.
            dgvAccounts.BackgroundColor = AppTheme.Background;
            dgvAccounts.BorderStyle = BorderStyle.None;
            dgvAccounts.GridColor = AppTheme.Border;
            dgvAccounts.RowTemplate.Height = 32; // A bit more breathing room than the ~22px default

            // Full-row highlighting instead of the default single-cell selection reads as far
            // more "list of entries, pick one" - closer to the reference apps - and the row
            // header gutter (with its little selector arrow) was pure WinForms chrome that
            // wasn't doing anything for this app, so it's hidden rather than restyled.
            dgvAccounts.RowHeadersVisible = false;
            dgvAccounts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvAccounts.DefaultCellStyle.SelectionBackColor = AppTheme.AccentSubtle;
            dgvAccounts.DefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            dgvAccounts.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;

            // EnableHeadersVisualStyles must be off for ColumnHeadersDefaultCellStyle to take
            // effect at all - otherwise Windows' own visual-styles renderer draws the header
            // and ignores these colours entirely.
            dgvAccounts.EnableHeadersVisualStyles = false;
            dgvAccounts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvAccounts.ColumnHeadersHeight = 36;
            dgvAccounts.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.PanelBackground;
            dgvAccounts.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.TextSecondary;
            dgvAccounts.ColumnHeadersDefaultCellStyle.Font = AppTheme.Caption; // Reuses the same caption style as the details panel's field labels
            dgvAccounts.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppTheme.PanelBackground; // Headers aren't selectable rows - keep them looking static, not "selected"
            dgvAccounts.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppTheme.TextSecondary;
            dgvAccounts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            gridPresenter.Refresh(accounts, txtSearch.Text); // Starts unfiltered since txtSearch is empty

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

        /// Builds one left-nav button with the app's flat, hover-tinted style, keeping
        /// all three nav buttons visually identical without repeating the same setup.
        private static Button CreateNavButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.PanelBackground,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AppTheme.AccentSubtle;
            button.FlatAppearance.MouseDownBackColor = AppTheme.AccentSubtle;
            button.Click += onClick;
            return button;
        }

        /// Builds one "Caption: value [buttons]" row for the details panel, keeping every
        /// row's layout consistent without repeating the same FlowLayoutPanel setup each time.
        private static Control CreateDetailRow(string caption, Control valueControl, params Control[] extraControls)
        {
            // A FlowLayoutPanel was used here previously, but it wraps to a new line
            // whenever caption + value + buttons don't all fit on one row - which is
            // exactly what was happening to the Username/Password rows (buttons ended up
            // stranded on their own line below). A TableLayoutPanel with a dedicated,
            // never-shrinking column for the buttons keeps them pinned to the right of the
            // row instead: the value column simply ellipsizes if it runs out of room.
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10, 5, 10, 5)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));   // Caption
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Value - takes whatever space is left
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));   // Buttons - always keeps its full width

            var captionLabel = new Label
            {
                Text = caption,
                AutoSize = true,
                Font = AppTheme.Caption,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 8, 0)
            };

            valueControl.Margin = new Padding(0, 3, 8, 0);
            if (valueControl is Label && !(valueControl is TextBox))
            {
                valueControl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }

            row.Controls.Add(captionLabel, 0, 0);
            row.Controls.Add(valueControl, 1, 0);

            if (extraControls.Length > 0)
            {
                var buttonsPanel = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    Margin = new Padding(0)
                };
                foreach (Control extra in extraControls)
                {
                    buttonsPanel.Controls.Add(extra);
                }
                row.Controls.Add(buttonsPanel, 2, 0);
            }

            return row;
        }

        /// Updates the right-hand details panel to match whatever's currently selected in
        /// the grid, or shows the placeholder if nothing is selected. Reads password reveal
        /// state from the same passwordRevealTracker the grid itself uses, so the two always
        /// agree with each other.
        private void RefreshDetailsPanel()
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                detailsContentPanel.Visible = false;
                rightDetailsPlaceholder.Visible = true;
                return;
            }

            rightDetailsPlaceholder.Visible = false;
            detailsContentPanel.Visible = true;

            bool isRevealed = passwordRevealTracker.IsRevealed(selectedAccount);

            lblDetailsService.Text = selectedAccount.Service;
            lblDetailsUsername.Text = selectedAccount.Username;
            lblDetailsPassword.Text = isRevealed ? selectedAccount.Password : "••••••••";
            btnDetailsTogglePassword.Text = isRevealed ? "Hide" : "Show";
            lblDetailsUrl.Text = string.IsNullOrEmpty(selectedAccount.Url) ? "-" : selectedAccount.Url;
            txtDetailsNotes.Text = selectedAccount.Notes;
            lblDetailsCreated.Text = selectedAccount.CreatedAt is DateTime created ? created.ToString("g") : "-";
            lblDetailsModified.Text = selectedAccount.ModifiedAt is DateTime modified ? modified.ToString("g") : "-";
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

            // The grid is bound to a filtered copy, not accounts directly (see
            // AccountGridPresenter), so it needs an explicit refresh to pick up the change
            // that just happened.
            gridPresenter.Refresh(accounts, txtSearch.Text);
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            gridPresenter.Refresh(accounts, txtSearch.Text);
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
            togglePasswordMenuItem.Text = hasSelection && passwordRevealTracker.IsRevealed(selectedAccount)
                ? "Hide Password"
                : "Show Password";
        }

        private void TogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            passwordRevealTracker.Toggle(selectedAccount);
            dgvAccounts.InvalidateRow(dgvAccounts.CurrentRow.Index); // Force this row's cells to re-format
        }

        private void CopyPassword_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            clipboardGuard.CopyAndAutoClear(selectedAccount.Password);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If a copied password is still sitting on the clipboard, the auto-clear timer
            // never gets the chance to fire on its own - clear it here instead.
            clipboardGuard.ClearIfStillCopied();

            // LoginForm called Hide() rather than Close() when login succeeded, so it's
            // still technically open - and Application.Run (in Program.cs) is watching
            // LoginForm's lifetime, since that's the form the app started with. Without
            // this, closing MainForm alone doesn't end the application; the process keeps
            // running invisibly even though no window is left open.
            Application.Exit();
        }

        private void EditEntry_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            using (var editEntryForm = new AddEntryForm(selectedAccount))
            {
                editEntryForm.StartPosition = FormStartPosition.CenterParent;

                if (editEntryForm.ShowDialog(this) == DialogResult.OK)
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

            DialogResult confirmResult;
            using (var confirmDialog = new ConfirmationDialog(
                $"Delete the entry for '{selectedAccount.Service}'? This cannot be undone.",
                "Confirm Delete"))
            {
                confirmResult = confirmDialog.ShowDialog(this);
            }

            if (confirmResult == DialogResult.Yes)
            {
                accounts.Remove(selectedAccount);
                passwordRevealTracker.Forget(selectedAccount);
            }
        }

        private void ChangeMasterPassword_Click(object sender, EventArgs e)
        {
            using (var changeForm = new ChangeMasterPasswordForm())
            {
                var mousePos = Control.MousePosition;
                changeForm.StartPosition = FormStartPosition.CenterParent;
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

                DialogResult confirmResult;
                using (var confirmDialog = new ConfirmationDialog(
                    "This will replace your current vault with the selected backup. Your " +
                    "current vault will be kept as a .bak file, but the app needs to restart " +
                    "afterward to safely load the restored data. Continue?",
                    "Confirm Restore"))
                {
                    confirmResult = confirmDialog.ShowDialog(this);
                }

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