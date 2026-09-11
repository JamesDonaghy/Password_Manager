using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace PasswordManager
{
    public class MainForm : Form
    {
        private DataGridView dgvAccounts;
        private TextBox txtSearch;
        private Label lblEntryCount;
        private Label lblSidebarAllItemsCount;
        private Panel leftNavPanel;
        private Panel selectedNavRow; // Whichever sidebar nav row (All Items/Favorites/Security/Settings) is currently highlighted - see CreateNavRow/SelectNavRow
        private Panel rightDetailsPanel;
        private Label rightDetailsPlaceholder;
        private Panel detailsContentPanel;
        private ServiceBadgeControl serviceBadge;
        private Label lblDetailsService;
        private Label lblDetailsUrlLink;
        private Label lblDetailsUsername;
        private Label lblDetailsPassword;
        private Button btnDetailsTogglePassword;
        private Panel pnlDetailsStrengthBarContainer;
        private Panel pnlDetailsStrengthBarFill;
        private Label lblDetailsStrengthText;
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

        // Auto-lock: returns to the login screen after 5 minutes with no mouse/keyboard
        // activity anywhere in the app, so a vault left open on an unattended workstation
        // doesn't stay exposed indefinitely. See ActivityMessageFilter for how activity is
        // detected, and Lock() for what actually happens when it's time to lock.
        private static readonly TimeSpan AutoLockTimeout = TimeSpan.FromMinutes(5);
        private readonly System.Windows.Forms.Timer autoLockTimer = new System.Windows.Forms.Timer { Interval = 15000 }; // Checked every 15s; the 5-minute figure above is what actually matters. Fully qualified: System.Threading also has a Timer class, and this project's ImplicitUsings brings that namespace in globally, making the bare name ambiguous.
        private readonly ActivityMessageFilter activityFilter = new ActivityMessageFilter();
        private DateTime lastActivityUtc = DateTime.UtcNow;

        // Set just before deliberately closing this form to lock, so MainForm_FormClosing
        // knows not to treat it as a real application exit (see both for why).
        private bool isLocking;

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
            // Shared themed renderer for both the menu bar and the row context menu below -
            // see AppMenuColorTable for what it actually changes (just colours, not menu
            // behaviour).
            var menuRenderer = new ToolStripProfessionalRenderer(new AppMenuColorTable());

            this.dgvAccounts = new DataGridView();
            this.contextMenu = new ContextMenuStrip { Renderer = menuRenderer, Font = AppTheme.Base, BackColor = AppTheme.Surface, ForeColor = AppTheme.TextPrimary };
            this.contextMenu.Items.Add("Add Entry", null, AddEntry_Click);
            this.editEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Edit Entry", null, EditEntry_Click);
            this.deleteEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Delete Entry", null, DeleteEntry_Click);
            this.togglePasswordMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Show Password", null, TogglePasswordVisibility_Click);
            this.copyPasswordMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Copy Password", null, CopyPassword_Click);
            this.contextMenu.Opening += ContextMenu_Opening; // Enable/disable menu items based on whether a row is selected
            this.dgvAccounts.ContextMenuStrip = this.contextMenu;

            // App-level actions that don't depend on a selected row live in a proper menu
            // bar, not the row context menu.
            this.menuStrip = new MenuStrip { Dock = DockStyle.Top, Renderer = menuRenderer, Font = AppTheme.Base, BackColor = AppTheme.PanelBackground, ForeColor = AppTheme.TextPrimary };
            var settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.DropDownItems.Add("Lock", null, Lock_Click);
            settingsMenu.DropDownItems.Add("Change Master Password", null, ChangeMasterPassword_Click);
            settingsMenu.DropDownItems.Add("Backup Vault...", null, BackupVault_Click);
            settingsMenu.DropDownItems.Add("Restore Vault...", null, RestoreVault_Click);
            this.menuStrip.Items.Add(settingsMenu);
            this.MainMenuStrip = this.menuStrip;

            // Vault header: heading, live entry count, and a real "+ Add Entry" button -
            // previously the only way to add an entry was the row context menu, which
            // isn't discoverable on an empty or unfamiliar vault.
            var btnAddEntry = DialogControls.CreatePrimaryButton("+ Add Entry", 130, 36);
            btnAddEntry.Dock = DockStyle.Right;
            btnAddEntry.Click += AddEntry_Click;

            var lblVaultHeading = new Label
            {
                Text = "Your Vault",
                Dock = DockStyle.Fill,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            var titleRow = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = AppTheme.Background };
            titleRow.Controls.Add(lblVaultHeading);
            titleRow.Controls.Add(btnAddEntry);

            // Text set by UpdateEntryCountLabel() below, called once on initial load and
            // again on every Accounts_ListChanged - always reflects the vault's total count,
            // not the current search's filtered count, matching "Your Vault" being the whole
            // vault rather than a search-results view.
            this.lblEntryCount = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };

            var headerBottomSpacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.Background };

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

            // Owns filtering and the card-style row painting (icon badge, service name,
            // username) - see AccountGridPresenter. Password is no longer shown in the list
            // at all, so it no longer needs passwordRevealTracker; that's only relevant to
            // the details panel now (see RefreshDetailsPanel/TogglePasswordVisibility_Click).
            this.gridPresenter = new AccountGridPresenter(this.dgvAccounts);

            // If a copied password is still sitting on the clipboard when the app closes,
            // the auto-clear timer never gets the chance to fire - clear it here instead.
            this.FormClosing += MainForm_FormClosing;

            // Wire up the auto-lock timer and global activity detector (see the field
            // declarations above and Lock()/MainForm_FormClosing below for the full picture).
            this.activityFilter.ActivityDetected += () => lastActivityUtc = DateTime.UtcNow;
            Application.AddMessageFilter(this.activityFilter);
            this.autoLockTimer.Tick += AutoLockTimer_Tick;
            this.autoLockTimer.Start();

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
                BorderStyle = BorderStyle.None,
                AutoScroll = true // Safety net now that the sidebar holds many more items than before - scrolls rather than clipping on a short window
            };

            // Thin hairline in place of the previous FixedSingle 3D border - separates
            // the nav panel from the grid without the harsh system-drawn bevel.
            var leftNavDivider = new Panel
            {
                Dock = DockStyle.Right,
                Width = 1,
                BackColor = AppTheme.Border
            };

            // All four sidebar entries below are "nav rows" built by CreateNavRow, so they
            // all get the same purple-highlight-plus-accent-bar look and only one is ever
            // highlighted at a time - see CreateNavRow/SelectNavRow further down. This used
            // to be styling unique to "All Items" (a plain Button for the other three, which
            // meant clicking them only ever showed the default focus-rectangle outline and
            // never picked up the highlight, and "All Items" could never lose it).
            this.lblSidebarAllItemsCount = new Label { Dock = DockStyle.Right, Width = 36, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 14, 0) };
            var allItemsRow = CreateNavRow("🗂️ All Items", this.lblSidebarAllItemsCount);
            var btnFavorites = CreateNavRow("⭐ Favorites", null); // Not wired to a real view yet - no Favourites feature to back it (see ServiceBadge/AccountGridPresenter's own placeholder star)
            var btnSecurity = CreateNavRow("🛡️ Security", null); // Placeholder - no security dashboard yet
            var btnSettingsNav = CreateNavRow("⚙️ Settings", null); // Placeholder - Change Master Password/Backup/Restore are reached via the top Settings menu for now (see below)

            SelectNavRow(allItemsRow); // "All Items" is the view shown on load, so it starts out highlighted

            var btnLock = CreateNavButton("🔒 Lock Vault", Lock_Click);
            btnLock.Dock = DockStyle.Bottom; // Pinned to the very bottom regardless of how much is stacked above it, unlike every other nav item here

            var sectionDividerSpacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.PanelBackground };
            var sectionDividerLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = AppTheme.Border };

            // Dock=Top siblings stack in reverse of add order (last added ends up closest
            // to the top edge) - same quirk called out throughout this file - so everything
            // below is added bottom-to-top of its intended visual position. Lock Vault uses
            // Dock=Bottom instead (set above), pinning it to the very bottom regardless of
            // how much is stacked above it, leaving a gap in between - matching the
            // reference design.
            this.leftNavPanel.Controls.Add(btnSettingsNav);
            this.leftNavPanel.Controls.Add(btnSecurity);
            this.leftNavPanel.Controls.Add(sectionDividerLine);
            this.leftNavPanel.Controls.Add(sectionDividerSpacer);
            this.leftNavPanel.Controls.Add(btnFavorites);
            this.leftNavPanel.Controls.Add(allItemsRow);
            this.leftNavPanel.Controls.Add(CreateSidebarSectionLabel("VAULT"));

            this.leftNavPanel.Controls.Add(btnLock);
            this.leftNavPanel.Controls.Add(leftNavDivider);

            // Middle column: search box + grid. Layout/columns unchanged from the original
            // three-column split - just moved into a narrower column instead of spanning
            // the whole window. Same fill/search/sort/reveal/copy behavior as before; only
            // the search box's own appearance and the spacing around it are new here.
            var middlePanel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, Padding = new Padding(16, 14, 16, 0) };
            middlePanel.Controls.Add(this.dgvAccounts);
            middlePanel.Controls.Add(searchSpacer);
            middlePanel.Controls.Add(searchBorder);
            middlePanel.Controls.Add(headerBottomSpacer);
            middlePanel.Controls.Add(this.lblEntryCount);
            middlePanel.Controls.Add(titleRow);

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

            // Header: service icon badge, service name + clickable website link stacked
            // beside it, and a decorative favourite star (no Favourites feature yet to
            // back it - see ServiceBadge.cs / AccountGridPresenter for the same note on
            // the list's own star).
            const int headerHeight = 64;
            this.serviceBadge = new ServiceBadgeControl { Size = new System.Drawing.Size(48, 48) };
            this.serviceBadge.Location = new System.Drawing.Point(0, (headerHeight - this.serviceBadge.Height) / 2);
            var badgeWrapper = new Panel { Dock = DockStyle.Left, Width = 60, BackColor = AppTheme.PanelBackground };
            badgeWrapper.Controls.Add(this.serviceBadge);

            var lblDetailsFavoriteStar = new Label
            {
                Text = "☆",
                Dock = DockStyle.Right,
                Width = 32,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 14f),
                ForeColor = AppTheme.Border,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            lblDetailsService = new Label
            {
                AutoSize = true,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                Margin = new Padding(0)
            };
            lblDetailsUrlLink = new Label
            {
                AutoSize = true,
                Font = AppTheme.Base,
                ForeColor = AppTheme.Accent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 0, 0)
            };
            lblDetailsUrlLink.Click += (sender, e) => OpenSelectedAccountUrl();

            var serviceTextStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = AppTheme.PanelBackground,
                Padding = new Padding(4, 6, 0, 0)
            };
            serviceTextStack.Controls.Add(lblDetailsService);
            serviceTextStack.Controls.Add(lblDetailsUrlLink);

            var headerRow = new Panel { Dock = DockStyle.Top, Height = headerHeight, BackColor = AppTheme.PanelBackground };
            headerRow.Controls.Add(serviceTextStack);
            headerRow.Controls.Add(badgeWrapper);
            headerRow.Controls.Add(lblDetailsFavoriteStar);

            // Username / Password / Website fields, each a caption above a bordered "field
            // box" (DialogControls.CreateBorderedFieldRow) with inline action buttons -
            // replacing the old plain "Caption: value" rows (CreateDetailRow, now removed).
            lblDetailsUsername = new Label { AutoSize = false, AutoEllipsis = true, ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            var btnDetailsCopyUsername = DialogControls.CreateInlineActionButton("Copy", 56, 26);
            btnDetailsCopyUsername.Click += (sender, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount)
                {
                    clipboardGuard.CopyAndAutoClear(selectedAccount.Username);
                }
            };
            var usernameGroup = DialogControls.CreateFieldGroup("USERNAME", DialogControls.CreateBorderedFieldRow(lblDetailsUsername, btnDetailsCopyUsername), 34);

            lblDetailsPassword = new Label { AutoSize = false, AutoEllipsis = true, ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            btnDetailsTogglePassword = DialogControls.CreateInlineActionButton("Show", 56, 26);
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
            var btnDetailsCopyPassword = DialogControls.CreateInlineActionButton("Copy", 56, 26);
            btnDetailsCopyPassword.Click += (sender, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount)
                {
                    clipboardGuard.CopyAndAutoClear(selectedAccount.Password);
                }
            };
            var passwordGroup = DialogControls.CreateFieldGroup("PASSWORD", DialogControls.CreateBorderedFieldRow(lblDetailsPassword, btnDetailsTogglePassword, btnDetailsCopyPassword), 34);

            // Strength meter for the saved password - same PasswordStrength heuristic
            // AddEntryForm's own meter uses (extracted so both agree), just laid out as one
            // row (caption, bar, tier label) instead of AddEntryForm's caption-above style,
            // matching the reference design's compact "Strength [====----] Strong" row.
            var lblStrengthCaption = new Label { Text = "Strength", Dock = DockStyle.Left, Width = 64, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            lblDetailsStrengthText = new Label { Dock = DockStyle.Right, Width = 56, Font = AppTheme.Base, TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            pnlDetailsStrengthBarFill = new Panel { Location = new System.Drawing.Point(0, 0) };
            pnlDetailsStrengthBarContainer = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            pnlDetailsStrengthBarContainer.Controls.Add(pnlDetailsStrengthBarFill);
            // Padding centres the (fixed-height, via Dock=Fill inside it) bar container
            // vertically within the row rather than letting it stretch to the full row
            // height.
            var strengthBarWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8), BackColor = AppTheme.PanelBackground };
            strengthBarWrapper.Controls.Add(pnlDetailsStrengthBarContainer);
            var strengthRow = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = AppTheme.PanelBackground };
            strengthRow.Controls.Add(strengthBarWrapper);
            strengthRow.Controls.Add(lblStrengthCaption);
            strengthRow.Controls.Add(lblDetailsStrengthText);

            lblDetailsUrl = new Label { AutoSize = false, AutoEllipsis = true, ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            var btnDetailsOpenUrl = DialogControls.CreateInlineActionButton("Open", 56, 26);
            btnDetailsOpenUrl.Click += (sender, e) => OpenSelectedAccountUrl();
            var urlGroup = DialogControls.CreateFieldGroup("WEBSITE", DialogControls.CreateBorderedFieldRow(lblDetailsUrl, btnDetailsOpenUrl), 34);

            txtDetailsNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None, // The bordered field row wrapping this provides the border instead - a second one here would double up
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Surface
            };
            var notesGroup = DialogControls.CreateFieldGroup("NOTES", DialogControls.CreateBorderedFieldRow(txtDetailsNotes), 90);

            // Created/Modified side by side rather than stacked, matching the reference -
            // plain text, not boxed like the fields above, since they're not editable/
            // actionable.
            lblDetailsCreated = new Label { AutoSize = true, Font = AppTheme.Base, ForeColor = AppTheme.TextPrimary };
            lblDetailsModified = new Label { AutoSize = true, Font = AppTheme.Base, ForeColor = AppTheme.TextPrimary };
            var createdStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            createdStack.Controls.Add(new Label { Text = "CREATED", AutoSize = true, Font = AppTheme.Caption, ForeColor = AppTheme.TextSecondary });
            createdStack.Controls.Add(lblDetailsCreated);
            var modifiedStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            modifiedStack.Controls.Add(new Label { Text = "MODIFIED", AutoSize = true, Font = AppTheme.Caption, ForeColor = AppTheme.TextSecondary });
            modifiedStack.Controls.Add(lblDetailsModified);

            var metadataRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 44, ColumnCount = 2, RowCount = 1 };
            metadataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            metadataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            metadataRow.Controls.Add(createdStack, 0, 0);
            metadataRow.Controls.Add(modifiedStack, 1, 0);

            // Scrollable body so a longer-than-expected layout (small window, larger DPI/
            // font) grows a scrollbar instead of clipping a field - same safety-net pattern
            // used throughout the dialogs.
            var detailsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.PanelBackground, Padding = new Padding(16, 12, 16, 12) };

            // Dock=Top siblings stack in reverse of add order (last added ends up closest
            // to the top edge) - same quirk called out throughout the dialogs.
            detailsScroll.Controls.Add(metadataRow);
            detailsScroll.Controls.Add(notesGroup);
            detailsScroll.Controls.Add(urlGroup);
            detailsScroll.Controls.Add(strengthRow);
            detailsScroll.Controls.Add(passwordGroup);
            detailsScroll.Controls.Add(usernameGroup);
            detailsScroll.Controls.Add(headerRow);

            // Edit and Delete reuse the exact same handlers as the context menu's Edit
            // Entry/Delete Entry items - both already operate on whatever's currently
            // selected in the grid, which is exactly what this panel is showing.
            var btnDetailsEdit = DialogControls.CreatePrimaryButton("Edit", 80, 32);
            btnDetailsEdit.Click += EditEntry_Click;
            var btnDetailsDelete = DialogControls.CreateDangerButton("Delete", 80, 32);
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

            this.detailsContentPanel.Controls.Add(detailsScroll);
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
            dgvAccounts.CellBorderStyle = DataGridViewCellBorderStyle.None; // Rows draw their own divider instead (see AccountGridPresenter)
            dgvAccounts.RowTemplate.Height = 68; // Tall enough for the icon badge plus two stacked text lines

            // Full-row highlighting instead of the default single-cell selection reads as far
            // more "list of entries, pick one" - closer to the reference apps - and the row
            // header gutter (with its little selector arrow) was pure WinForms chrome that
            // wasn't doing anything for this app, so it's hidden rather than restyled.
            dgvAccounts.RowHeadersVisible = false;
            dgvAccounts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvAccounts.DefaultCellStyle.SelectionBackColor = AppTheme.AccentSubtle;
            dgvAccounts.DefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            dgvAccounts.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
            dgvAccounts.DefaultCellStyle.BackColor = AppTheme.Surface; // Explicit rather than relying on WinForms' own default (white) happening to already match Surface

            gridPresenter.Refresh(accounts, txtSearch.Text); // Starts unfiltered since txtSearch is empty
            UpdateEntryCountLabel();

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

        /// Builds one left-nav action row (currently just "Lock Vault") with the exact same
        /// flat, hover-tinted look as the selectable rows from CreateNavRow below - a Label
        /// docked inside a Panel, no borders. Previously this was a plain Button, which - even
        /// with FlatStyle.Flat and BorderSize 0 - still gets a system-drawn focus/hover box
        /// and a different font from the ambient theme, so it stood out from the rest of the
        /// sidebar instead of matching it. Unlike CreateNavRow, the click here fires the given
        /// action directly rather than going through SelectNavRow, since Lock Vault triggers a
        /// one-off action rather than switching to a persistent selected view.
        private static Panel CreateNavButton(string text, EventHandler onClick)
        {
            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            var row = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = AppTheme.PanelBackground,
                Cursor = Cursors.Hand
            };
            row.Controls.Add(label);

            row.Click += onClick;
            label.Click += onClick;

            EventHandler hoverOn = (s, e) => row.BackColor = AppTheme.AccentSubtle;
            EventHandler hoverOff = (s, e) => row.BackColor = AppTheme.PanelBackground;
            row.MouseEnter += hoverOn;
            row.MouseLeave += hoverOff;
            label.MouseEnter += hoverOn;
            label.MouseLeave += hoverOff;

            return row;
        }

        /// Builds one selectable left-nav row: a text label plus a left accent bar that's
        /// only shown while the row is selected, both wrapped in a Panel so the whole row
        /// highlights (and clicks) as a single unit. An optional trailing label (currently
        /// just the "All Items" entry count) docks to the right. This is what gives every
        /// sidebar entry the same purple-highlight look "All Items" used to have alone -
        /// see SelectNavRow for how the highlight actually moves between rows on click.
        private Panel CreateNavRow(string text, Label trailingLabel)
        {
            var accentBar = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = AppTheme.Accent, Visible = false };
            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            var row = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = AppTheme.PanelBackground,
                Cursor = Cursors.Hand
            };
            row.Controls.Add(label);
            if (trailingLabel != null)
            {
                row.Controls.Add(trailingLabel);
            }
            row.Controls.Add(accentBar);
            row.Tag = accentBar; // Stashed on the row itself so SelectNavRow can flip it on/off without a separate row->accentBar lookup

            // The label docks over the entire row, so in practice it's what receives mouse
            // clicks/hovers, not the row panel underneath it - both are wired to the same
            // handlers so the row behaves the same no matter where on it the pointer is.
            EventHandler select = (s, e) => SelectNavRow(row);
            row.Click += select;
            label.Click += select;

            EventHandler hoverOn = (s, e) => { if (row != selectedNavRow) row.BackColor = AppTheme.AccentSubtle; };
            EventHandler hoverOff = (s, e) => { if (row != selectedNavRow) row.BackColor = AppTheme.PanelBackground; };
            row.MouseEnter += hoverOn;
            row.MouseLeave += hoverOff;
            label.MouseEnter += hoverOn;
            label.MouseLeave += hoverOff;

            return row;
        }

        /// Moves the sidebar's purple highlight (background + left accent bar) onto the
        /// given row, clearing it off whichever row had it before. Called on every nav-row
        /// click, and once up front to give "All Items" the initial highlight - so exactly
        /// one row is ever highlighted at a time, instead of "All Items" being permanently
        /// stuck highlighted while everything else stays unhighlighted no matter what's
        /// clicked.
        private void SelectNavRow(Panel row)
        {
            if (selectedNavRow == row)
            {
                return;
            }

            if (selectedNavRow != null)
            {
                selectedNavRow.BackColor = AppTheme.PanelBackground;
                if (selectedNavRow.Tag is Panel previousAccentBar)
                {
                    previousAccentBar.Visible = false;
                }
            }

            row.BackColor = AppTheme.AccentSubtle;
            if (row.Tag is Panel accentBar)
            {
                accentBar.Visible = true;
            }

            selectedNavRow = row;
        }

        /// Small caption-style section header for the sidebar (e.g. "VAULT", "CATEGORIES") -
        /// not interactive, just a grouping label.
        private static Label CreateSidebarSectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 28,
                Font = AppTheme.Caption,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft,
                Padding = new Padding(14, 0, 0, 4)
            };
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

            serviceBadge.ServiceName = selectedAccount.Service;
            lblDetailsService.Text = selectedAccount.Service;
            lblDetailsUrlLink.Text = selectedAccount.Url;
            lblDetailsUrlLink.Visible = !string.IsNullOrEmpty(selectedAccount.Url);
            lblDetailsUsername.Text = selectedAccount.Username;
            lblDetailsPassword.Text = isRevealed ? selectedAccount.Password : "••••••••";
            btnDetailsTogglePassword.Text = isRevealed ? "Hide" : "Show";
            UpdateDetailsStrengthMeter(selectedAccount.Password); // Strength reflects the real password regardless of whether it's currently shown or masked
            lblDetailsUrl.Text = string.IsNullOrEmpty(selectedAccount.Url) ? "-" : selectedAccount.Url;
            txtDetailsNotes.Text = selectedAccount.Notes;
            lblDetailsCreated.Text = selectedAccount.CreatedAt is DateTime created ? created.ToString("g") : "-";
            lblDetailsModified.Text = selectedAccount.ModifiedAt is DateTime modified ? modified.ToString("g") : "-";
        }

        private void UpdateDetailsStrengthMeter(string password)
        {
            var (score, label, color) = PasswordStrength.Evaluate(password);

            pnlDetailsStrengthBarFill.Height = pnlDetailsStrengthBarContainer.ClientSize.Height;
            pnlDetailsStrengthBarFill.Width = (int)(pnlDetailsStrengthBarContainer.ClientSize.Width * (score / 100.0));
            pnlDetailsStrengthBarFill.BackColor = color;
            lblDetailsStrengthText.Text = label;
            lblDetailsStrengthText.ForeColor = color;
        }

        /// Opens the selected entry's URL in the system's default browser - used by both
        /// the header's clickable link and the Website field's "Open" button.
        private void OpenSelectedAccountUrl()
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount) || string.IsNullOrWhiteSpace(selectedAccount.Url))
            {
                return;
            }

            string url = selectedAccount.Url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url; // Most saved URLs are stored without a scheme (e.g. "youtube.com") - Process.Start needs one
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open this URL: {ex.Message}", "Open URL Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            UpdateEntryCountLabel();
        }

        /// Keeps the "N entries" text under the "Your Vault" heading in sync with the
        /// vault's total size (not the current search's filtered count - see where this is
        /// built for why).
        private void UpdateEntryCountLabel()
        {
            lblEntryCount.Text = accounts.Count == 1 ? "1 entry" : $"{accounts.Count} entries";
            lblSidebarAllItemsCount.Text = accounts.Count.ToString();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            gridPresenter.Refresh(accounts, txtSearch.Text);
        }

        private void AddEntry_Click(object sender, EventArgs e)
        {
            using (var addEntryForm = new AddEntryForm())
            {
                addEntryForm.StartPosition = FormStartPosition.CenterParent;

                // Show the form as a dialog
                if (addEntryForm.ShowDialog(this) == DialogResult.OK)
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

        private void AutoLockTimer_Tick(object sender, EventArgs e)
        {
            // While a modal dialog owned by MainForm (Add/Edit Entry, a confirmation, etc.)
            // is focused, defer locking rather than yanking it away mid-use - just check
            // again next tick. ShowDialog() isn't called consistently with an owner
            // throughout this file, so checking Form.ActiveForm here is what reliably
            // detects "some other window is currently active" regardless of that.
            if (Form.ActiveForm != this)
            {
                return;
            }

            if (DateTime.UtcNow - lastActivityUtc >= AutoLockTimeout)
            {
                Lock();
            }
        }

        private void Lock_Click(object sender, EventArgs e)
        {
            Lock();
        }

        /// Returns to the login screen, requiring the master password to be re-entered,
        /// without ending the application process - used by both the Lock button/menu item
        /// and the auto-lock timer above.
        private void Lock()
        {
            isLocking = true;

            var loginForm = new LoginForm();
            loginForm.Show();
            this.Close(); // Triggers MainForm_FormClosing, which checks isLocking rather than exiting the app
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If a copied password is still sitting on the clipboard, the auto-clear timer
            // never gets the chance to fire on its own - clear it here instead.
            clipboardGuard.ClearIfStillCopied();

            autoLockTimer.Stop();
            Application.RemoveMessageFilter(activityFilter);

            if (isLocking)
            {
                // Lock() already opened a fresh LoginForm - don't fall through to
                // Application.Exit() below.
                return;
            }

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