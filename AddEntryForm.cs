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
        private const int GenerationSettingsHeight = 110;

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
            this.Size = new System.Drawing.Size(440, 560);

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
            lblCurrentLength = new Label
            {
                Text = PasswordGenerator.DefaultLength.ToString(),
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
                Value = PasswordGenerator.DefaultLength,
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

            // Generate menu: choose password or passphrase, then fill the field immediately.
            btnGeneratePassword = DialogControls.CreateSecondaryButton("Generate ▾", 100, iconSize);
            btnRegeneratePassword = CreateIconButton("🔄", iconSize);
            btnRegeneratePassword.Dock = DockStyle.None;
            var regenerateTip = new ToolTip();
            regenerateTip.SetToolTip(btnRegeneratePassword, "Regenerate");
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

            btnGenerationSettings = CreateIconButton("⚙", iconSize);
            btnGenerationSettings.Dock = DockStyle.None;
            var settingsTip = new ToolTip();
            settingsTip.SetToolTip(btnGenerationSettings, "Generation settings");
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
            btnSave = DialogControls.CreatePrimaryButton("Save", 96, 32);
            btnCancel = DialogControls.CreateSecondaryButton("Cancel", 96, 32);

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

            // Password row: single bordered box with the visibility control attached inside.
            // Generation controls sit on a separate row below.
            btnTogglePasswordVisibility.BackColor = AppTheme.Surface;
            var passwordVisibilityTip = new ToolTip();
            passwordVisibilityTip.SetToolTip(btnTogglePasswordVisibility, "Show password");
            btnTogglePasswordVisibility.Tag = passwordVisibilityTip; // updated when toggled
            var passwordRow = DialogControls.CreateBorderedFieldRow(txtPassword, btnTogglePasswordVisibility);

            // Action buttons: Generate, Regenerate, Settings. Character options live in the settings panel.
            btnGeneratePassword.Dock = DockStyle.None;
            btnRegeneratePassword.Dock = DockStyle.None;
            btnGenerationSettings.Dock = DockStyle.None;
            btnGeneratePassword.Margin = new Padding(0, 0, 6, 0);
            btnRegeneratePassword.Margin = new Padding(0, 0, 6, 0);
            btnGenerationSettings.Margin = new Padding(0, 0, 0, 0);

            var passwordActionsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                // Extra bottom padding so Settings actions sit a bit above the length row.
                Padding = new Padding(0, 4, 0, 10),
                Margin = new Padding(0),
                BackColor = AppTheme.Background
            };
            passwordActionsRow.Controls.Add(btnGeneratePassword);
            passwordActionsRow.Controls.Add(btnRegeneratePassword);
            passwordActionsRow.Controls.Add(btnGenerationSettings);

            // --- Password settings content (length + charset) ---
            chkUppercase = CreatePasswordOptionCheckBox("Uppercase", true);
            chkLowercase = CreatePasswordOptionCheckBox("Lowercase", true);
            chkNumbers = CreatePasswordOptionCheckBox("Numbers", true);
            chkSymbols = CreatePasswordOptionCheckBox("Symbols", true);

            var optionsGrid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = AppTheme.Background
            };
            optionsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optionsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optionsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsGrid.Controls.Add(chkUppercase, 0, 0);
            optionsGrid.Controls.Add(chkLowercase, 1, 0);
            optionsGrid.Controls.Add(chkNumbers, 0, 1);
            optionsGrid.Controls.Add(chkSymbols, 1, 1);

            var optionsHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(0, 0, 0, 0),
                BackColor = AppTheme.Background
            };
            optionsHost.Controls.Add(optionsGrid);
            optionsHost.Resize += (s, e) =>
            {
                optionsGrid.Left = Math.Max(0, (optionsHost.ClientSize.Width - optionsGrid.Width) / 2);
                optionsGrid.Top = Math.Max(0, (optionsHost.ClientSize.Height - optionsGrid.Height) / 2);
            };

            var lengthRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0),
                Margin = new Padding(0),
                BackColor = AppTheme.Background
            };
            var lblLengthCaption = new Label
            {
                Text = "Length",
                AutoSize = false,
                Width = 52,
                Height = 24,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            lengthRow.Controls.Add(lblLengthCaption);
            lengthRow.Controls.Add(sliderPasswordLength);
            lengthRow.Controls.Add(lblCurrentLength);

            passwordSettingsContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Background
            };
            passwordSettingsContent.Controls.Add(optionsHost);
            passwordSettingsContent.Controls.Add(lengthRow);

            // --- Passphrase settings content (words + options) ---
            nudPassphraseWords = new NumericUpDown
            {
                Minimum = PassphraseGenerator.MinWordCount,
                Maximum = PassphraseGenerator.MaxWordCount,
                Value = PassphraseGenerator.DefaultWordCount,
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
                Padding = new Padding(0, 2, 0, 0),
                Margin = new Padding(0),
                BackColor = AppTheme.Background
            };
            wordsRow.Controls.Add(new Label
            {
                Text = "Words",
                AutoSize = false,
                Width = 52,
                Height = 24,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            });
            wordsRow.Controls.Add(nudPassphraseWords);

            chkPassphraseCapitalize = CreatePassphraseOptionCheckBox("Capitalize", true);
            chkPassphraseNumbers = CreatePassphraseOptionCheckBox("Numbers", true);
            chkPassphraseSpecial = CreatePassphraseOptionCheckBox("Hyphens", true);

            var passphraseOptionsGrid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = AppTheme.Background
            };
            passphraseOptionsGrid.Controls.Add(chkPassphraseCapitalize, 0, 0);
            passphraseOptionsGrid.Controls.Add(chkPassphraseNumbers, 1, 0);
            passphraseOptionsGrid.Controls.Add(chkPassphraseSpecial, 2, 0);

            var passphraseOptionsHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = AppTheme.Background
            };
            passphraseOptionsHost.Controls.Add(passphraseOptionsGrid);
            passphraseOptionsHost.Resize += (s, e) =>
            {
                passphraseOptionsGrid.Left = Math.Max(0, (passphraseOptionsHost.ClientSize.Width - passphraseOptionsGrid.Width) / 2);
                passphraseOptionsGrid.Top = Math.Max(0, (passphraseOptionsHost.ClientSize.Height - passphraseOptionsGrid.Height) / 2);
            };

            passphraseSettingsContent = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = AppTheme.Background
            };
            passphraseSettingsContent.Controls.Add(passphraseOptionsHost);
            passphraseSettingsContent.Controls.Add(wordsRow);

            generationSettingsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = GenerationSettingsHeight,
                Visible = false,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = AppTheme.Background
            };
            generationSettingsPanel.Controls.Add(passwordSettingsContent);
            generationSettingsPanel.Controls.Add(passphraseSettingsContent);

            var strengthPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 2, 0, 2), Margin = new Padding(0), BackColor = AppTheme.Background };
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
            passwordActionsRow.TabIndex = 3;
            generationSettingsPanel.TabIndex = 4;
            strengthPanel.TabIndex = 5;
            repeatGroup.TabIndex = 6;
            urlGroup.TabIndex = 7;
            notesGroup.TabIndex = 8;
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
            contentScroll.Controls.Add(notesGroup);
            contentScroll.Controls.Add(urlGroup);
            contentScroll.Controls.Add(repeatGroup);
            contentScroll.Controls.Add(strengthPanel);
            contentScroll.Controls.Add(generationSettingsPanel);
            contentScroll.Controls.Add(passwordActionsRow);
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
            if (generationSettingsPanel == null)
            {
                return;
            }

            bool willShow = !generationSettingsPanel.Visible;

            SuspendLayout();
            contentScroll.SuspendLayout();
            contentScroll.AutoScroll = false;

            if (willShow)
            {
                Height += GenerationSettingsHeight;
                UpdateGenerationSettingsMode();
            }

            generationSettingsPanel.Visible = willShow;
            btnGenerationSettings.BackColor = willShow ? AppTheme.AccentSubtle : AppTheme.PanelBackground;

            if (!willShow)
            {
                Height -= GenerationSettingsHeight;
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
                BackColor = AppTheme.Background,
                Margin = new Padding(8, 2, 8, 2)
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
                BackColor = AppTheme.Background,
                Margin = new Padding(8, 2, 8, 2)
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