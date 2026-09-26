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
        private ComboBox cmbSort;
        private Label lblSidebarAllItemsCount;
        private Panel leftNavPanel;
        private Panel selectedNavRow; // Whichever sidebar nav row (All Items/Favorites/Security/Settings) is currently highlighted - see CreateNavRow/SelectNavRow
        private Panel allItemsNavRow; // Reselected whenever we navigate back to the vault view - see ShowVaultView
        private bool showFavoritesOnly; // When true, the grid only shows favourited entries
        private Panel middlePanel;
        private Panel securityMiddlePanel;
        private Panel securityRightPanel;
        private Label lblHealthTotal;
        private Label lblHealthExposedCount;
        private Label lblHealthReusedCount;
        private Label lblHealthWeakCount;
        private Label lblHealthOldCount;
        private Label lblIssueReusedDesc;
        private Label lblIssueWeakDesc;
        private Label lblIssueOldDesc;
        private Label lblIssueReusedCount;
        private Label lblIssueWeakCount;
        private Label lblIssueOldCount;
        private Label lblSecurityRightTitle;
        private Label lblSecurityRightHint;
        private Panel securityIssueEntriesHost;
        private PasswordHealth.IssueKind? activeSecurityIssue;
        private Panel settingsMiddlePanel;
        private Panel settingsRightPanel;
        private Panel generatorMiddlePanel;
        private Panel generatorRightPanel;
        private TextBox txtGeneratedPassword;
        private NumericUpDown nudGeneratorLength;
        private CheckBox chkGeneratorUppercase;
        private CheckBox chkGeneratorLowercase;
        private CheckBox chkGeneratorDigits;
        private CheckBox chkGeneratorSymbols;
        private Panel generatorPasswordPanel;
        private Panel generatorPassphrasePanel;
        private Label btnTabPassword;
        private Label btnTabPassphrase;
        private Panel tabUnderlinePassword;
        private Panel tabUnderlinePassphrase;
        private TextBox txtGeneratedPassphrase;
        private NumericUpDown nudPassphraseWords;
        private CheckBox chkPassphraseCapitalize;
        private CheckBox chkPassphraseNumbers;
        private CheckBox chkPassphraseSpecial;
        private Label lblGeneratorRightHeading;
        private Label lblGeneratorRightHint;
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
        private ToolStripMenuItem copyUsernameMenuItem;
        private ToolStripMenuItem copyPasswordMenuItem;
        private ToolStripMenuItem autoTypeMenuItem;
        private ToolStripMenuItem editEntryMenuItem;
        private ToolStripMenuItem deleteEntryMenuItem;

        private AccountGridPresenter gridPresenter;

        // Clears the clipboard a short time after Copy Password, so a copied password
        // doesn't sit there indefinitely for other apps/clipboard history tools to read.
        private readonly ClipboardGuard clipboardGuard = new ClipboardGuard();

        // Tracks which accounts currently have their password shown in the grid.
        private readonly RevealedPasswordTracker passwordRevealTracker = new RevealedPasswordTracker();

        // Auto-lock: returns to the login screen after a configurable period of inactivity
        // (see AppPreferences.AutoLockMinutes / Security page). 0 minutes = disabled.
        // See ActivityMessageFilter for activity detection, and Lock() for what happens
        // when it's time to lock.
        private readonly System.Windows.Forms.Timer autoLockTimer = new System.Windows.Forms.Timer { Interval = 15000 }; // Checked every 15s; the preference minutes figure is what actually matters. Fully qualified: System.Threading also has a Timer class, and this project's ImplicitUsings brings that namespace in globally, making the bare name ambiguous.
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
            // Copy actions first (keyboard: Ctrl+X username, Ctrl+C password), then a
            // divider, then entry management. Show/Hide Password lives only on the details
            // panel now - keeping it out of the context menu reduces clutter.
            // ShortcutKeyDisplayString shows the hint right-aligned; actual handling is in
            // ProcessCmdKey (ShortcutKeys is left unset so it doesn't fight that path).
            this.copyUsernameMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Copy Username", null, CopyUsername_Click);
            this.copyUsernameMenuItem.ShortcutKeyDisplayString = "Ctrl+X";
            this.copyPasswordMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Copy Password", null, CopyPassword_Click);
            this.copyPasswordMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            // KeePass-style auto-type: username + Tab + password into the focused window
            // (usually a browser login form). Placed directly under Copy Password.
            this.autoTypeMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Perform Auto-Type", null, AutoType_Click);
            this.autoTypeMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
            this.contextMenu.Items.Add(new ToolStripSeparator());
            this.contextMenu.Items.Add("Add Entry", null, AddEntry_Click);
            this.editEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Edit Entry", null, EditEntry_Click);
            this.deleteEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Delete Entry", null, DeleteEntry_Click);
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

            var headerBottomSpacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background };

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
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = AppTheme.Border
            };
            searchBorder.Controls.Add(searchInset);

            // Sort control sits beside search in a matching bordered field (see reference UI).
            // Display-only; does not change vault order on disk.
            this.cmbSort = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = AppTheme.Base,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary
            };
            this.cmbSort.Items.AddRange(new object[]
            {
                "Sort: Name (A–Z)",
                "Sort: Name (Z–A)",
                "Sort: Username (A–Z)",
                "Sort: Recently modified",
                "Sort: Recently created",
                "Sort: Favourites first"
            });
            // Index applied after gridPresenter exists so the saved preference both selects
            // the combo and drives the first list refresh.
            this.cmbSort.SelectedIndexChanged += CmbSort_SelectedIndexChanged;

            var sortInset = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Surface,
                Padding = new Padding(8, 6, 4, 6)
            };
            sortInset.Controls.Add(this.cmbSort);

            var sortBorder = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                Padding = new Padding(1),
                BackColor = AppTheme.Border
            };
            sortBorder.Controls.Add(sortInset);

            // Gap between search and sort fields.
            var searchSortGap = new Panel
            {
                Dock = DockStyle.Right,
                Width = 10,
                BackColor = AppTheme.Background
            };

            // One row: search (fill) + gap + sort (fixed), same height as the old search bar.
            var searchSortRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = AppTheme.Background
            };
            searchSortRow.Controls.Add(searchBorder);
            searchSortRow.Controls.Add(searchSortGap);
            searchSortRow.Controls.Add(sortBorder);

            // Empty spacer so the search/sort row doesn't sit flush against the grid below it.
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
            // the details panel now (see RefreshDetailsPanel).
            this.gridPresenter = new AccountGridPresenter(this.dgvAccounts);
            this.gridPresenter.FavoriteToggled += OnFavoriteToggled;

            // Restore last sort choice before the first Refresh in InitializeDataGridView.
            this.gridPresenter.SortMode = AppPreferences.SortMode;
            this.cmbSort.SelectedIndex = SortModeToComboIndex(AppPreferences.SortMode);

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

            // Sidebar nav rows share the same purple-highlight-plus-accent-bar look; only one is
            // highlighted at a time - see CreateNavRow/SelectNavRow further down.
            this.lblSidebarAllItemsCount = new Label { Dock = DockStyle.Right, Width = 36, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 14, 0) };
            this.allItemsNavRow = CreateNavRow("🗂️ All Items", this.lblSidebarAllItemsCount, ShowAllItemsView);
            var btnFavorites = CreateNavRow("⭐ Favorites", null, ShowFavoritesView);
            var btnGenerator = CreateNavRow("🔑 Generator", null, ShowGeneratorView);
            var btnSecurity = CreateNavRow("🛡️ Security", null, ShowSecurityView);
            var btnSettingsNav = CreateNavRow("⚙️ Settings", null, ShowSettingsView);

            SelectNavRow(this.allItemsNavRow); // "All Items" is the view shown on load, so it starts out highlighted

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
            this.leftNavPanel.Controls.Add(btnGenerator);
            this.leftNavPanel.Controls.Add(sectionDividerLine);
            this.leftNavPanel.Controls.Add(sectionDividerSpacer);
            this.leftNavPanel.Controls.Add(btnFavorites);
            this.leftNavPanel.Controls.Add(this.allItemsNavRow);
            this.leftNavPanel.Controls.Add(CreateSidebarSectionLabel("VAULT"));

            this.leftNavPanel.Controls.Add(btnLock);
            this.leftNavPanel.Controls.Add(leftNavDivider);

            // Middle column: search box + grid. Layout/columns unchanged from the original
            // three-column split - just moved into a narrower column instead of spanning
            // the whole window. Same fill/search/sort/reveal/copy behavior as before; only
            // the search box's own appearance and the spacing around it are new here.
            var middlePanel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, Padding = new Padding(16, 14, 16, 0) };
            this.middlePanel = middlePanel;
            middlePanel.Controls.Add(this.dgvAccounts);
            middlePanel.Controls.Add(searchSpacer);
            middlePanel.Controls.Add(searchSortRow);
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

            BuildSecurityPage(out this.securityMiddlePanel, out this.securityRightPanel);
            this.securityMiddlePanel.Visible = false;
            this.securityRightPanel.Visible = false;

            BuildSettingsPage(out this.settingsMiddlePanel, out this.settingsRightPanel);
            this.settingsMiddlePanel.Visible = false;
            this.settingsRightPanel.Visible = false;

            BuildGeneratorPage(out this.generatorMiddlePanel, out this.generatorRightPanel);
            this.generatorMiddlePanel.Visible = false;
            this.generatorRightPanel.Visible = false;

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
            // Security/Settings/Generator pages share the same two cells as the vault's
            // middle/right panels - only one of each pair is ever Visible at a time
            // (see ShowVaultView/ShowSecurityView/ShowGeneratorView), the same overlapping-
            // panel technique already used for rightDetailsPlaceholder vs. detailsContentPanel.
            mainLayout.Controls.Add(this.securityMiddlePanel, 1, 0);
            mainLayout.Controls.Add(this.securityRightPanel, 2, 0);
            mainLayout.Controls.Add(this.settingsMiddlePanel, 1, 0);
            mainLayout.Controls.Add(this.settingsRightPanel, 2, 0);
            mainLayout.Controls.Add(this.generatorMiddlePanel, 1, 0);
            mainLayout.Controls.Add(this.generatorRightPanel, 2, 0);

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
            dgvAccounts.RowTemplate.Height = 52; // Compact row: badge + two stacked text lines without excess vertical padding

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

            gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly); // Starts unfiltered since txtSearch is empty
            UpdateEntryCountLabel();

            // Entries are only ever added/edited through AddEntryForm (via the right-click
            // menu), never by typing directly into the grid. Leaving AllowUserToAddRows on
            // shows WinForms' built-in blank "new row" placeholder, and if the grid is sitting
            // on that placeholder when we add to the bound BindingList programmatically, it
            // throws InvalidOperationException. ReadOnly stops inline cell edits too, since
            // those wouldn't be validated or reflected back into the Account objects anyway.
            dgvAccounts.AllowUserToAddRows = false;
            dgvAccounts.AllowUserToDeleteRows = false;
            dgvAccounts.AllowUserToResizeRows = false; // Fixed row height - dragging to resize entries is not supported
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
        /// just the "All Items" entry count) docks to the right, and an optional onSelected
        /// callback fires after the row is highlighted - used by All Items/Security to also
        /// switch which view (vault vs. security) is showing; Favorites/Settings still pass
        /// null since there's no view behind them yet. This is what gives every sidebar
        /// entry the same purple-highlight look "All Items" used to have alone - see
        /// SelectNavRow for how the highlight actually moves between rows on click.
        private Panel CreateNavRow(string text, Label trailingLabel, Action onSelected = null)
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
            EventHandler select = (s, e) => { SelectNavRow(row); onSelected?.Invoke(); };
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

        /// Switches the middle/right columns back to the vault view (grid + entry details),
        /// hiding the Security/Settings/Generator pages' panels.
        private void ShowVaultPanels()
        {
            securityMiddlePanel.Visible = false;
            securityRightPanel.Visible = false;
            settingsMiddlePanel.Visible = false;
            settingsRightPanel.Visible = false;
            generatorMiddlePanel.Visible = false;
            generatorRightPanel.Visible = false;
            middlePanel.Visible = true;
            rightDetailsPanel.Visible = true;
        }

        /// Shows all vault entries (clears the favourites-only filter). Nav highlight is
        /// already set by CreateNavRow before this runs.
        private void ShowAllItemsView()
        {
            showFavoritesOnly = false;
            ShowVaultPanels();
            gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly);
        }

        /// Shows only favourited entries. Nav highlight is already set by CreateNavRow.
        private void ShowFavoritesView()
        {
            showFavoritesOnly = true;
            ShowVaultPanels();
            gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly);
        }

        /// Used by Security/Settings back arrows: return to the full vault list and highlight
        /// "All Items".
        private void ShowVaultView()
        {
            showFavoritesOnly = false;
            ShowVaultPanels();
            SelectNavRow(allItemsNavRow);
            gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly);
        }

        /// Switches the middle/right columns to the Security page, hiding the vault's grid
        /// and entry details panels. The Security nav row is already highlighted by the time
        /// this runs (CreateNavRow calls SelectNavRow before invoking this).
        private void ShowSecurityView()
        {
            middlePanel.Visible = false;
            rightDetailsPanel.Visible = false;
            settingsMiddlePanel.Visible = false;
            settingsRightPanel.Visible = false;
            generatorMiddlePanel.Visible = false;
            generatorRightPanel.Visible = false;
            securityMiddlePanel.Visible = true;
            securityRightPanel.Visible = true;
            RefreshSecurityHealth();
        }

        /// Switches the middle/right columns to the Settings page, hiding the vault's grid
        /// and entry details panels. The Settings nav row is already highlighted by the time
        /// this runs (CreateNavRow calls SelectNavRow before invoking this).
        private void ShowSettingsView()
        {
            middlePanel.Visible = false;
            rightDetailsPanel.Visible = false;
            securityMiddlePanel.Visible = false;
            securityRightPanel.Visible = false;
            generatorMiddlePanel.Visible = false;
            generatorRightPanel.Visible = false;
            settingsMiddlePanel.Visible = true;
            settingsRightPanel.Visible = true;
        }

        /// Switches the middle/right columns to the Generator page (placeholder in Stage 1).
        private void ShowGeneratorView()
        {
            middlePanel.Visible = false;
            rightDetailsPanel.Visible = false;
            securityMiddlePanel.Visible = false;
            securityRightPanel.Visible = false;
            settingsMiddlePanel.Visible = false;
            settingsRightPanel.Visible = false;
            generatorMiddlePanel.Visible = true;
            generatorRightPanel.Visible = true;
        }

        /// Builds the Generator page with Password / Passphrase tabs.
        private void BuildGeneratorPage(out Panel generatorMiddle, out Panel generatorRight)
        {
            // --- Tab strip ---
            btnTabPassword = new Label
            {
                Text = "Password Generator",
                AutoSize = false,
                Width = 160,
                Height = 32,
                Font = AppTheme.Base,
                ForeColor = AppTheme.Accent,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Location = new System.Drawing.Point(0, 0)
            };
            btnTabPassword.Click += (s, e) => ShowGeneratorTab(passwordTab: true);

            btnTabPassphrase = new Label
            {
                Text = "Passphrase Generator",
                AutoSize = false,
                Width = 170,
                Height = 32,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Location = new System.Drawing.Point(170, 0)
            };
            btnTabPassphrase.Click += (s, e) => ShowGeneratorTab(passwordTab: false);

            tabUnderlinePassword = new Panel
            {
                Height = 2,
                Width = 150,
                BackColor = AppTheme.Accent,
                Location = new System.Drawing.Point(0, 32)
            };
            tabUnderlinePassphrase = new Panel
            {
                Height = 2,
                Width = 160,
                BackColor = AppTheme.Accent,
                Location = new System.Drawing.Point(170, 32),
                Visible = false
            };

            var tabStrip = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = AppTheme.Background };
            tabStrip.Controls.Add(btnTabPassword);
            tabStrip.Controls.Add(btnTabPassphrase);
            tabStrip.Controls.Add(tabUnderlinePassword);
            tabStrip.Controls.Add(tabUnderlinePassphrase);

            var tabBottomLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = AppTheme.Border };
            var spacerAfterTabs = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background };

            generatorPasswordPanel = BuildPasswordGeneratorContent();
            generatorPassphrasePanel = BuildPassphraseGeneratorContent();
            generatorPassphrasePanel.Visible = false;

            var bodyHost = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
            bodyHost.Controls.Add(generatorPasswordPanel);
            bodyHost.Controls.Add(generatorPassphrasePanel);

            // Dock=Top reverse: body first (fill), then spacers/tabs on top
            generatorMiddle = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, Padding = new Padding(16, 14, 16, 0) };
            generatorMiddle.Controls.Add(bodyHost);
            generatorMiddle.Controls.Add(spacerAfterTabs);
            generatorMiddle.Controls.Add(tabBottomLine);
            generatorMiddle.Controls.Add(tabStrip);

            lblGeneratorRightHeading = new Label
            {
                Text = "Password Generator",
                Dock = DockStyle.Top,
                Height = 32,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            lblGeneratorRightHint = new Label
            {
                Text = "Adjust length and character types, then copy the password into a vault entry.",
                Dock = DockStyle.Top,
                Height = 80,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.TopCenter
            };

            generatorRight = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.PanelBackground, Padding = new Padding(16, 20, 16, 20) };
            generatorRight.Controls.Add(lblGeneratorRightHint);
            generatorRight.Controls.Add(lblGeneratorRightHeading);

            RegeneratePasswordFromOptions();
            RegeneratePassphraseFromOptions();
        }

        private Panel BuildPasswordGeneratorContent()
        {
            var lblHeading = new Label
            {
                Text = "Password Generator",
                Dock = DockStyle.Top,
                Height = 32,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary
            };
            var lblSubtitle = new Label
            {
                Text = "Create strong, unique passwords",
                Dock = DockStyle.Top,
                Height = 22,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };
            var spacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background };

            txtGeneratedPassword = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 11f),
                ReadOnly = true
            };

            var btnCopyGenerated = DialogControls.CreateInlineActionButton("Copy", 56, 26);
            btnCopyGenerated.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtGeneratedPassword.Text))
                {
                    clipboardGuard.CopyAndAutoClear(txtGeneratedPassword.Text);
                }
            };

            var passwordRow = DialogControls.CreateBorderedFieldRow(txtGeneratedPassword, btnCopyGenerated);
            passwordRow.Dock = DockStyle.Top;
            passwordRow.Height = 40;

            var lblLength = new Label
            {
                Text = "Length",
                Dock = DockStyle.Left,
                Width = 60,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            nudGeneratorLength = new NumericUpDown
            {
                Minimum = PasswordGenerator.MinLength,
                Maximum = PasswordGenerator.MaxLength,
                Value = PasswordGenerator.DefaultLength,
                Width = 72,
                Font = AppTheme.Base,
                Dock = DockStyle.Left
            };
            nudGeneratorLength.ValueChanged += (s, e) => RegeneratePasswordFromOptions();

            var lengthRow = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = AppTheme.Background };
            lengthRow.Controls.Add(nudGeneratorLength);
            lengthRow.Controls.Add(lblLength);

            chkGeneratorUppercase = CreateGeneratorOptionCheckBox("Uppercase (A–Z)", true);
            chkGeneratorLowercase = CreateGeneratorOptionCheckBox("Lowercase (a–z)", true);
            chkGeneratorDigits = CreateGeneratorOptionCheckBox("Numbers (0–9)", true);
            chkGeneratorSymbols = CreateGeneratorOptionCheckBox("Symbols (!@#$…)", true);

            var optionsStack = new Panel { Dock = DockStyle.Top, Height = 32 * 4, BackColor = AppTheme.Background };
            optionsStack.Controls.Add(chkGeneratorSymbols);
            optionsStack.Controls.Add(chkGeneratorDigits);
            optionsStack.Controls.Add(chkGeneratorLowercase);
            optionsStack.Controls.Add(chkGeneratorUppercase);

            var lblOptions = new Label
            {
                Text = "Include",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 9f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft
            };

            var btnGenerate = DialogControls.CreatePrimaryButton("Generate", 120, 36);
            btnGenerate.Dock = DockStyle.Left;
            btnGenerate.Click += (s, e) => RegeneratePasswordFromOptions();

            var buttonRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = AppTheme.Background,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttonRow.Controls.Add(btnGenerate);

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background };
            panel.Controls.Add(buttonRow);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = AppTheme.Background });
            panel.Controls.Add(optionsStack);
            panel.Controls.Add(lblOptions);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background });
            panel.Controls.Add(lengthRow);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background });
            panel.Controls.Add(passwordRow);
            panel.Controls.Add(spacer);
            panel.Controls.Add(lblSubtitle);
            panel.Controls.Add(lblHeading);
            return panel;
        }

        private Panel BuildPassphraseGeneratorContent()
        {
            var lblHeading = new Label
            {
                Text = "Passphrase Generator",
                Dock = DockStyle.Top,
                Height = 32,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary
            };
            var lblSubtitle = new Label
            {
                Text = "Create memorable and secure passphrases using a mix of random words.",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };

            var lblWords = new Label
            {
                Text = "Words",
                Dock = DockStyle.Left,
                Width = 56,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            nudPassphraseWords = new NumericUpDown
            {
                Minimum = PassphraseGenerator.MinWordCount,
                Maximum = PassphraseGenerator.MaxWordCount,
                Value = PassphraseGenerator.DefaultWordCount,
                Width = 64,
                Font = AppTheme.Base,
                Dock = DockStyle.Left
            };
            nudPassphraseWords.ValueChanged += (s, e) => RegeneratePassphraseFromOptions();
            var lblWordsHint = new Label
            {
                Text = "Recommended: 4–6 words",
                Dock = DockStyle.Left,
                Width = 160,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            var wordsRow = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = AppTheme.Background };
            wordsRow.Controls.Add(lblWordsHint);
            wordsRow.Controls.Add(nudPassphraseWords);
            wordsRow.Controls.Add(lblWords);

            var lblOptions = new Label
            {
                Text = "Options",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 9f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft
            };

            chkPassphraseCapitalize = CreatePassphraseOptionCheckBox("Capitalize each word", true);
            chkPassphraseNumbers = CreatePassphraseOptionCheckBox("Include numbers", true);
            chkPassphraseSpecial = CreatePassphraseOptionCheckBox("Include special characters", true);

            var optionsStack = new Panel { Dock = DockStyle.Top, Height = 32 * 3, BackColor = AppTheme.Background };
            optionsStack.Controls.Add(chkPassphraseSpecial);
            optionsStack.Controls.Add(chkPassphraseNumbers);
            optionsStack.Controls.Add(chkPassphraseCapitalize);

            var btnGenerate = DialogControls.CreatePrimaryButton("Generate Passphrase", 160, 36);
            btnGenerate.Dock = DockStyle.Left;
            btnGenerate.Click += (s, e) => RegeneratePassphraseFromOptions();
            var buttonRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = AppTheme.Background,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttonRow.Controls.Add(btnGenerate);

            txtGeneratedPassphrase = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 11f),
                ReadOnly = true
            };
            var btnCopy = DialogControls.CreateInlineActionButton("Copy", 56, 26);
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtGeneratedPassphrase.Text))
                {
                    clipboardGuard.CopyAndAutoClear(txtGeneratedPassphrase.Text);
                }
            };
            var passphraseRow = DialogControls.CreateBorderedFieldRow(txtGeneratedPassphrase, btnCopy);
            passphraseRow.Dock = DockStyle.Top;
            passphraseRow.Height = 40;

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background };
            // Dock=Top reverse order — visual top: heading, subtitle, output, words, options, generate
            panel.Controls.Add(buttonRow);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = AppTheme.Background });
            panel.Controls.Add(optionsStack);
            panel.Controls.Add(lblOptions);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background });
            panel.Controls.Add(wordsRow);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background });
            panel.Controls.Add(passphraseRow);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Background });
            panel.Controls.Add(lblSubtitle);
            panel.Controls.Add(lblHeading);
            return panel;
        }

        private void ShowGeneratorTab(bool passwordTab)
        {
            if (generatorPasswordPanel == null || generatorPassphrasePanel == null)
            {
                return;
            }

            generatorPasswordPanel.Visible = passwordTab;
            generatorPassphrasePanel.Visible = !passwordTab;
            tabUnderlinePassword.Visible = passwordTab;
            tabUnderlinePassphrase.Visible = !passwordTab;
            btnTabPassword.ForeColor = passwordTab ? AppTheme.Accent : AppTheme.TextSecondary;
            btnTabPassphrase.ForeColor = passwordTab ? AppTheme.TextSecondary : AppTheme.Accent;

            if (lblGeneratorRightHeading != null)
            {
                if (passwordTab)
                {
                    lblGeneratorRightHeading.Text = "Password Generator";
                    lblGeneratorRightHint.Text = "Adjust length and character types, then copy the password into a vault entry.";
                }
                else
                {
                    lblGeneratorRightHeading.Text = "Passphrase Generator";
                    lblGeneratorRightHint.Text = "Passphrases are longer, easier to remember, and more secure than traditional passwords. Ideal when you want strong security without a complex random string.";
                }
            }
        }

        private CheckBox CreatePassphraseOptionCheckBox(string text, bool isChecked)
        {
            var check = new CheckBox
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 28,
                Checked = isChecked,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Background
            };
            check.CheckedChanged += (s, e) => RegeneratePassphraseFromOptions();
            return check;
        }

        private void RegeneratePassphraseFromOptions()
        {
            if (txtGeneratedPassphrase == null || nudPassphraseWords == null)
            {
                return;
            }

            var options = new PassphraseGenerator.Options
            {
                WordCount = (int)nudPassphraseWords.Value,
                Capitalize = chkPassphraseCapitalize?.Checked ?? true,
                IncludeNumbers = chkPassphraseNumbers?.Checked ?? true,
                IncludeSpecialCharacters = chkPassphraseSpecial?.Checked ?? true
            };
            txtGeneratedPassphrase.Text = PassphraseGenerator.Generate(options);
        }

        private CheckBox CreateGeneratorOptionCheckBox(string text, bool isChecked)
        {
            var check = new CheckBox
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 28,
                Checked = isChecked,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Background
            };
            check.CheckedChanged += GeneratorOption_CheckedChanged;
            return check;
        }

        private void GeneratorOption_CheckedChanged(object sender, EventArgs e)
        {
            // Keep at least one character set enabled.
            if (sender is CheckBox changed && !changed.Checked && !AnyGeneratorCharsetEnabled())
            {
                changed.Checked = true;
                return;
            }

            RegeneratePasswordFromOptions();
        }

        private bool AnyGeneratorCharsetEnabled()
        {
            return (chkGeneratorUppercase?.Checked ?? false)
                || (chkGeneratorLowercase?.Checked ?? false)
                || (chkGeneratorDigits?.Checked ?? false)
                || (chkGeneratorSymbols?.Checked ?? false);
        }

        private void RegeneratePasswordFromOptions()
        {
            if (txtGeneratedPassword == null || nudGeneratorLength == null)
            {
                return;
            }

            var options = new PasswordGenerator.Options
            {
                Length = (int)nudGeneratorLength.Value,
                Uppercase = chkGeneratorUppercase?.Checked ?? true,
                Lowercase = chkGeneratorLowercase?.Checked ?? true,
                Digits = chkGeneratorDigits?.Checked ?? true,
                Symbols = chkGeneratorSymbols?.Checked ?? true
            };

            try
            {
                txtGeneratedPassword.Text = PasswordGenerator.Generate(options);
            }
            catch (InvalidOperationException)
            {
                // No character sets — should not happen when toggles enforce one enabled.
            }
        }

        /// Builds the Security page: Password Health, Security Issues, and Security Settings
        /// (master password + auto-lock), with a right panel that lists entries for a selected issue.
        private void BuildSecurityPage(out Panel securityMiddle, out Panel securityRight)
        {
            var lblSecurityHeading = new Label
            {
                Text = "Security",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            var lblSecuritySubtitle = new Label
            {
                Text = "Keep your vault safe and secure",
                Dock = DockStyle.Top,
                Height = 22,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };
            var spacerAfterSubtitle = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = AppTheme.Background };

            // --- Password Health summary card ---
            var healthCard = BuildPasswordHealthCard();

            // --- Security Issues rows ---
            var issuesCard = BuildSecurityIssuesCard();

            // --- Security Settings (master password + auto-lock) ---
            var settingsCard = BuildSecuritySettingsCard();

            var securityContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 12) };
            // Dock=Top stacks reverse of add order.
            securityContent.Controls.Add(settingsCard);
            securityContent.Controls.Add(issuesCard);
            securityContent.Controls.Add(healthCard);
            securityContent.Controls.Add(spacerAfterSubtitle);
            securityContent.Controls.Add(lblSecuritySubtitle);
            securityContent.Controls.Add(lblSecurityHeading);

            securityMiddle = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, Padding = new Padding(16, 14, 16, 0) };
            securityMiddle.Controls.Add(securityContent);

            // --- Right: issue detail / placeholder ---
            var backToOverview = new Label
            {
                Text = "←",
                Dock = DockStyle.Left,
                Width = 28,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 12f),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            backToOverview.Click += (s, e) => ShowSecurityIssueOverview();

            lblSecurityRightTitle = new Label
            {
                Text = "Password Health",
                Dock = DockStyle.Fill,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            var rightTitleRow = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = AppTheme.PanelBackground };
            rightTitleRow.Controls.Add(lblSecurityRightTitle);
            rightTitleRow.Controls.Add(backToOverview);

            lblSecurityRightHint = new Label
            {
                Text = "Select an issue to see affected entries.",
                Dock = DockStyle.Top,
                Height = 40,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };

            securityIssueEntriesHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = AppTheme.PanelBackground,
                Padding = new Padding(0, 8, 0, 0)
            };

            securityRight = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.PanelBackground, Padding = new Padding(16, 20, 16, 20) };
            securityRight.Controls.Add(securityIssueEntriesHost);
            securityRight.Controls.Add(lblSecurityRightHint);
            securityRight.Controls.Add(rightTitleRow);
        }

        private Panel BuildPasswordHealthCard()
        {
            var title = new Label
            {
                Text = "Password Health",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary
            };
            var subtitle = new Label
            {
                Text = "Check your vault for common security issues.",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };

            lblHealthTotal = new Label
            {
                Text = "0",
                Dock = DockStyle.Top,
                Height = 36,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 22f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomCenter
            };
            var totalCaption = new Label
            {
                Text = "vault entries",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.TopCenter
            };
            var totalCol = new Panel { Dock = DockStyle.Left, Width = 110, BackColor = AppTheme.Surface, Padding = new Padding(8, 8, 8, 8) };
            totalCol.Controls.Add(totalCaption);
            totalCol.Controls.Add(lblHealthTotal);

            lblHealthExposedCount = new Label { Text = "0", Dock = DockStyle.Right, Width = 36, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            lblHealthReusedCount = new Label { Text = "0", Dock = DockStyle.Right, Width = 36, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            lblHealthWeakCount = new Label { Text = "0", Dock = DockStyle.Right, Width = 36, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            lblHealthOldCount = new Label { Text = "0", Dock = DockStyle.Right, Width = 36, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleRight };

            var statsStack = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(12, 4, 8, 4) };
            // Dock=Top stacks reverse of add order — add bottom row first.
            statsStack.Controls.Add(CreateHealthStatRow("⚠", "Old passwords", lblHealthOldCount, System.Drawing.Color.DarkOrange));
            statsStack.Controls.Add(CreateHealthStatRow("⚠", "Weak passwords", lblHealthWeakCount, System.Drawing.Color.DarkOrange));
            statsStack.Controls.Add(CreateHealthStatRow("⚠", "Reused passwords", lblHealthReusedCount, System.Drawing.Color.DarkOrange));
            statsStack.Controls.Add(CreateHealthStatRow("✓", "No exposed passwords", lblHealthExposedCount, System.Drawing.Color.SeaGreen));

            var body = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = AppTheme.Surface };
            body.Controls.Add(statsStack);
            body.Controls.Add(totalCol);

            var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            const int cardHeight = 48 + 120 + 32;
            var inset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(16, 12, 16, 12) };
            inset.Controls.Add(body);
            inset.Controls.Add(header);

            var border = new Panel { Dock = DockStyle.Top, Height = cardHeight, BackColor = AppTheme.Border, Padding = new Padding(1) };
            border.Controls.Add(inset);
            return new Panel { Dock = DockStyle.Top, Height = cardHeight + 16, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 16), Controls = { border } };
        }

        private static Panel CreateHealthStatRow(string icon, string text, Label countLabel, System.Drawing.Color iconColor)
        {
            var iconLbl = new Label
            {
                Text = icon,
                Dock = DockStyle.Left,
                Width = 22,
                Font = AppTheme.Base,
                ForeColor = iconColor,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            var textLbl = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            var row = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = AppTheme.Surface };
            row.Controls.Add(textLbl);
            row.Controls.Add(countLabel);
            row.Controls.Add(iconLbl);
            return row;
        }

        private Panel BuildSecurityIssuesCard()
        {
            var sectionLabel = new Label
            {
                Text = "SECURITY ISSUES",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 8.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft
            };

            lblIssueReusedDesc = new Label();
            lblIssueWeakDesc = new Label();
            lblIssueOldDesc = new Label();
            lblIssueReusedCount = new Label();
            lblIssueWeakCount = new Label();
            lblIssueOldCount = new Label();

            var reusedRow = CreateSecurityIssueRow(
                "Reused Passwords",
                lblIssueReusedDesc,
                lblIssueReusedCount,
                PasswordHealth.IssueKind.Reused);
            var weakRow = CreateSecurityIssueRow(
                "Weak Passwords",
                lblIssueWeakDesc,
                lblIssueWeakCount,
                PasswordHealth.IssueKind.Weak);
            var oldRow = CreateSecurityIssueRow(
                "Old Passwords",
                lblIssueOldDesc,
                lblIssueOldCount,
                PasswordHealth.IssueKind.Old);

            var stack = new Panel { Dock = DockStyle.Top, Height = 22 + 56 * 3 + 2, BackColor = AppTheme.Surface };
            // reverse dock order
            stack.Controls.Add(oldRow);
            stack.Controls.Add(CreateInsetDivider());
            stack.Controls.Add(weakRow);
            stack.Controls.Add(CreateInsetDivider());
            stack.Controls.Add(reusedRow);
            stack.Controls.Add(sectionLabel);

            var inset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(12, 10, 12, 10) };
            inset.Controls.Add(stack);

            int cardHeight = 22 + 56 * 3 + 24;
            var border = new Panel { Dock = DockStyle.Top, Height = cardHeight, BackColor = AppTheme.Border, Padding = new Padding(1) };
            border.Controls.Add(inset);
            return new Panel { Dock = DockStyle.Top, Height = cardHeight + 16, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 16), Controls = { border } };
        }

        private Panel CreateSecurityIssueRow(string title, Label descLabel, Label countLabel, PasswordHealth.IssueKind kind)
        {
            var titleLbl = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 22,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 9.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary
            };
            descLabel.Dock = DockStyle.Top;
            descLabel.Height = 20;
            descLabel.Font = AppTheme.Base;
            descLabel.ForeColor = AppTheme.TextSecondary;
            descLabel.Text = PasswordHealth.DescriptionFor(kind, 0);

            var textStack = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            textStack.Controls.Add(descLabel);
            textStack.Controls.Add(titleLbl);

            countLabel.Dock = DockStyle.Right;
            countLabel.Width = 36;
            countLabel.Font = AppTheme.Base;
            countLabel.ForeColor = AppTheme.TextSecondary;
            countLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            countLabel.Text = "0";

            // Compact button: Dock=Right alone stretches to full row height, so host it.
            var viewBtn = DialogControls.CreateInlineActionButton("View Entries ›", 102, 26);
            viewBtn.Click += (s, e) => ShowSecurityIssue(kind);
            var viewHost = CreateRightAlignedControlHost(viewBtn, 108, 26);

            var row = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = AppTheme.Surface, Padding = new Padding(4, 6, 4, 6) };
            row.Controls.Add(textStack);
            row.Controls.Add(countLabel);
            row.Controls.Add(viewHost);
            return row;
        }

        /// Hosts a fixed-size control on the right of a row without stretching it to the
        /// full row height (plain Dock=Right would expand Height to match the parent).
        private static Panel CreateRightAlignedControlHost(Control control, int hostWidth, int controlHeight)
        {
            var host = new Panel
            {
                Dock = DockStyle.Right,
                Width = hostWidth,
                BackColor = AppTheme.Surface
            };
            control.Dock = DockStyle.None;
            control.Height = controlHeight;
            control.Width = Math.Min(control.Width, hostWidth);
            host.Controls.Add(control);
            host.Resize += (s, e) =>
            {
                control.Left = Math.Max(0, host.ClientSize.Width - control.Width);
                control.Top = Math.Max(0, (host.ClientSize.Height - control.Height) / 2);
            };
            // Initial position before first resize.
            control.Left = Math.Max(0, hostWidth - control.Width);
            control.Top = 0;
            return host;
        }

        private Panel BuildSecuritySettingsCard()
        {
            var sectionLabel = new Label
            {
                Text = "Security Settings",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary
            };
            var sectionSub = new Label
            {
                Text = "Manage your vault security settings.",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };

            // Master password row
            var mpTitle = new Label
            {
                Text = "Master Password",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 9.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary
            };
            var mpDesc = new Label
            {
                Text = "Change your master password.",
                Dock = DockStyle.Top,
                Height = 18,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };
            var mpText = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            mpText.Controls.Add(mpDesc);
            mpText.Controls.Add(mpTitle);
            var mpBtn = DialogControls.CreateInlineActionButton("Change Master Password ›", 168, 26);
            mpBtn.Click += ChangeMasterPassword_Click;
            var mpHost = CreateRightAlignedControlHost(mpBtn, 176, 26);
            var mpRow = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = AppTheme.Surface, Padding = new Padding(4, 6, 4, 6) };
            mpRow.Controls.Add(mpText);
            mpRow.Controls.Add(mpHost);

            // Auto-lock row
            var alTitle = new Label
            {
                Text = "Auto-Lock",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 9.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary
            };
            var alDesc = new Label
            {
                Text = "Automatically lock the vault after inactivity.",
                Dock = DockStyle.Top,
                Height = 18,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };
            var alText = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            alText.Controls.Add(alDesc);
            alText.Controls.Add(alTitle);

            var cmbAutoLock = new ComboBox
            {
                Width = 110,
                Height = 22,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = AppTheme.Base,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary
            };
            cmbAutoLock.Items.Add(new AutoLockOption(1, "1 minute"));
            cmbAutoLock.Items.Add(new AutoLockOption(2, "2 minutes"));
            cmbAutoLock.Items.Add(new AutoLockOption(5, "5 minutes"));
            cmbAutoLock.Items.Add(new AutoLockOption(10, "10 minutes"));
            cmbAutoLock.Items.Add(new AutoLockOption(15, "15 minutes"));
            cmbAutoLock.Items.Add(new AutoLockOption(30, "30 minutes"));
            cmbAutoLock.Items.Add(new AutoLockOption(0, "Never"));
            cmbAutoLock.DisplayMember = nameof(AutoLockOption.Label);
            cmbAutoLock.SelectedIndex = AutoLockMinutesToComboIndex(AppPreferences.AutoLockMinutes);
            cmbAutoLock.SelectedIndexChanged += (s, e) =>
            {
                if (cmbAutoLock.SelectedItem is AutoLockOption option)
                {
                    AppPreferences.AutoLockMinutes = option.Minutes;
                    lastActivityUtc = DateTime.UtcNow;
                }
            };
            cmbAutoLock.Height = 26;
            var alHost = CreateRightAlignedControlHost(cmbAutoLock, 118, 26);

            var alRow = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = AppTheme.Surface, Padding = new Padding(4, 6, 4, 6) };
            alRow.Controls.Add(alText);
            alRow.Controls.Add(alHost);

            var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };
            header.Controls.Add(sectionSub);
            header.Controls.Add(sectionLabel);

            var stack = new Panel { Dock = DockStyle.Top, Height = 48 + 52 + 1 + 52, BackColor = AppTheme.Surface };
            stack.Controls.Add(alRow);
            stack.Controls.Add(CreateInsetDivider());
            stack.Controls.Add(mpRow);
            stack.Controls.Add(header);

            int cardHeight = 48 + 52 + 1 + 52 + 24;
            var inset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(12, 10, 12, 10) };
            inset.Controls.Add(stack);
            var border = new Panel { Dock = DockStyle.Top, Height = cardHeight, BackColor = AppTheme.Border, Padding = new Padding(1) };
            border.Controls.Add(inset);
            return new Panel { Dock = DockStyle.Top, Height = cardHeight + 16, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 16), Controls = { border } };
        }

        private void RefreshSecurityHealth()
        {
            if (accounts == null || lblHealthTotal == null)
            {
                return;
            }

            var report = PasswordHealth.Analyze(accounts);
            lblHealthTotal.Text = report.TotalEntries.ToString();
            lblHealthExposedCount.Text = report.ExposedCount.ToString();
            lblHealthReusedCount.Text = report.Reused.Count.ToString();
            lblHealthWeakCount.Text = report.Weak.Count.ToString();
            lblHealthOldCount.Text = report.Old.Count.ToString();

            lblIssueReusedDesc.Text = PasswordHealth.DescriptionFor(PasswordHealth.IssueKind.Reused, report.Reused.Count);
            lblIssueWeakDesc.Text = PasswordHealth.DescriptionFor(PasswordHealth.IssueKind.Weak, report.Weak.Count);
            lblIssueOldDesc.Text = PasswordHealth.DescriptionFor(PasswordHealth.IssueKind.Old, report.Old.Count);
            lblIssueReusedCount.Text = report.Reused.Count.ToString();
            lblIssueWeakCount.Text = report.Weak.Count.ToString();
            lblIssueOldCount.Text = report.Old.Count.ToString();

            if (activeSecurityIssue is PasswordHealth.IssueKind kind)
            {
                ShowSecurityIssue(kind);
            }
        }

        private void ShowSecurityIssueOverview()
        {
            activeSecurityIssue = null;
            if (lblSecurityRightTitle != null)
            {
                lblSecurityRightTitle.Text = "Password Health";
                lblSecurityRightHint.Text = "Select an issue to see affected entries.";
            }

            securityIssueEntriesHost?.Controls.Clear();
        }

        private void ShowSecurityIssue(PasswordHealth.IssueKind kind)
        {
            if (accounts == null || securityIssueEntriesHost == null)
            {
                return;
            }

            activeSecurityIssue = kind;
            var report = PasswordHealth.Analyze(accounts);
            var entries = PasswordHealth.EntriesFor(report, kind);

            lblSecurityRightTitle.Text = PasswordHealth.TitleFor(kind);
            lblSecurityRightHint.Text = PasswordHealth.DescriptionFor(kind, entries.Count);

            securityIssueEntriesHost.Controls.Clear();
            // Dock=Top reverse order: add bottom-first so first entry appears at top.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                securityIssueEntriesHost.Controls.Add(CreateSecurityIssueEntryRow(entries[i]));
            }
        }

        private Panel CreateSecurityIssueEntryRow(Account account)
        {
            var badge = new ServiceBadgeControl
            {
                Width = 32,
                Height = 32,
                ServiceName = account.Service,
                WebsiteUrl = account.Url,
                Location = new System.Drawing.Point(0, 8)
            };
            var badgeHost = new Panel { Dock = DockStyle.Left, Width = 40, BackColor = AppTheme.PanelBackground };
            badgeHost.Controls.Add(badge);

            var name = new Label
            {
                Text = string.IsNullOrEmpty(account.Service) ? "(no service)" : account.Service,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 9.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary
            };
            var user = new Label
            {
                Text = account.Username ?? string.Empty,
                Dock = DockStyle.Top,
                Height = 18,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary
            };
            var text = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.PanelBackground };
            text.Controls.Add(user);
            text.Controls.Add(name);

            var chevron = new Label
            {
                Text = "›",
                Dock = DockStyle.Right,
                Width = 20,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 12f),
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            var row = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = AppTheme.PanelBackground, Padding = new Padding(0, 4, 0, 4), Cursor = Cursors.Hand };
            row.Controls.Add(text);
            row.Controls.Add(chevron);
            row.Controls.Add(badgeHost);

            void openInVault(object s, EventArgs e)
            {
                ShowAllItemsView();
                SelectNavRow(allItemsNavRow);

                // MultiSelect defaults to true, so selecting without clearing leaves the
                // first row selected and the details panel stuck on that entry.
                dgvAccounts.ClearSelection();
                foreach (DataGridViewRow gridRow in dgvAccounts.Rows)
                {
                    if (ReferenceEquals(gridRow.DataBoundItem, account))
                    {
                        gridRow.Selected = true;
                        dgvAccounts.CurrentCell = gridRow.Cells[0];
                        RefreshDetailsPanel();
                        break;
                    }
                }
            }

            row.Click += openInVault;
            name.Click += openInVault;
            user.Click += openInVault;
            chevron.Click += openInVault;
            return row;
        }

        /// Builds the Settings page's middle (heading + Vault card) and right (summary)
        /// panels, shown instead of the vault's middle/details panels while the Settings nav
        /// row is selected. Same overlapping-panel/toggle-Visible approach as the Security
        /// page - see BuildSecurityPage/ShowSettingsView/ShowVaultView.
        private void BuildSettingsPage(out Panel settingsMiddle, out Panel settingsRight)
        {
            // --- Middle: heading + Vault card ---
            var backArrow = new Label
            {
                Text = "←",
                Dock = DockStyle.Left,
                Width = 32,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 14f),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            backArrow.Click += (sender, e) => ShowVaultView();

            var lblSettingsHeading = new Label
            {
                Text = "Settings",
                Dock = DockStyle.Fill,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            var titleRow = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = AppTheme.Background };
            titleRow.Controls.Add(lblSettingsHeading);
            titleRow.Controls.Add(backArrow);

            var lblSettingsSubtitle = new Label
            {
                Text = "Manage your vault and application preferences",
                Dock = DockStyle.Top,
                Height = 24,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                Padding = new Padding(2, 0, 0, 0)
            };
            var spacerAfterSubtitle = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.Background };

            // Vault card: one header ("Vault") followed by two clickable rows - Backup Vault
            // and Restore Vault - each firing the exact same handler already used by the top
            // Settings menu. No new backup/restore logic here, just a second entry point
            // into the existing one.
            var lblVaultCardTitle = new Label { Text = "Vault", Dock = DockStyle.Top, Height = 22, Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10.5f, System.Drawing.FontStyle.Bold), ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.BottomLeft };
            var lblVaultCardDescription = new Label { Text = "Backup and restore your encrypted vault.", Dock = DockStyle.Top, Height = 20, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.TopLeft };
            // Height must accommodate top padding (16) + title (22) + description (20) so the header text is not clipped.
            var vaultCardHeader = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = AppTheme.Surface, Padding = new Padding(16, 16, 16, 0) };
            vaultCardHeader.Controls.Add(lblVaultCardDescription);
            vaultCardHeader.Controls.Add(lblVaultCardTitle);

            var backupRow = CreateSettingsActionRow("Backup Vault", "Create a backup of your encrypted vault.", BackupVault_Click);
            var restoreRow = CreateSettingsActionRow("Restore Vault", "Restore your vault from an existing backup.", RestoreVault_Click);

            // Inset dividers (left/right padding) so the hairlines don't run edge-to-edge of the card.
            var headerDivider = CreateInsetDivider();
            var rowDivider = CreateInsetDivider();

            const int rowHeight = 64;
            const int headerHeight = 64;
            const int dividerHeight = 1;
            // +2 for the 1px border padding on top and bottom of cardBorder so the last row is not clipped.
            const int cardHeight = headerHeight + dividerHeight + rowHeight + dividerHeight + rowHeight + 2;

            var cardInset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            // Dock=Top siblings stack in reverse of add order (last added ends up closest
            // to the top edge) - same quirk called out throughout this file.
            cardInset.Controls.Add(restoreRow);
            cardInset.Controls.Add(rowDivider);
            cardInset.Controls.Add(backupRow);
            cardInset.Controls.Add(headerDivider);
            cardInset.Controls.Add(vaultCardHeader);

            var cardBorder = new Panel { Dock = DockStyle.Top, Height = cardHeight, BackColor = AppTheme.Border, Padding = new Padding(1) };
            cardBorder.Controls.Add(cardInset);

            var cardWithMargin = new Panel { Dock = DockStyle.Top, Height = cardHeight + 16, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 16) };
            cardWithMargin.Controls.Add(cardBorder);

            // Appearance card: website icons toggle (defaults on; can be turned off to use letter badges only).
            var appearanceCard = BuildAppearanceSettingsCard();

            var settingsContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 12) };
            // Dock=Top stacks reverse of add order: appearance below vault visually.
            settingsContent.Controls.Add(appearanceCard);
            settingsContent.Controls.Add(cardWithMargin);
            settingsContent.Controls.Add(spacerAfterSubtitle);
            settingsContent.Controls.Add(lblSettingsSubtitle);
            settingsContent.Controls.Add(titleRow);

            settingsMiddle = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, Padding = new Padding(16, 14, 16, 0) };
            settingsMiddle.Controls.Add(settingsContent);

            // --- Right: summary panel ---
            var lblRightHeading = new Label { Text = "Settings", Dock = DockStyle.Top, Height = 32, Font = AppTheme.Heading, ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
            var lblRightSubtitle = new Label { Text = "Manage your vault and application preferences", Dock = DockStyle.Top, Height = 40, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.TopCenter };
            var rightDividerSpacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.PanelBackground };
            var rightDividerLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = AppTheme.Border };
            var rightDividerSpacerAfter = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = AppTheme.PanelBackground };

            // Only one checklist line for now - Appearance/About aren't implemented yet, so
            // they're not claimed here either.
            var checklistItem = CreateChecklistItem("Backup and restore your vault");

            settingsRight = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.PanelBackground, Padding = new Padding(16, 20, 16, 20) };
            settingsRight.Controls.Add(checklistItem);
            settingsRight.Controls.Add(rightDividerSpacerAfter);
            settingsRight.Controls.Add(rightDividerLine);
            settingsRight.Controls.Add(rightDividerSpacer);
            settingsRight.Controls.Add(lblRightSubtitle);
            settingsRight.Controls.Add(lblRightHeading);
        }

        /// Inset hairline divider for Settings cards: full-width container with left/right
        /// padding so the line matches the content indent and does not run edge-to-edge.
        private static Panel CreateInsetDivider()
        {
            var line = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Border };
            var container = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = AppTheme.Surface, Padding = new Padding(16, 0, 16, 0) };
            container.Controls.Add(line);
            return container;
        }

        /// Appearance settings card with the website-icons toggle.
        private Panel BuildAppearanceSettingsCard()
        {
            var lblTitle = new Label
            {
                Text = "Appearance",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft
            };
            var lblDescription = new Label
            {
                Text = "How entries look in the vault list and details panel.",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };
            var cardHeader = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = AppTheme.Surface, Padding = new Padding(16, 16, 16, 0) };
            cardHeader.Controls.Add(lblDescription);
            cardHeader.Controls.Add(lblTitle);

            var titleLabel = new Label
            {
                Text = "Website icons",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft
            };
            var descriptionLabel = new Label
            {
                Text = "Show each site's favicon instead of the coloured letter badge.",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };
            var textStack = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            textStack.Controls.Add(descriptionLabel);
            textStack.Controls.Add(titleLabel);

            var iconsToggle = new CheckBox
            {
                Checked = AppPreferences.ShowWebsiteIcons,
                Dock = DockStyle.Right,
                Width = 40,
                CheckAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = AppTheme.Surface
            };
            iconsToggle.CheckedChanged += (s, e) =>
            {
                AppPreferences.ShowWebsiteIcons = iconsToggle.Checked;
                if (!dgvAccounts.IsDisposed && dgvAccounts.IsHandleCreated)
                {
                    dgvAccounts.Invalidate();
                }
                serviceBadge?.Invalidate();
            };

            var toggleRow = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = AppTheme.Surface, Padding = new Padding(16, 10, 16, 10) };
            toggleRow.Controls.Add(textStack);
            toggleRow.Controls.Add(iconsToggle);

            var headerDivider = CreateInsetDivider();

            const int headerHeight = 64;
            const int rowHeight = 64;
            const int dividerHeight = 1;
            const int cardHeight = headerHeight + dividerHeight + rowHeight + 2;

            var cardInset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface };
            cardInset.Controls.Add(toggleRow);
            cardInset.Controls.Add(headerDivider);
            cardInset.Controls.Add(cardHeader);

            var cardBorder = new Panel { Dock = DockStyle.Top, Height = cardHeight, BackColor = AppTheme.Border, Padding = new Padding(1) };
            cardBorder.Controls.Add(cardInset);

            var cardWithMargin = new Panel { Dock = DockStyle.Top, Height = cardHeight + 16, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 16) };
            cardWithMargin.Controls.Add(cardBorder);
            return cardWithMargin;
        }

        /// Builds one clickable "title / description / ›" row for the Settings page's Vault
        /// card (Backup Vault, Restore Vault) - firing onClick, the same handler already
        /// used by the top Settings menu's equivalent item.
        private static Panel CreateSettingsActionRow(string title, string description, EventHandler onClick)
        {
            var titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 22, Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10f, System.Drawing.FontStyle.Bold), ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.BottomLeft, Cursor = Cursors.Hand };
            var descriptionLabel = new Label { Text = description, Dock = DockStyle.Top, Height = 20, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.TopLeft, Cursor = Cursors.Hand };
            var textStack = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Cursor = Cursors.Hand };
            textStack.Controls.Add(descriptionLabel);
            textStack.Controls.Add(titleLabel);

            var chevron = new Label { Text = "›", Dock = DockStyle.Right, Width = 24, Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 12f), ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };

            var row = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = AppTheme.Surface, Padding = new Padding(16, 10, 16, 10), Cursor = Cursors.Hand };
            row.Controls.Add(textStack);
            row.Controls.Add(chevron);

            // Every layer of the row is wired to the same handler, so a click anywhere on
            // it - not just directly on the title text - triggers the action.
            row.Click += onClick;
            textStack.Click += onClick;
            titleLabel.Click += onClick;
            descriptionLabel.Click += onClick;
            chevron.Click += onClick;

            // Same purple hover tint used by sidebar nav rows (CreateNavRow / CreateNavButton).
            EventHandler hoverOn = (s, e) =>
            {
                row.BackColor = AppTheme.AccentSubtle;
                textStack.BackColor = AppTheme.AccentSubtle;
            };
            EventHandler hoverOff = (s, e) =>
            {
                row.BackColor = AppTheme.Surface;
                textStack.BackColor = AppTheme.Surface;
            };
            row.MouseEnter += hoverOn;
            row.MouseLeave += hoverOff;
            textStack.MouseEnter += hoverOn;
            textStack.MouseLeave += hoverOff;
            titleLabel.MouseEnter += hoverOn;
            titleLabel.MouseLeave += hoverOff;
            descriptionLabel.MouseEnter += hoverOn;
            descriptionLabel.MouseLeave += hoverOff;
            chevron.MouseEnter += hoverOn;
            chevron.MouseLeave += hoverOff;

            return row;
        }

        /// Builds one "✓ text" row for a page's right-hand summary checklist (Security,
        /// Settings) - only ever used for things that are actually implemented, per the
        /// same reasoning as where it's called from.
        private static Panel CreateChecklistItem(string text)
        {
            var check = new Label { Text = "✓", Dock = DockStyle.Left, Width = 24, Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10f, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.SeaGreen, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
            var label = new Label { Text = text, Dock = DockStyle.Fill, Font = AppTheme.Base, ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            var itemRow = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = AppTheme.PanelBackground };
            itemRow.Controls.Add(label);
            itemRow.Controls.Add(check);
            return itemRow;
        }

        /// Builds one bordered "card" for the Security page: a title/description header
        /// and an action row underneath (e.g. the Change Master Password row).
        private static Panel CreateSecurityCard(string title, string description, Control actionRow)
        {
            var titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 22, Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 10.5f, System.Drawing.FontStyle.Bold), ForeColor = AppTheme.TextPrimary, TextAlign = System.Drawing.ContentAlignment.BottomLeft };
            var descriptionLabel = new Label { Text = description, Dock = DockStyle.Top, Height = 20, Font = AppTheme.Base, ForeColor = AppTheme.TextSecondary, TextAlign = System.Drawing.ContentAlignment.TopLeft };
            var headerRow = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };
            headerRow.Controls.Add(descriptionLabel);
            headerRow.Controls.Add(titleLabel);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Surface };
            actionRow.Dock = DockStyle.Top;

            const int cardHeight = 126; // header(48) + spacer(12) + actionRow(34) + inset padding(16+16)
            var cardInset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(16) };
            cardInset.Controls.Add(actionRow);
            cardInset.Controls.Add(spacer);
            cardInset.Controls.Add(headerRow);

            var cardBorder = new Panel { Dock = DockStyle.Top, Height = cardHeight, BackColor = AppTheme.Border, Padding = new Padding(1) };
            cardBorder.Controls.Add(cardInset);

            var cardWithMargin = new Panel { Dock = DockStyle.Top, Height = cardHeight + 16, BackColor = AppTheme.Background, Padding = new Padding(0, 0, 0, 16) };
            cardWithMargin.Controls.Add(cardBorder);
            return cardWithMargin;
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
            serviceBadge.WebsiteUrl = selectedAccount.Url;
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
            gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly);
            UpdateEntryCountLabel();
            RefreshSecurityHealth();
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
            gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly);
        }

        private void CmbSort_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (gridPresenter == null || cmbSort.SelectedIndex < 0)
            {
                return;
            }

            AccountSortMode mode = ComboIndexToSortMode(cmbSort.SelectedIndex);
            AppPreferences.SortMode = mode;
            gridPresenter.SortMode = mode;
        }

        private static AccountSortMode ComboIndexToSortMode(int index) => index switch
        {
            1 => AccountSortMode.NameDescending,
            2 => AccountSortMode.UsernameAscending,
            3 => AccountSortMode.RecentlyModified,
            4 => AccountSortMode.RecentlyCreated,
            5 => AccountSortMode.FavouritesFirst,
            _ => AccountSortMode.NameAscending
        };

        private static int SortModeToComboIndex(AccountSortMode mode) => mode switch
        {
            AccountSortMode.NameDescending => 1,
            AccountSortMode.UsernameAscending => 2,
            AccountSortMode.RecentlyModified => 3,
            AccountSortMode.RecentlyCreated => 4,
            AccountSortMode.FavouritesFirst => 5,
            _ => 0
        };

        /// Persists a favourite toggle (BindingList does not raise ListChanged for in-place
        /// property changes) and refreshes the grid so a favourites-only view drops the
        /// entry when it is unfavourited.
        private void OnFavoriteToggled(Account account)
        {
            int index = accounts.IndexOf(account);
            if (index >= 0)
            {
                accounts.ResetItem(index); // Triggers Accounts_ListChanged -> save + refresh
            }
            else
            {
                // Fallback if the account is somehow not in the list: still refresh the view.
                gridPresenter.Refresh(accounts, txtSearch.Text, showFavoritesOnly);
            }
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

            copyUsernameMenuItem.Enabled = hasSelection;
            copyPasswordMenuItem.Enabled = hasSelection;
            autoTypeMenuItem.Enabled = hasSelection;
            editEntryMenuItem.Enabled = hasSelection;
            deleteEntryMenuItem.Enabled = hasSelection;
        }

        private void CopyUsername_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            clipboardGuard.CopyAndAutoClear(selectedAccount.Username);
        }

        private void CopyPassword_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            clipboardGuard.CopyAndAutoClear(selectedAccount.Password);
        }

        /// <summary>
        /// KeePass-style auto-type: types Username, Tab, Password into whichever window
        /// currently has focus (e.g. a browser login form). Minimizes this form briefly
        /// so the previously active window is restored, then waits a short moment for
        /// focus to settle before sending keystrokes.
        /// </summary>
        private void AutoType_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            PerformAutoType(selectedAccount);
        }

        private void PerformAutoType(Account account)
        {
            // Capture credentials on the UI thread before any delay/background work.
            string username = account.Username ?? string.Empty;
            string password = account.Password ?? string.Empty;

            // Minimize so the window that was behind us (browser, etc.) becomes active.
            // Leave minimized so focus stays on the target window after typing finishes;
            // the user can restore the vault from the taskbar when ready.
            this.WindowState = FormWindowState.Minimized;

            // Type on a background thread so the UI stays responsive during the delay
            // and keystroke simulation. SendInput itself is fine from a non-UI thread.
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                // Give Windows a moment to switch focus to the previous foreground window.
                AutoTypeHelper.DelayBeforeTyping(450);

                // Default sequence matches KeePass's common login form pattern
                // (username → Tab → password). Enter is not sent so the user can
                // review the filled fields before submitting.
                AutoTypeHelper.TypeCredentials(username, password, pressEnter: false);
            });
        }

        /// Ctrl+X copies the selected entry's username; Ctrl+C copies its password;
        /// Ctrl+V performs auto-type. Only when the vault grid has focus.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (dgvAccounts.Focused && dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount)
            {
                if (keyData == (Keys.Control | Keys.X))
                {
                    clipboardGuard.CopyAndAutoClear(selectedAccount.Username);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.C))
                {
                    clipboardGuard.CopyAndAutoClear(selectedAccount.Password);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.V))
                {
                    PerformAutoType(selectedAccount);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
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

            int minutes = AppPreferences.AutoLockMinutes;
            if (minutes <= 0)
            {
                return; // Auto-lock disabled ("Never")
            }

            if (DateTime.UtcNow - lastActivityUtc >= TimeSpan.FromMinutes(minutes))
            {
                Lock();
            }
        }

        private static int AutoLockMinutesToComboIndex(int minutes) => minutes switch
        {
            1 => 0,
            2 => 1,
            5 => 2,
            10 => 3,
            15 => 4,
            30 => 5,
            0 => 6,
            _ => 2 // Unknown saved value → show 5 minutes (historical default)
        };

        private sealed class AutoLockOption
        {
            public int Minutes { get; }
            public string Label { get; }

            public AutoLockOption(int minutes, string label)
            {
                Minutes = minutes;
                Label = label;
            }

            public override string ToString() => Label;
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