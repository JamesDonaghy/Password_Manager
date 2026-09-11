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
        private TextBox txtService;
        private ComboBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtRepeatPassword; // New repeat password field
        private TextBox txtUrl; // New URL field
        private TextBox txtNotes;
        private Button btnGeneratePassword;
        private Button btnTogglePasswordVisibility;
        private Button btnManageUsernames; // Opens the curated username suggestions list
        private Button btnSave;
        private Button btnCancel;
        private Button btnToggleSymbols; // Button for symbols
        private Button btnToggleNumbers; // Button for numbers
        private Button btnToggleLengthSlider; // Toggle button for the length slider
        private TrackBar sliderPasswordLength; // Slider for password length
        private Label lblCurrentLength; // Label to show current length
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
        private bool includeSymbols = true; // Track inclusion of symbols
        private bool includeNumbers = true; // Track inclusion of numbers
        private bool isGeneratedPassword = false; // Track if using a generated password

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

            // The length slider row (TrackBar + label) is only added to the layout once,
            // but its Visible starts false so it takes no space initially. When toggled on,
            // the dialog needs to grow by roughly this much or the extra content pushes the
            // total past the window's fixed height, which trips the AutoScroll safety net's
            // scrollbar - and that scrollbar eats width from the row above it, squeezing the
            // password field's icon buttons (reported in review). Growing the window itself
            // instead avoids that, and avoids permanently reserving the space when hidden.
            const int lengthRowHeight = 55;

            txtService = new TextBox { PlaceholderText = "Service Name", BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtUsername = new ComboBox { Font = AppTheme.Base, DropDownStyle = ComboBoxStyle.DropDown };
            RefreshUsernameSuggestions(); // Builds suggestions from UsernameSuggestionsStore

            btnManageUsernames = CreateIconButton("👤", iconSize); // Manage suggested usernames
            btnManageUsernames.Click += BtnManageUsernames_Click;

            txtPassword = new TextBox { PlaceholderText = "Password", BorderStyle = BorderStyle.FixedSingle, PasswordChar = '*', Font = AppTheme.Base };
            txtRepeatPassword = new TextBox { PlaceholderText = "Repeat Password", BorderStyle = BorderStyle.FixedSingle, PasswordChar = '*', Font = AppTheme.Base, Enabled = false }; // Repeat password field disabled by default
            txtUrl = new TextBox { PlaceholderText = "URL", BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.Base };
            txtNotes = new TextBox { PlaceholderText = "Notes", BorderStyle = BorderStyle.FixedSingle, Multiline = true, Font = AppTheme.Base };

            // Password length slider
            lblCurrentLength = new Label { Text = "15", Dock = DockStyle.Left, Width = 30, Font = AppTheme.Base, ForeColor = AppTheme.TextPrimary, Visible = false }; // Label to show current length, initially hidden
            sliderPasswordLength = new TrackBar
            {
                Minimum = 6,
                Maximum = 20,
                Value = 15,
                Dock = DockStyle.Top,
                Height = 45,
                TickFrequency = 1,
                LargeChange = 1,
                SmallChange = 1,
                Width = 300, // Make the slider wider
                Visible = false // Initially hidden
            };
            sliderPasswordLength.Scroll += (s, e) =>
            {
                lblCurrentLength.Text = sliderPasswordLength.Value.ToString(); // Update current length label
                RegeneratePassword(); // Regenerate password when slider changes
            };

            // Toggle button for showing/hiding the length slider with an icon. Styled the
            // same as the other icon buttons rather than the previous one-off LightBlue -
            // it's a "show more options" disclosure, not an on/off state like symbols/numbers,
            // so it doesn't get their active/inactive treatment.
            btnToggleLengthSlider = CreateIconButton("🔧", iconSize);
            btnToggleLengthSlider.Click += (sender, e) =>
            {
                bool willShow = !sliderPasswordLength.Visible;

                // SuspendLayout/ResumeLayout batches the resize and the visibility change
                // into a single layout pass, but that alone didn't stop the flicker
                // (reported in review): AutoScroll's scrollbar decision is made as PART of
                // that batched layout pass, evaluated against whatever the content height
                // happens to be at that instant - so it can still momentarily decide a
                // scrollbar is needed before the Form has actually finished growing.
                //
                // Turning AutoScroll off for the duration removes the scrollbar calculation
                // entirely, so there's nothing to flash. It's switched back on only once the
                // Form is already at its final size, so contentScroll's one-and-only
                // evaluation happens against the correct height and (in the normal case)
                // never needs a scrollbar at all.
                this.SuspendLayout();
                contentScroll.SuspendLayout();
                contentScroll.AutoScroll = false;

                if (willShow)
                {
                    this.Height += lengthRowHeight;
                }

                sliderPasswordLength.Visible = willShow;

                // Only show current length label when slider is visible
                lblCurrentLength.Visible = willShow;
                if (willShow)
                {
                    lblCurrentLength.Text = sliderPasswordLength.Value.ToString(); // Show the current length when the slider is displayed
                }

                if (!willShow)
                {
                    this.Height -= lengthRowHeight;
                }

                contentScroll.ResumeLayout(true);
                this.ResumeLayout(true);
                contentScroll.AutoScroll = true; // Re-enable as the safety net, now evaluated against the final size
            };

            // Create buttons for password actions with matching sizes
            btnGeneratePassword = CreateIconButton("🔄", iconSize); // Generate icon
            btnTogglePasswordVisibility = CreateIconButton("👁️", iconSize); // Eye icon

            // Save/Cancel as proper labeled buttons instead of emoji-only icons, matching
            // the reference apps' dialog buttons. Uses the shared primary/secondary button
            // helpers (DialogControls) so later dialogs reuse this exact same look.
            btnSave = DialogControls.CreatePrimaryButton("Save", 96, 32);
            btnCancel = DialogControls.CreateSecondaryButton("Cancel", 96, 32);

            // Buttons for symbols and numbers - active/inactive now shown via the theme's
            // accent tint instead of ad-hoc LightGreen/LightCoral, and set to match their
            // actual starting state (includeSymbols/includeNumbers both default true) rather
            // than only updating on the first click as before.
            btnToggleSymbols = CreateIconButton("⚙️", iconSize); // Gear icon for symbols
            btnToggleSymbols.BackColor = AppTheme.AccentSubtle;
            btnToggleNumbers = CreateIconButton("🔢", iconSize); // Numbers icon
            btnToggleNumbers.BackColor = AppTheme.AccentSubtle;

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
            btnGeneratePassword.Click += BtnGeneratePassword_Click;
            btnTogglePasswordVisibility.Click += BtnTogglePasswordVisibility_Click; // Do not clear repeat password
            btnSave.Click += BtnSave_Click; // Clear repeat password
            btnCancel.Click += (sender, e) => this.Close(); // Clear repeat password
            btnToggleSymbols.Click += BtnToggleSymbols_Click; // Event for symbols toggle button
            btnToggleNumbers.Click += BtnToggleNumbers_Click; // Event for numbers toggle button

            // Adding manual entry detection
            txtPassword.TextChanged += TxtPassword_TextChanged; // Update strength when password changes
            txtRepeatPassword.TextChanged += TxtRepeatPassword_TextChanged; // Check match on repeat password text change

            // Username row: field fills remaining width, manage-usernames button to its right.
            var usernameRow = new Panel { BackColor = AppTheme.Background };
            usernameRow.Controls.Add(txtUsername);
            txtUsername.Dock = DockStyle.Fill;
            usernameRow.Controls.Add(btnManageUsernames);

            // Password row: field fills remaining width, action icons to its right. Right-
            // docked siblings render in the SAME order added (unlike Top/Left, where it's
            // reversed) - the last one added lands flush against the true right edge - so
            // adding left-to-right in reading order here places them correctly.
            var passwordRow = new Panel { BackColor = AppTheme.Background };
            passwordRow.Controls.Add(txtPassword);
            txtPassword.Dock = DockStyle.Fill;
            passwordRow.Controls.Add(btnTogglePasswordVisibility);
            passwordRow.Controls.Add(btnGeneratePassword);
            passwordRow.Controls.Add(btnToggleSymbols);
            passwordRow.Controls.Add(btnToggleNumbers);
            passwordRow.Controls.Add(btnToggleLengthSlider);

            // Strength meter and length slider stay as their own small rows (fixed-size
            // controls sitting side by side) rather than the label-above-field pattern used
            // for the real inputs - a FlowLayoutPanel is the simplest fit for that shape.
            var strengthPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 2, 0, 2), Margin = new Padding(0), BackColor = AppTheme.Background };
            strengthPanel.Controls.Add(pnlStrengthBarContainer);
            strengthPanel.Controls.Add(lblStrengthText);

            var lengthPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4), Margin = new Padding(0), BackColor = AppTheme.Background };
            lengthPanel.Controls.Add(sliderPasswordLength); // Add slider for password length
            lengthPanel.Controls.Add(lblCurrentLength); // Add current length label

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
            contentScroll.Controls.Add(lengthPanel);
            contentScroll.Controls.Add(strengthPanel);
            contentScroll.Controls.Add(passwordGroup);
            contentScroll.Controls.Add(usernameGroup);
            contentScroll.Controls.Add(serviceGroup);

            Controls.Add(contentScroll);
            Controls.Add(actionPanel);
            Controls.Add(lblFormTitle);

            Text = "Add Entry";

            // Set the Load event handler
            this.Load += AddEntryForm_Load;

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

        private void AddEntryForm_Load(object sender, EventArgs e)
        {
            txtService.Focus(); // Set focus to the Service Name text field
        }

        private void BtnGeneratePassword_Click(object sender, EventArgs e)
        {
            RegeneratePassword(); // Call the new method to generate password
            isGeneratedPassword = true; // Set flag to indicate generated password
            txtRepeatPassword.Enabled = false; // Disable repeat password textbox
            ClearRepeatPassword(); // Clear repeat password textbox
        }

        private void BtnTogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            isPasswordVisible = !isPasswordVisible; // Toggle visibility state
            txtPassword.PasswordChar = isPasswordVisible ? '\0' : '*'; // Show or hide password
            txtRepeatPassword.PasswordChar = isPasswordVisible ? '\0' : '*'; // Show or hide repeat password
            btnTogglePasswordVisibility.Text = isPasswordVisible ? "🙈" : "👁️"; // Update button icon
        }

        private void BtnToggleSymbols_Click(object sender, EventArgs e)
        {
            includeSymbols = !includeSymbols; // Toggle symbols inclusion
            btnToggleSymbols.BackColor = includeSymbols ? AppTheme.AccentSubtle : AppTheme.PanelBackground;

            RegeneratePassword(); // Regenerate password when toggling symbols
            ClearRepeatPassword(); // Clear repeat password textbox
        }

        private void BtnToggleNumbers_Click(object sender, EventArgs e)
        {
            includeNumbers = !includeNumbers; // Toggle numbers inclusion
            btnToggleNumbers.BackColor = includeNumbers ? AppTheme.AccentSubtle : AppTheme.PanelBackground;

            RegeneratePassword(); // Regenerate password when toggling numbers
            ClearRepeatPassword(); // Clear repeat password textbox
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
            int length = sliderPasswordLength.Value; // Get the length from the slider
            txtPassword.Text = GenerateRandomPassword(length); // Generate a password of selected length
            txtRepeatPassword.Enabled = false; // Disable repeat password textbox when regenerating
            isGeneratedPassword = true; // Set flag to indicate generated password
            txtRepeatPassword.BackColor = AppTheme.Surface; // Reset colour
        }

        private string GenerateRandomPassword(int length)
        {
            const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "1234567890";
            const string symbols = "!@#$%^&*()";

            StringBuilder validChars = new StringBuilder(letters);
            if (includeNumbers)
                validChars.Append(numbers);
            if (includeSymbols)
                validChars.Append(symbols);

            StringBuilder result = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                // RandomNumberGenerator.GetInt32 is the modern replacement for
                // RNGCryptoServiceProvider (now obsolete). It's also unbiased: the previous
                // "random byte % validChars.Length" approach slightly favored characters
                // near the start of validChars, since 256 doesn't divide evenly into most
                // charset lengths. GetInt32 picks uniformly from [0, validChars.Length).
                int index = RandomNumberGenerator.GetInt32(validChars.Length);
                result.Append(validChars[index]);
            }
            return result.ToString();
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