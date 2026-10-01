using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace PasswordManager
{
    public class AddEntryForm : Form
    {
        private const int GenerationSettingsCollapsedHeight = 34;
        private const int GenerationSettingsExpandedHeight = 290;

        private TextBox txtService;
        private ComboBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtRepeatPassword; // New repeat password field
        private TextBox txtUrl; // New URL field
        private TextBox txtNotes;
        private Button btnGeneratePassword;
        private Button btnRegeneratePassword;
        private Button btnGenerationSettings;
        private Button btnTogglePasswordVisibility;
        private Panel generationSettingsPanel;
        private Button btnManageUsernames; // Opens the curated username suggestions list
        private Button btnSave;
        private Button btnCancel;
        private TrackBar sliderPasswordLength; // Slider for password length
        private Label lblCurrentLength; // Label to show current length
        private CheckBox chkUppercase;
        private CheckBox chkLowercase;
        private CheckBox chkNumbers;
        private CheckBox chkSymbols;
        private Panel passwordSettingsContent;
        private Panel passphraseSettingsContent;
        private NumericUpDown nudPassphraseWords;
        private CheckBox chkPassphraseCapitalize;
        private CheckBox chkPassphraseNumbers;
        private CheckBox chkPassphraseSpecial;
        private Panel pnlStrengthBarContainer;
        private Panel pnlStrengthBarFill;
        private Label lblStrengthText;
        private Panel contentScroll; // Scrollable body; needs its layout suspended too when toggling the length slider

        public string Service { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string Notes { get; private set; }
        public string Url { get; private set; } // URL property

        private bool isPasswordVisible = false; // Track visibility state
        private bool isGeneratedPassword = false; // Track if using a generated password
        /// Last type chosen from the Generate menu (used by Regenerate later).
        private bool lastGeneratedWasPassphrase = false;

        public AddEntryForm(Account existingAccount = null)
        {
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;

            // Narrower and shorter, closely matching the tightened content's actual height
            // rather than leaving a large buffer below Notes - the content area is still
            // scrollable as a safety net in case any platform/DPI combination needs a
            // little more room than expected, rather than fields ever getting clipped.
            this.Size = new System.Drawing.Size(460, 640);

            const int inputHeight = 30;
            const int iconSize = 30;

            txtService = new TextBox { PlaceholderText = "Service Name", BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtUsername = new ComboBox { Font = AppTheme.Base, DropDownStyle = ComboBoxStyle.DropDown };
            RefreshUsernameSuggestions(); // Builds suggestions from UsernameSuggestionsStore

            btnManageUsernames = CreateIconButton("👤", iconSize); // Manage suggested usernames
            btnManageUsernames.Click += BtnManageUsernames_Click;

            // Borderless: the bordered field row supplies the outline so the eye sits inside the same box.
            txtPassword = new TextBox { PlaceholderText = "Password", BorderStyle = BorderStyle.None, PasswordChar = '*', Font = AppTheme.Base, BackColor = AppTheme.Surface };
            txtRepeatPassword = new TextBox { PlaceholderText = "Repeat Password", BorderStyle = BorderStyle.FixedSingle, PasswordChar = '*', Font = AppTheme.Base, Enabled = false }; // Repeat password field disabled by default
            txtUrl = new TextBox { PlaceholderText = "URL", BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtNotes = new TextBox { PlaceholderText = "Notes", BorderStyle = BorderStyle.FixedSingle, Multiline = true, Font = AppTheme.Base };

            // Password length slider (lives inside the generation settings panel).
            int savedLength = Math.Clamp(AppPreferences.PasswordLength, PasswordGenerator.MinLength, PasswordGenerator.MaxLength);
            lblCurrentLength = new Label
            {
                Text = savedLength.ToString(),
                AutoSize = false,
                Width = 30,
                Height = 24,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            sliderPasswordLength = new TrackBar
            {
                Minimum = PasswordGenerator.MinLength,
                Maximum = PasswordGenerator.MaxLength,
                Value = savedLength,
                Height = 45,
                TickFrequency = 4,
                LargeChange = 1,
                SmallChange = 1,
                Width = 280
            };
            sliderPasswordLength.Scroll += (s, e) =>
            {
                lblCurrentLength.Text = sliderPasswordLength.Value.ToString();
                if (!lastGeneratedWasPassphrase)
                {
                    RegeneratePassword();
                }
            };

            // Generate sits beside the password field (primary button + dropdown menu).
            btnGeneratePassword = DialogControls.CreatePrimaryButton("Generate ▾", 110, iconSize);
            btnGeneratePassword.Dock = DockStyle.None;

            // Header control for the collapsible Generation Options container.
            btnGenerationSettings = new Button
            {
                Text = "▸  Generation Options (optional)",
                FlatStyle = FlatStyle.Flat,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Height = 32,
                Dock = DockStyle.Top,
                Cursor = Cursors.Hand,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Surface,
                Padding = new Padding(8, 0, 0, 0)
            };
            btnGenerationSettings.FlatAppearance.BorderSize = 0;
            btnGenerationSettings.FlatAppearance.MouseOverBackColor = AppTheme.AccentSubtle;
            btnGenerationSettings.Click += (s, e) => ToggleGenerationSettings();

            btnTogglePasswordVisibility = CreateIconButton("👁️", iconSize); // Eye icon

            var generateMenu = new ContextMenuStrip();
            generateMenu.Font = AppTheme.Base;
            generateMenu.Items.Add("Password", null, (s, e) => ApplyGeneratedPassword());
            generateMenu.Items.Add("Passphrase", null, (s, e) => ApplyGeneratedPassphrase());
            btnGeneratePassword.Click += (s, e) =>
            {
                generateMenu.Show(btnGeneratePassword, new System.Drawing.Point(0, btnGeneratePassword.Height));
            };

            // Save/Cancel as proper labeled buttons instead of emoji-only icons, matching
            // the reference apps' dialog buttons. Uses the shared primary/secondary button
            // helpers (DialogControls) so later dialogs reuse this exact same look.
            // Primary action is "Add" for new entries; switched to "Save" when editing.
            btnSave = DialogControls.CreatePrimaryButton("Add", 96, 32);
            btnCancel = DialogControls.CreateSecondaryButton("Cancel", 96, 32);

            var generateTip = new ToolTip();
            generateTip.SetToolTip(btnGeneratePassword, "Generate password or passphrase");

            // Password strength meter: a bordered bar that fills proportionally and changes
            // color, paired with a text label since color alone isn't accessible to everyone.
            pnlStrengthBarContainer = new Panel { Width = 200, Height = 14, Margin = new Padding(0, 4, 0, 0), BorderStyle = BorderStyle.FixedSingle };
            pnlStrengthBarFill = new Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Height = pnlStrengthBarContainer.ClientSize.Height,
                Width = 0,
                BackColor = AppTheme.TextSecondary
            };
            pnlStrengthBarContainer.Controls.Add(pnlStrengthBarFill);
            lblStrengthText = new Label { AutoSize = true, Font = AppTheme.Base, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Margin = new Padding(8, 4, 0, 0) };

            // Add event handlers
            btnTogglePasswordVisibility.Click += BtnTogglePasswordVisibility_Click; // Do not clear repeat password
            btnSave.Click += BtnSave_Click; // Clear repeat password
            btnCancel.Click += (sender, e) => this.Close(); // Clear repeat password

            // Adding manual entry detection
            txtPassword.TextChanged += TxtPassword_TextChanged; // Update strength when password changes
            txtRepeatPassword.TextChanged += TxtRepeatPassword_TextChanged; // Check match on repeat password text change

            // Username row: field fills remaining width, manage-usernames button to its right.
            var usernameRow = new Panel { BackColor = AppTheme.Background };
            usernameRow.Controls.Add(txtUsername);
            txtUsername.Dock = DockStyle.Fill;
            usernameRow.Controls.Add(btnManageUsernames);

            // Password row: [ bordered field + eye ] [ Generate ▾ ]
            btnTogglePasswordVisibility.BackColor = AppTheme.Surface;
            var passwordVisibilityTip = new ToolTip();
            passwordVisibilityTip.SetToolTip(btnTogglePasswordVisibility, "Show password");
            btnTogglePasswordVisibility.Tag = passwordVisibilityTip;
            var passwordFieldBox = DialogControls.CreateBorderedFieldRow(txtPassword, btnTogglePasswordVisibility);
            passwordFieldBox.Dock = DockStyle.Fill;

            btnGeneratePassword.Margin = new Padding(8, 0, 0, 0);
            var passwordRow = new Panel { BackColor = AppTheme.Background };
            passwordRow.Controls.Add(passwordFieldBox);
            // Right-dock Generate so the field fills remaining width.
            btnGeneratePassword.Dock = DockStyle.Right;
            passwordRow.Controls.Add(btnGeneratePassword);

            // --- Password settings content (length + character types) — seeded from saved prefs ---
            chkUppercase = CreatePasswordOptionCheckBox("Uppercase (A–Z)", AppPreferences.PasswordUppercase);
            chkLowercase = CreatePasswordOptionCheckBox("Lowercase (a–z)", AppPreferences.PasswordLowercase);
            chkNumbers = CreatePasswordOptionCheckBox("Numbers (0–9)", AppPreferences.PasswordDigits);
            chkSymbols = CreatePasswordOptionCheckBox("Symbols (!@#…)", AppPreferences.PasswordSymbols);

            var optionsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = AppTheme.Surface,
                Padding = new Padding(0, 2, 0, 2)
            };
            optionsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            optionsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            optionsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            optionsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            optionsGrid.Controls.Add(chkUppercase, 0, 0);
            optionsGrid.Controls.Add(chkNumbers, 1, 0);
            optionsGrid.Controls.Add(chkLowercase, 0, 1);
            optionsGrid.Controls.Add(chkSymbols, 1, 1);

            sliderPasswordLength.Width = 220;
            sliderPasswordLength.Height = 36;
            sliderPasswordLength.BackColor = AppTheme.Surface;
            var lengthRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 4),
                Margin = new Padding(0),
                BackColor = AppTheme.Surface
            };
            lengthRow.Controls.Add(new Label
            {
                Text = "Length",
                AutoSize = false,
                Width = 52,
                Height = 28,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            });
            lengthRow.Controls.Add(sliderPasswordLength);
            lengthRow.Controls.Add(lblCurrentLength);

            var lblCharacterTypes = new Label
            {
                Text = "Character Types",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 8.5f, System.Drawing.FontStyle.Bold),
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                BackColor = AppTheme.Surface
            };

            passwordSettingsContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Surface,
                Padding = new Padding(0, 4, 0, 0)
            };
            passwordSettingsContent.Controls.Add(optionsGrid);
            passwordSettingsContent.Controls.Add(lblCharacterTypes);
            passwordSettingsContent.Controls.Add(lengthRow);

            // --- Passphrase settings content (words + options) ---
            int savedWordCount = Math.Clamp(
                AppPreferences.PassphraseWordCount,
                PassphraseGenerator.MinWordCount,
                PassphraseGenerator.MaxWordCount);
            nudPassphraseWords = new NumericUpDown
            {
                Minimum = PassphraseGenerator.MinWordCount,
                Maximum = PassphraseGenerator.MaxWordCount,
                Value = savedWordCount,
                Width = 64,
                Font = AppTheme.Base
            };
            nudPassphraseWords.ValueChanged += (s, e) =>
            {
                if (lastGeneratedWasPassphrase && isGeneratedPassword)
                {
                    ApplyGeneratedPassphrase();
                }
            };

            var wordsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 4),
                Margin = new Padding(0),
                BackColor = AppTheme.Surface
            };
            wordsRow.Controls.Add(new Label
            {
                Text = "Words",
                AutoSize = false,
                Width = 52,
                Height = 28,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            });
            wordsRow.Controls.Add(nudPassphraseWords);

            chkPassphraseCapitalize = CreatePassphraseOptionCheckBox("Capitalize each word", AppPreferences.PassphraseCapitalize);
            chkPassphraseNumbers = CreatePassphraseOptionCheckBox("Include numbers", AppPreferences.PassphraseNumbers);
            chkPassphraseSpecial = CreatePassphraseOptionCheckBox("Use hyphens", AppPreferences.PassphraseSpecial);

            var passphraseOptionsStack = new Panel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = AppTheme.Surface
            };
            chkPassphraseCapitalize.Dock = DockStyle.Top;
            chkPassphraseNumbers.Dock = DockStyle.Top;
            chkPassphraseSpecial.Dock = DockStyle.Top;
            chkPassphraseCapitalize.Height = 28;
            chkPassphraseNumbers.Height = 28;
            chkPassphraseSpecial.Height = 28;
            // Dock=Top reverse add order
            passphraseOptionsStack.Controls.Add(chkPassphraseSpecial);
            passphraseOptionsStack.Controls.Add(chkPassphraseNumbers);
            passphraseOptionsStack.Controls.Add(chkPassphraseCapitalize);

            passphraseSettingsContent = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = AppTheme.Surface,
                Padding = new Padding(0, 4, 0, 0)
            };
            passphraseSettingsContent.Controls.Add(passphraseOptionsStack);
            passphraseSettingsContent.Controls.Add(wordsRow);

            // Mode switch: Password | Passphrase (inside the options container).
            var btnModePassword = DialogControls.CreateSecondaryButton("Password", 100, 28);
            var btnModePassphrase = DialogControls.CreateSecondaryButton("Passphrase", 100, 28);
            btnModePassword.Margin = new Padding(0, 0, 6, 0);
            btnModePassphrase.Margin = new Padding(0, 0, 0, 0);
            void SyncModeButtons()
            {
                btnModePassword.BackColor = !lastGeneratedWasPassphrase ? AppTheme.AccentSubtle : AppTheme.PanelBackground;
                btnModePassphrase.BackColor = lastGeneratedWasPassphrase ? AppTheme.AccentSubtle : AppTheme.PanelBackground;
            }
            btnModePassword.Click += (s, e) =>
            {
                lastGeneratedWasPassphrase = false;
                UpdateGenerationSettingsMode();
                SyncModeButtons();
            };
            btnModePassphrase.Click += (s, e) =>
            {
                lastGeneratedWasPassphrase = true;
                UpdateGenerationSettingsMode();
                SyncModeButtons();
            };
            SyncModeButtons();

            var modeRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 4, 0, 8),
                BackColor = AppTheme.Surface
            };
            modeRow.Controls.Add(btnModePassword);
            modeRow.Controls.Add(btnModePassphrase);

            // Footer: Save (persist options) + Regenerate — same height, right-aligned pair.
            const int footerButtonHeight = 30;
            btnRegeneratePassword = DialogControls.CreateSecondaryButton("Regenerate", 100, footerButtonHeight);
            btnRegeneratePassword.Click += (s, e) =>
            {
                if (lastGeneratedWasPassphrase)
                {
                    ApplyGeneratedPassphrase();
                }
                else
                {
                    ApplyGeneratedPassword();
                }
            };

            var btnSaveGenerationSettings = DialogControls.CreatePrimaryButton("Save", 80, footerButtonHeight);
            btnSaveGenerationSettings.Click += (s, e) => SaveGenerationPreferences();
            var saveSettingsTip = new ToolTip();
            saveSettingsTip.SetToolTip(btnSaveGenerationSettings, "Save generation options for next time");

            var footerRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = footerButtonHeight + 12,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = AppTheme.Surface
            };
            // Right-dock order: last added sits outermost right → add Save last.
            btnRegeneratePassword.Dock = DockStyle.Right;
            btnSaveGenerationSettings.Dock = DockStyle.Right;
            var footerGap = new Panel { Dock = DockStyle.Right, Width = 8, BackColor = AppTheme.Surface };
            footerRow.Controls.Add(btnRegeneratePassword);
            footerRow.Controls.Add(footerGap);
            footerRow.Controls.Add(btnSaveGenerationSettings);

            var settingsBodyHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Surface
            };
            settingsBodyHost.Controls.Add(passwordSettingsContent);
            settingsBodyHost.Controls.Add(passphraseSettingsContent);

            var generationBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 10),
                BackColor = AppTheme.Surface,
                Visible = false,
                AutoScroll = false
            };
            // Dock order: mode (top), settings (fill), footer (bottom)
            generationBody.Controls.Add(settingsBodyHost);
            generationBody.Controls.Add(footerRow);
            generationBody.Controls.Add(modeRow);

            // Bordered collapsible container — collapsed shows header only.
            generationSettingsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = GenerationSettingsCollapsedHeight,
                Padding = new Padding(1),
                BackColor = AppTheme.Border
            };
            var generationInset = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Surface
            };
            generationInset.Controls.Add(generationBody);
            generationInset.Controls.Add(btnGenerationSettings);
            generationSettingsPanel.Controls.Add(generationInset);

            var strengthPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 6, 0, 8), Margin = new Padding(0), BackColor = AppTheme.Background };
            strengthPanel.Controls.Add(pnlStrengthBarContainer);
            strengthPanel.Controls.Add(lblStrengthText);

            // Each real input gets its own full-width row with a caption above it (matching
            // the reference apps), replacing the old half-width Service+Username and
            // RepeatPassword+URL pairs - those were only paired up to fit the form's fixed
            // width, not because the fields are related.
            var serviceGroup = CreateFieldGroup("Service Name", txtService, inputHeight);
            var usernameGroup = CreateFieldGroup("Username", usernameRow, inputHeight);
            var passwordGroup = CreateFieldGroup("Password", passwordRow, inputHeight);
            var repeatGroup = CreateFieldGroup("Repeat Password", txtRepeatPassword, inputHeight);
            var urlGroup = CreateFieldGroup("URL", txtUrl, inputHeight);
            var notesGroup = CreateFieldGroup("Notes", txtNotes, 64);

            // Explicit tab order: Service → Username → Password → Repeat → URL → Notes.
            // Without this, TabIndex follows Controls.Add order (notes added first), so
            // focus opens on Notes and Tab never walks the fields top-to-bottom.
            serviceGroup.TabIndex = 0;
            usernameGroup.TabIndex = 1;
            passwordGroup.TabIndex = 2;
            strengthPanel.TabIndex = 3;
            generationSettingsPanel.TabIndex = 4;
            repeatGroup.TabIndex = 5;
            urlGroup.TabIndex = 6;
            notesGroup.TabIndex = 7;
            txtService.TabIndex = 0;
            txtUsername.TabIndex = 0;
            txtPassword.TabIndex = 0;
            txtRepeatPassword.TabIndex = 0;
            txtUrl.TabIndex = 0;
            txtNotes.TabIndex = 0;
            // Icon/action buttons sit in the same rows as the fields - skip them on Tab
            // so Tab moves field-to-field instead of stopping on every toolbar icon.
            btnManageUsernames.TabStop = false;
            btnGeneratePassword.TabStop = false;
            btnRegeneratePassword.TabStop = false;
            btnGenerationSettings.TabStop = false;
            btnTogglePasswordVisibility.TabStop = false;
            sliderPasswordLength.TabStop = false;

            var lblFormTitle = new Label
            {
                Text = "Add Entry",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft,
                Padding = new Padding(20, 0, 0, 6)
            };

            // Scrollable body so a longer-than-expected layout (different DPI/font metrics)
            // grows a scrollbar instead of clipping a field - the rest of the form isn't
            // resizable (FixedDialog), so this is the one safety net for that.
            contentScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 2, 20, 2), BackColor = AppTheme.Background };

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(20, 8, 20, 12), BackColor = AppTheme.Background };
            // Both buttons need matching top/bottom margin or they render misaligned
            // vertically within the row - only the left margin should differ, to create
            // the gap between them.
            btnCancel.Margin = new Padding(0, 3, 0, 3);
            btnSave.Margin = new Padding(10, 3, 0, 3);
            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnSave);

            // Add controls to the scrollable body in the correct order. Same-Dock-style
            // controls stack in the REVERSE of the order added (last added ends up closest
            // to the top edge) - confirmed the hard way in MainForm a few sessions back.
            // Dock=Top reverse: last added is top. Order: service → … → password → strength → options → repeat…
            contentScroll.Controls.Add(notesGroup);
            contentScroll.Controls.Add(urlGroup);
            contentScroll.Controls.Add(repeatGroup);
            contentScroll.Controls.Add(generationSettingsPanel);
            contentScroll.Controls.Add(strengthPanel);
            contentScroll.Controls.Add(passwordGroup);
            contentScroll.Controls.Add(usernameGroup);
            contentScroll.Controls.Add(serviceGroup);

            Controls.Add(contentScroll);
            Controls.Add(actionPanel);
            Controls.Add(lblFormTitle);

            Text = "Add Entry";

            // Shown is more reliable than Load for initial focus (Load can be overridden by
            // the default first-TabIndex control after the form becomes visible).
            this.Shown += AddEntryForm_Shown;

            // If editing an existing account, pre-fill the form and switch to edit mode.
            // Setting txtPassword.Text here also triggers TxtPassword_TextChanged, which
            // enables the repeat-password field; we then pre-fill that too so saving without
            // changing the password works immediately, same as if the user had retyped it.
            if (existingAccount != null)
            {
                Text = "Edit Entry";
                lblFormTitle.Text = "Edit Entry";
                btnSave.Text = "Save";
                txtService.Text = existingAccount.Service;
                txtUsername.Text = existingAccount.Username;
                txtUrl.Text = existingAccount.Url;
                txtNotes.Text = existingAccount.Notes;
                txtPassword.Text = existingAccount.Password;
                txtRepeatPassword.Text = existingAccount.Password;
            }
        }

        private void AddEntryForm_Shown(object sender, EventArgs e)
        {
            // Cursor ready in Service Name so the user can start typing immediately.
            ActiveControl = txtService;
            txtService.Focus();
        }

        private void ToggleGenerationSettings()
        {
            if (generationSettingsPanel == null || btnGenerationSettings == null)
            {
                return;
            }

            // Body is the Fill panel under the header inside the bordered container.
            Panel body = null;
            if (btnGenerationSettings.Parent != null)
            {
                foreach (Control child in btnGenerationSettings.Parent.Controls)
                {
                    if (child != btnGenerationSettings && child is Panel p)
                    {
                        body = p;
                        break;
                    }
                }
            }

            if (body == null)
            {
                return;
            }

            bool willShow = !body.Visible;

            SuspendLayout();
            contentScroll.SuspendLayout();
            contentScroll.AutoScroll = false;

            int expandDelta = GenerationSettingsExpandedHeight - GenerationSettingsCollapsedHeight;

            if (willShow)
            {
                generationSettingsPanel.Height = GenerationSettingsExpandedHeight;
                Height += expandDelta;
                body.Visible = true;
                btnGenerationSettings.Text = "▾  Generation Options (optional)";
                UpdateGenerationSettingsMode();
            }
            else
            {
                body.Visible = false;
                generationSettingsPanel.Height = GenerationSettingsCollapsedHeight;
                Height -= expandDelta;
                btnGenerationSettings.Text = "▸  Generation Options (optional)";
            }

            contentScroll.ResumeLayout(true);
            ResumeLayout(true);
            contentScroll.AutoScroll = true;
        }

        /// Shows password or passphrase options based on the last Generate menu choice.
        private void UpdateGenerationSettingsMode()
        {
            if (passwordSettingsContent == null || passphraseSettingsContent == null)
            {
                return;
            }

            passwordSettingsContent.Visible = !lastGeneratedWasPassphrase;
            passphraseSettingsContent.Visible = lastGeneratedWasPassphrase;
        }

        /// Writes current panel options to AppPreferences so future Generate uses them.
        private void SaveGenerationPreferences()
        {
            AppPreferences.SaveGenerationOptions(
                length: sliderPasswordLength?.Value ?? AppPreferences.PasswordLength,
                uppercase: chkUppercase?.Checked ?? true,
                lowercase: chkLowercase?.Checked ?? true,
                digits: chkNumbers?.Checked ?? true,
                symbols: chkSymbols?.Checked ?? true,
                wordCount: (int)(nudPassphraseWords?.Value ?? AppPreferences.PassphraseWordCount),
                capitalizeWords: chkPassphraseCapitalize?.Checked ?? true,
                includeNumbers: chkPassphraseNumbers?.Checked ?? true,
                includeSpecial: chkPassphraseSpecial?.Checked ?? true);

            MessageBox.Show(
                "Generation options saved. Generate will use these settings next time.",
                "Preferences Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// Fills the password field from the current password settings (length / numbers / symbols).
        private void ApplyGeneratedPassword()
        {
            lastGeneratedWasPassphrase = false;
            UpdateGenerationSettingsMode();
            RegeneratePassword();
            MarkFieldAsGenerated();
        }

        /// Fills the password field with a passphrase using the current passphrase settings.
        private void ApplyGeneratedPassphrase()
        {
            lastGeneratedWasPassphrase = true;
            UpdateGenerationSettingsMode();

            var options = new PassphraseGenerator.Options
            {
                WordCount = (int)(nudPassphraseWords?.Value ?? PassphraseGenerator.DefaultWordCount),
                Capitalize = chkPassphraseCapitalize?.Checked ?? true,
                IncludeNumbers = chkPassphraseNumbers?.Checked ?? true,
                IncludeSpecialCharacters = chkPassphraseSpecial?.Checked ?? true
            };
            txtPassword.Text = PassphraseGenerator.Generate(options);
            MarkFieldAsGenerated();
        }

        private CheckBox CreatePassphraseOptionCheckBox(string text, bool isChecked)
        {
            var check = new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Surface,
                Margin = new Padding(4, 4, 4, 4),
                Height = 28
            };
            check.CheckedChanged += (s, e) =>
            {
                if (lastGeneratedWasPassphrase && isGeneratedPassword)
                {
                    ApplyGeneratedPassphrase();
                }
            };
            return check;
        }

        private void MarkFieldAsGenerated()
        {
            isGeneratedPassword = true;
            txtRepeatPassword.Enabled = false;
            ClearRepeatPassword();
        }

        private void BtnTogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;
            txtPassword.PasswordChar = isPasswordVisible ? '\0' : '*';
            txtRepeatPassword.PasswordChar = isPasswordVisible ? '\0' : '*';
            btnTogglePasswordVisibility.Text = isPasswordVisible ? "🙈" : "👁️";
            if (btnTogglePasswordVisibility.Tag is ToolTip tip)
            {
                tip.SetToolTip(btnTogglePasswordVisibility, isPasswordVisible ? "Hide password" : "Show password");
            }
        }

        private CheckBox CreatePasswordOptionCheckBox(string text, bool isChecked)
        {
            var check = new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Surface,
                Margin = new Padding(4, 4, 12, 4)
            };
            check.CheckedChanged += PasswordOption_CheckedChanged;
            return check;
        }

        private void PasswordOption_CheckedChanged(object sender, EventArgs e)
        {
            // Keep at least one character set enabled.
            if (sender is CheckBox changed && !changed.Checked && !AnyPasswordCharsetEnabled())
            {
                changed.Checked = true;
                return;
            }

            if (!lastGeneratedWasPassphrase && isGeneratedPassword)
            {
                RegeneratePassword();
                MarkFieldAsGenerated();
            }
        }

        private bool AnyPasswordCharsetEnabled()
        {
            return (chkUppercase?.Checked ?? false)
                || (chkLowercase?.Checked ?? false)
                || (chkNumbers?.Checked ?? false)
                || (chkSymbols?.Checked ?? false);
        }

        private void TxtPassword_TextChanged(object sender, EventArgs e)
        {
            // If the password is manually entered, enable repeat password textbox
            if (txtPassword.Text.Length > 0)
            {
                txtRepeatPassword.Enabled = true; // Enable repeat password textbox
                isGeneratedPassword = false; // Set flag to indicate password is manually entered
            }
            else
            {
                txtRepeatPassword.Enabled = false; // Disable if password is empty
            }

            UpdatePasswordStrengthMeter();
        }

        private void UpdatePasswordStrengthMeter()
        {
            var (score, label, color) = PasswordStrength.Evaluate(txtPassword.Text);

            pnlStrengthBarFill.Width = (int)(pnlStrengthBarContainer.ClientSize.Width * (score / 100.0));
            pnlStrengthBarFill.BackColor = color;
            lblStrengthText.Text = label;
            lblStrengthText.ForeColor = color;
        }

        private void TxtRepeatPassword_TextChanged(object sender, EventArgs e)
        {
            // Optionally, you could provide feedback if the passwords don't match.
            // Only meaningful when the field is enabled (manually-typed password);
            // for generated passwords the repeat field is disabled and cleared, and
            // that clearing shouldn't be flagged as a "mismatch".
            if (txtRepeatPassword.Enabled && txtPassword.Text != txtRepeatPassword.Text)
            {
                txtRepeatPassword.BackColor = System.Drawing.Color.IndianRed; // Same semantic red as the "Weak" strength tier
            }
            else
            {
                txtRepeatPassword.BackColor = AppTheme.Surface; // Reset colour
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateEntry())
            {
                return; // Stop here if validation fails; let the user correct the issue
            }

            Service = txtService.Text.Trim();
            Username = txtUsername.Text.Trim();
            Password = txtPassword.Text;
            Notes = txtNotes.Text;
            Url = txtUrl.Text.Trim(); // Get URL from the new field
            this.DialogResult = DialogResult.OK; // Indicate success
            this.Close();
        }

        private bool ValidateEntry()
        {
            if (string.IsNullOrWhiteSpace(txtService.Text))
            {
                MessageBox.Show("Please enter a service name.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtService.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Please enter a username.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUsername.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Please enter or generate a password.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPassword.Focus();
                return false;
            }

            // The repeat password field is only enabled for manually-typed passwords
            // (generated passwords disable it), so only enforce the match in that case.
            if (txtRepeatPassword.Enabled && txtPassword.Text != txtRepeatPassword.Text)
            {
                MessageBox.Show("Passwords do not match. Please re-enter the repeat password.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRepeatPassword.Focus();
                return false;
            }

            return true;
        }

        private void ClearRepeatPassword()
        {
            txtRepeatPassword.Text = string.Empty; // Clear the repeat password textbox
        }

        private void RegeneratePassword()
        {
            var options = new PasswordGenerator.Options
            {
                Length = sliderPasswordLength?.Value ?? PasswordGenerator.DefaultLength,
                Uppercase = chkUppercase?.Checked ?? true,
                Lowercase = chkLowercase?.Checked ?? true,
                Digits = chkNumbers?.Checked ?? true,
                Symbols = chkSymbols?.Checked ?? true
            };

            try
            {
                txtPassword.Text = PasswordGenerator.Generate(options);
            }
            catch (InvalidOperationException)
            {
                // No character sets selected — prevented by the checkbox handler.
                return;
            }

            txtRepeatPassword.Enabled = false;
            isGeneratedPassword = true;
            txtRepeatPassword.BackColor = AppTheme.Surface;
        }

        private void BtnManageUsernames_Click(object sender, EventArgs e)
        {
            using (var manageForm = new ManageUsernamesForm())
            {
                var mousePos = Control.MousePosition;
                manageForm.StartPosition = FormStartPosition.Manual;
                manageForm.Location = new System.Drawing.Point(mousePos.X, mousePos.Y);
                manageForm.ShowDialog();
            }

            RefreshUsernameSuggestions(); // Pick up any changes made in the manage dialog
        }

        /// Builds one "label above field" group for this form's stacked layout - the same
        /// "caption then value" idea as MainForm's CreateDetailRow, just arranged vertically
        /// instead of in one row, matching the reference apps' field style. fieldRow can be
        /// a bare input (a single-field row) or a small Panel containing an input plus
        /// inline buttons (e.g. the password row).
        private static Panel CreateFieldGroup(string labelText, Control fieldRow, int fieldHeight)
            => DialogControls.CreateFieldGroup(labelText, fieldRow, fieldHeight);

        /// Builds one small flat icon button (Show/Generate/Manage/etc.), styled consistently
        /// with MainForm's nav buttons - flat, borderless, accent-tinted hover.
        private static Button CreateIconButton(string icon, int size)
            => DialogControls.CreateIconButton(icon, size);

        private void RefreshUsernameSuggestions()
        {
            List<string> suggestions;
            try
            {
                suggestions = UsernameSuggestionsStore.LoadUsernames();
            }
            catch (Exception)
            {
                // Suggestions are a convenience, not core functionality - a bad file here
                // shouldn't block using the form, just fall back to no suggestions.
                suggestions = new List<string>();
            }

            // Distinct, non-empty, alphabetical - duplicates or blanks would just clutter
            // both the dropdown list and the type-ahead suggestions without adding anything
            // useful, and an alphabetical dropdown is much easier to browse than insertion order.
            string[] distinctSuggestions = suggestions
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Items drives what shows up when the dropdown arrow is clicked (browse without
            // typing). Preserve whatever's currently typed/selected, since this also runs
            // after editing the suggestion list mid-form via the manage button.
            string currentText = txtUsername.Text;
            txtUsername.Items.Clear();
            txtUsername.Items.AddRange(distinctSuggestions);
            txtUsername.Text = currentText;

            // AutoCompleteCustomSource drives the separate live-filtering-while-typing
            // behavior - Items alone doesn't affect this.
            var usernameSuggestions = new AutoCompleteStringCollection();
            usernameSuggestions.AddRange(distinctSuggestions);

            txtUsername.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            txtUsername.AutoCompleteSource = AutoCompleteSource.CustomSource;
            txtUsername.AutoCompleteCustomSource = usernameSuggestions;
        }
    }
}