using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace PasswordManager
{
    public class AddEntryForm : Form
    {
        private Label lblService;
        private TextBox txtService;
        private Label lblUsername;
        private TextBox txtUsername;
        private Label lblPassword;
        private TextBox txtPassword;
        private TextBox txtNotes;
        private Button btnGeneratePassword;
        private Button btnTogglePasswordVisibility;
        private Button btnSave;
        private Button btnCancel;
        private Button btnToggleLengthSlider; // Toggle button for slider
        private Button btnToggleSymbols; // Toggle button for symbols
        private Button btnToggleNumbers; // Toggle button for numbers
        private TrackBar tbPasswordLength;
        private Label lblPasswordLength;

        public string Service { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string Notes { get; private set; }

        private bool isPasswordVisible = false; // Track visibility state
        private bool isSliderVisible = false; // Track slider visibility state
        private bool includeSymbols = true; // Track inclusion of symbols
        private bool includeNumbers = true; // Track inclusion of numbers

        public AddEntryForm()
        {
            // Set fixed size
            this.Size = new System.Drawing.Size(700, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Initialize labels and text boxes
            lblService = new Label { Text = "Service Name:", Dock = DockStyle.Top };
            txtService = new TextBox { PlaceholderText = "Enter Service Name", Dock = DockStyle.Top, Height = 40 };

            lblUsername = new Label { Text = "Username:", Dock = DockStyle.Top };
            txtUsername = new TextBox { PlaceholderText = "Enter Username", Dock = DockStyle.Top, Height = 40 };

            lblPassword = new Label { Text = "Password:", Dock = DockStyle.Top };
            txtPassword = new TextBox { PlaceholderText = "Enter Password", Dock = DockStyle.Top, Height = 40, PasswordChar = '*' };

            txtNotes = new TextBox { PlaceholderText = "Notes", Dock = DockStyle.Top, Multiline = true, Height = 80 };

            // Create buttons for password actions
            btnGeneratePassword = new Button { Text = "🔄", Dock = DockStyle.Right, Width = 60, Height = 50, Font = new System.Drawing.Font("Arial", 16) };
            btnTogglePasswordVisibility = new Button { Text = "👁️", Dock = DockStyle.Right, Width = 60, Height = 50, Font = new System.Drawing.Font("Arial", 16) };
            btnSave = new Button { Text = "Save", Dock = DockStyle.Bottom };
            btnCancel = new Button { Text = "Cancel", Dock = DockStyle.Bottom };

            // Toggle button for showing password length slider
            btnToggleLengthSlider = new Button { Text = "🔽", Dock = DockStyle.Left, Width = 60, Height = 50, Font = new System.Drawing.Font("Arial", 16) };

            // Toggle buttons for password options
            btnToggleSymbols = new Button { Text = "Symbols: On", Dock = DockStyle.Left, Width = 100, Height = 50, BackColor = System.Drawing.Color.LightGreen };
            btnToggleNumbers = new Button { Text = "Numbers: On", Dock = DockStyle.Left, Width = 100, Height = 50, BackColor = System.Drawing.Color.LightGreen };

            // TrackBar for password length
            lblPasswordLength = new Label { Text = "Password Length: 12", Dock = DockStyle.Top, Visible = false };
            tbPasswordLength = new TrackBar { Minimum = 6, Maximum = 20, Value = 12, Dock = DockStyle.Top, Visible = false };
            tbPasswordLength.Scroll += (sender, e) => lblPasswordLength.Text = $"Password Length: {tbPasswordLength.Value}";

            // Add event handlers
            btnGeneratePassword.Click += BtnGeneratePassword_Click;
            btnTogglePasswordVisibility.Click += BtnTogglePasswordVisibility_Click;
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (sender, e) => this.Close();
            btnToggleLengthSlider.Click += BtnToggleLengthSlider_Click; // Event for slider toggle button
            btnToggleSymbols.Click += BtnToggleSymbols_Click; // Event for symbols toggle button
            btnToggleNumbers.Click += BtnToggleNumbers_Click; // Event for numbers toggle button

            // Create a panel to hold password and action buttons
            var passwordPanel = new Panel { Dock = DockStyle.Top, Height = 50 };
            passwordPanel.Controls.Add(btnToggleLengthSlider); // Add toggle button for slider
            passwordPanel.Controls.Add(btnTogglePasswordVisibility);
            passwordPanel.Controls.Add(btnToggleSymbols);
            passwordPanel.Controls.Add(btnToggleNumbers);
            passwordPanel.Controls.Add(btnGeneratePassword);
            passwordPanel.Controls.Add(txtPassword);

            // Add controls to form
            Controls.Add(txtNotes);
            Controls.Add(lblPassword);
            Controls.Add(passwordPanel); // Add the password panel
            Controls.Add(lblPasswordLength); // Add label for password length below the password field
            Controls.Add(tbPasswordLength); // Add slider below the password field
            Controls.Add(lblUsername);
            Controls.Add(txtUsername);
            Controls.Add(lblService);
            Controls.Add(txtService);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);

            Text = "Add Entry";
        }

        private void BtnGeneratePassword_Click(object sender, EventArgs e)
        {
            txtPassword.Text = GenerateRandomPassword(tbPasswordLength.Value); // Generate a password of selected length
        }

        private void BtnTogglePasswordVisibility_Click(object sender, EventArgs e)
        {
            isPasswordVisible = !isPasswordVisible; // Toggle visibility state
            txtPassword.PasswordChar = isPasswordVisible ? '\0' : '*'; // Show or hide password
            btnTogglePasswordVisibility.Text = isPasswordVisible ? "🙈" : "👁️"; // Update button icon
        }

        private void BtnToggleLengthSlider_Click(object sender, EventArgs e)
        {
            isSliderVisible = !isSliderVisible; // Toggle slider visibility
            tbPasswordLength.Visible = isSliderVisible;
            lblPasswordLength.Visible = isSliderVisible; // Show or hide the slider and label
        }

        private void BtnToggleSymbols_Click(object sender, EventArgs e)
        {
            includeSymbols = !includeSymbols; // Toggle symbols inclusion
            btnToggleSymbols.Text = includeSymbols ? "Symbols: On" : "Symbols: Off";
            btnToggleSymbols.BackColor = includeSymbols ? System.Drawing.Color.LightGreen : System.Drawing.Color.LightCoral;
        }

        private void BtnToggleNumbers_Click(object sender, EventArgs e)
        {
            includeNumbers = !includeNumbers; // Toggle numbers inclusion
            btnToggleNumbers.Text = includeNumbers ? "Numbers: On" : "Numbers: Off";
            btnToggleNumbers.BackColor = includeNumbers ? System.Drawing.Color.LightGreen : System.Drawing.Color.LightCoral;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            Service = txtService.Text;
            Username = txtUsername.Text;
            Password = txtPassword.Text;
            Notes = txtNotes.Text;
            this.DialogResult = DialogResult.OK; // Indicate success
            this.Close();
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