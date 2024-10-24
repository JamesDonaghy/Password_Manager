using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace PasswordManager
{
    public class AddEntryForm : Form
    {
        private TextBox txtService;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtRepeatPassword; // New repeat password field
        private TextBox txtUrl; // New URL field
        private TextBox txtNotes;
        private Button btnGeneratePassword;
        private Button btnTogglePasswordVisibility;
        private Button btnSave;
        private Button btnCancel;
        private Button btnToggleSymbols; // Button for symbols
        private Button btnToggleNumbers; // Button for numbers
        private Button btnToggleLengthSlider; // Toggle button for the length slider
        private TrackBar sliderPasswordLength; // Slider for password length
        private Label lblCurrentLength; // Label to show current length

        public string Service { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string Notes { get; private set; }
        public string Url { get; private set; } // URL property

        private bool isPasswordVisible = false; // Track visibility state
        private bool includeSymbols = true; // Track inclusion of symbols
        private bool includeNumbers = true; // Track inclusion of numbers
        private bool isGeneratedPassword = false; // Track if using a generated password

        public AddEntryForm()
        {
            // Set fixed size
            this.Size = new System.Drawing.Size(700, 600);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Initialize text boxes with half the width of the form
            int textBoxWidth = this.ClientSize.Width / 2;
            int buttonHeight = 30; // Smaller button height
            int buttonWidth = 40; // Smaller button width

            // Set a larger font size
            var font = new System.Drawing.Font("Arial", 10);

            txtService = new TextBox { PlaceholderText = "Service Name", Dock = DockStyle.Top, Width = textBoxWidth, Height = buttonHeight + 10, Font = font };
            txtUsername = new TextBox { PlaceholderText = "Username", Dock = DockStyle.Top, Width = textBoxWidth, Height = buttonHeight + 10, Font = font };
            txtPassword = new TextBox { PlaceholderText = "Password", Dock = DockStyle.Top, Width = textBoxWidth, Height = buttonHeight + 10, PasswordChar = '*', Font = font };
            txtRepeatPassword = new TextBox { PlaceholderText = "Repeat Password", Dock = DockStyle.Top, Width = textBoxWidth, Height = buttonHeight + 10, PasswordChar = '*', Font = font, Enabled = false }; // Repeat password field disabled by default
            txtUrl = new TextBox { PlaceholderText = "URL", Dock = DockStyle.Top, Width = textBoxWidth, Height = buttonHeight + 10, Font = font }; // URL field
            txtNotes = new TextBox { PlaceholderText = "Notes", Dock = DockStyle.Top, Multiline = true, Width = textBoxWidth, Height = 80, Font = font };

            // Password length slider
            lblCurrentLength = new Label { Text = "15", Dock = DockStyle.Top, Font = font, Visible = false }; // Label to show current length, initially hidden
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

            // Toggle button for showing/hiding the length slider with an icon
            btnToggleLengthSlider = new Button { Text = "🔧", Width = buttonWidth, Height = buttonHeight, Font = font, BackColor = System.Drawing.Color.LightBlue }; // Wrench icon
            btnToggleLengthSlider.Click += (sender, e) => 
            {
                sliderPasswordLength.Visible = !sliderPasswordLength.Visible;

                // Only show current length label when slider is visible
                lblCurrentLength.Visible = sliderPasswordLength.Visible; 
                if (sliderPasswordLength.Visible)
                {
                    lblCurrentLength.Text = sliderPasswordLength.Value.ToString(); // Show the current length when the slider is displayed
                }
            };

            // Create buttons for password actions with matching sizes
            btnGeneratePassword = new Button { Text = "🔄", Width = buttonWidth, Height = buttonHeight, Font = font }; // Generate icon
            btnTogglePasswordVisibility = new Button { Text = "👁️", Width = buttonWidth, Height = buttonHeight, Font = font }; // Eye icon
            btnSave = new Button { Text = "💾", Width = buttonWidth, Height = buttonHeight, Font = font }; // Save icon
            btnCancel = new Button { Text = "❌", Width = buttonWidth, Height = buttonHeight, Font = font }; // Cancel icon

            // Buttons for symbols and numbers
            btnToggleSymbols = new Button { Text = "⚙️", Width = buttonWidth, Height = buttonHeight, BackColor = System.Drawing.Color.LightGreen, Font = font }; // Gear icon for symbols
            btnToggleNumbers = new Button { Text = "🔢", Width = buttonWidth, Height = buttonHeight, BackColor = System.Drawing.Color.LightGreen, Font = font }; // Numbers icon

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

            // Create a panel for the service and username text boxes
            var textBoxPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            textBoxPanel.Controls.Add(txtService);
            textBoxPanel.Controls.Add(txtUsername);

            // Create a panel to hold password and action buttons
            var passwordPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            passwordPanel.Controls.Add(txtPassword);
            passwordPanel.Controls.Add(btnTogglePasswordVisibility);
            passwordPanel.Controls.Add(btnGeneratePassword);
            passwordPanel.Controls.Add(btnToggleSymbols); // Add symbols button
            passwordPanel.Controls.Add(btnToggleNumbers); // Add numbers button
            passwordPanel.Controls.Add(btnToggleLengthSlider); // Add toggle button for length slider

            // Create a panel for repeat password and URL fields
            var repeatUrlPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            repeatUrlPanel.Controls.Add(txtRepeatPassword);
            repeatUrlPanel.Controls.Add(txtUrl);

            // Create a panel for password length
            var lengthPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            lengthPanel.Controls.Add(sliderPasswordLength); // Add slider for password length
            lengthPanel.Controls.Add(lblCurrentLength); // Add current length label

            // Create a panel for save/cancel buttons
            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnSave);

            // Add controls to form in the correct order
            Controls.Add(txtNotes); // Add notes field at the bottom
            Controls.Add(actionPanel); // Add action panel for buttons
            Controls.Add(repeatUrlPanel); // Add repeat password and URL panel
            Controls.Add(lengthPanel); // Add the password length panel
            Controls.Add(passwordPanel); // Add the password panel
            Controls.Add(textBoxPanel); // Add the text box panel

            Text = "Add Entry";

            // Set the Load event handler
            this.Load += AddEntryForm_Load;
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
            btnToggleSymbols.Text = includeSymbols ? "⚙️" : "⚙️"; // Keep gear icon
            btnToggleSymbols.BackColor = includeSymbols ? System.Drawing.Color.LightGreen : System.Drawing.Color.LightCoral;

            RegeneratePassword(); // Regenerate password when toggling symbols
            ClearRepeatPassword(); // Clear repeat password textbox
        }

        private void BtnToggleNumbers_Click(object sender, EventArgs e)
        {
            includeNumbers = !includeNumbers; // Toggle numbers inclusion
            btnToggleNumbers.Text = includeNumbers ? "🔢" : "🔢"; // Keep numbers icon
            btnToggleNumbers.BackColor = includeNumbers ? System.Drawing.Color.LightGreen : System.Drawing.Color.LightCoral;

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
        }

        private void TxtRepeatPassword_TextChanged(object sender, EventArgs e)
        {
            // Optionally, you could provide feedback if the passwords don't match
            if (txtPassword.Text != txtRepeatPassword.Text)
            {
                txtRepeatPassword.BackColor = System.Drawing.Color.LightCoral; // Indicate mismatch
            }
            else
            {
                txtRepeatPassword.BackColor = System.Drawing.Color.White; // Reset color
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            Service = txtService.Text;
            Username = txtUsername.Text;
            Password = txtPassword.Text;
            Notes = txtNotes.Text;
            Url = txtUrl.Text; // Get URL from the new field
            this.DialogResult = DialogResult.OK; // Indicate success
            this.Close();
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
            txtRepeatPassword.BackColor = System.Drawing.Color.White; // Reset color
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
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] data = new byte[length];
                rng.GetBytes(data);
                for (int i = 0; i < length; i++)
                {
                    result.Append(validChars[data[i] % validChars.Length]);
                }
            }
            return result.ToString();
        }
    }
}