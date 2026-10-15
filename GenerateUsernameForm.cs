using System;
using System.Drawing;
using System.Windows.Forms;

namespace PasswordManager
{
    /// <summary>
    /// Small dialog opened from Add/Edit Entry to generate a memorable username
    /// and optionally apply it to the username field. Options match the Generator
    /// page Username tab: words, separator, capitalization, include numbers.
    /// </summary>
    public class GenerateUsernameForm : Form
    {
        private NumericUpDown nudWords;
        private ComboBox cmbSeparator;
        private ComboBox cmbCapitalization;
        private CheckBox chkIncludeNumbers;
        private TextBox txtResult;
        private Button btnUseInEntry;
        private Panel separatorBlock;

        /// Username chosen via "Use in entry", or empty if cancelled.
        public string SelectedUsername { get; private set; } = "";

        public GenerateUsernameForm()
        {
            Text = "Generate Username";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.Background;
            Font = AppTheme.Base;
            ClientSize = new Size(300, 360);
            ShowInTaskbar = false;

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = AppTheme.Background
            };

            var lblTitle = new Label
            {
                Text = "Generate Username",
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font(AppTheme.Base.FontFamily, 12f, FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblSubtitle = new Label
            {
                Text = "Create a unique, memorable username\nfor this account.",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = ContentAlignment.TopLeft
            };

            nudWords = new NumericUpDown
            {
                Minimum = UsernameGenerator.MinWordCount,
                Maximum = UsernameGenerator.MaxWordCount,
                Value = UsernameGenerator.DefaultWordCount,
                Width = 56,
                Font = AppTheme.Base
            };

            cmbSeparator = CreateDropDown(64);
            cmbSeparator.Items.AddRange(new object[] { ".", "_", "-", "None" });
            cmbSeparator.SelectedIndex = 0;

            cmbCapitalization = CreateDropDown(130);
            cmbCapitalization.Items.AddRange(new object[] { "Lowercase", "Capitalized", "Mixed Case" });
            cmbCapitalization.SelectedIndex = 0;

            var wordsBlock = CreateLabeledControl("Words", nudWords, 64);
            separatorBlock = CreateLabeledControl("Separator", cmbSeparator, 72);
            var capitalBlock = CreateLabeledControl("Capitalization", cmbCapitalization, 140);

            var optionsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AppTheme.Background,
                Padding = new Padding(0, 4, 0, 0)
            };
            optionsRow.Controls.Add(wordsBlock);
            optionsRow.Controls.Add(separatorBlock);
            optionsRow.Controls.Add(capitalBlock);

            chkIncludeNumbers = new CheckBox
            {
                Text = "Include numbers",
                Dock = DockStyle.Top,
                Height = 28,
                Checked = true,
                Font = AppTheme.Base,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Background
            };

            var btnGenerate = DialogControls.CreatePrimaryButton("🔀  Generate", 0, 36);
            btnGenerate.Dock = DockStyle.Top;
            btnGenerate.Click += (s, e) => Regenerate();

            var generateHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = AppTheme.Background
            };
            generateHost.Controls.Add(btnGenerate);

            var lblResult = new Label
            {
                Text = "Generated username",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppTheme.Caption,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = ContentAlignment.BottomLeft
            };

            txtResult = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font(AppTheme.Base.FontFamily, 10.5f),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                ReadOnly = true
            };
            var btnCopy = DialogControls.CreateInlineActionButton("Copy", 56, 26);
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtResult.Text))
                {
                    try
                    {
                        Clipboard.SetText(txtResult.Text);
                        DialogControls.IndicateCopied(btnCopy);
                    }
                    catch
                    {
                        // Clipboard can fail if locked by another process.
                    }
                }
            };
            var resultRow = DialogControls.CreateBorderedFieldRow(txtResult, btnCopy);
            resultRow.Dock = DockStyle.Top;
            resultRow.Height = 36;

            btnUseInEntry = DialogControls.CreateSecondaryButton("Use in entry", 0, 34);
            btnUseInEntry.Dock = DockStyle.Top;
            btnUseInEntry.Enabled = false;
            btnUseInEntry.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtResult.Text))
                {
                    return;
                }

                SelectedUsername = txtResult.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };

            var useHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = AppTheme.Background
            };
            useHost.Controls.Add(btnUseInEntry);

            void SyncSeparatorVisibility()
            {
                separatorBlock.Visible = nudWords.Value > 1;
            }

            nudWords.ValueChanged += (s, e) =>
            {
                SyncSeparatorVisibility();
                Regenerate();
            };
            cmbSeparator.SelectedIndexChanged += (s, e) => Regenerate();
            cmbCapitalization.SelectedIndexChanged += (s, e) => Regenerate();
            chkIncludeNumbers.CheckedChanged += (s, e) => Regenerate();

            // Dock=Top reverse order
            content.Controls.Add(useHost);
            content.Controls.Add(resultRow);
            content.Controls.Add(lblResult);
            content.Controls.Add(generateHost);
            content.Controls.Add(chkIncludeNumbers);
            content.Controls.Add(optionsRow);
            content.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = AppTheme.Background });
            content.Controls.Add(lblSubtitle);
            content.Controls.Add(lblTitle);

            Controls.Add(content);

            SyncSeparatorVisibility();
            Regenerate();
        }

        private static ComboBox CreateDropDown(int width)
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = width,
                Font = AppTheme.Base,
                FlatStyle = FlatStyle.Flat
            };
        }

        private static Panel CreateLabeledControl(string label, Control field, int width)
        {
            var panel = new Panel
            {
                Width = width + 8,
                Height = 48,
                BackColor = AppTheme.Background,
                Margin = new Padding(0, 0, 8, 0)
            };
            var lbl = new Label
            {
                Text = label,
                AutoSize = false,
                Width = width,
                Height = 18,
                Font = AppTheme.Caption,
                ForeColor = AppTheme.TextSecondary,
                Location = new Point(0, 0)
            };
            field.Location = new Point(0, 20);
            field.Width = width;
            panel.Controls.Add(lbl);
            panel.Controls.Add(field);
            return panel;
        }

        private void Regenerate()
        {
            var separator = cmbSeparator.SelectedIndex switch
            {
                1 => UsernameGenerator.SeparatorKind.Underscore,
                2 => UsernameGenerator.SeparatorKind.Hyphen,
                3 => UsernameGenerator.SeparatorKind.None,
                _ => UsernameGenerator.SeparatorKind.Dot
            };
            var capitalization = cmbCapitalization.SelectedIndex switch
            {
                1 => UsernameGenerator.Capitalization.Capitalized,
                2 => UsernameGenerator.Capitalization.MixedCase,
                _ => UsernameGenerator.Capitalization.Lowercase
            };

            var options = new UsernameGenerator.Options
            {
                Style = UsernameGenerator.Style.Random,
                WordCount = (int)nudWords.Value,
                Separator = separator,
                Capitalization = capitalization,
                IncludeNumbers = chkIncludeNumbers.Checked
            };

            txtResult.Text = UsernameGenerator.Generate(options);
            btnUseInEntry.Enabled = !string.IsNullOrWhiteSpace(txtResult.Text);
        }
    }
}
