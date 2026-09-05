using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PasswordManager
{
    // A small modal dialog listing the grid's columns with checkboxes, so the user can
    // choose which ones are shown - the same "right-click header > Configure Columns"
    // pattern KeePass uses. Doesn't touch the grid itself; AccountGridPresenter applies
    // whatever the user picks here.
    public class ColumnConfigForm : Form
    {
        private readonly CheckedListBox checkedListBox;

        // The column names (DataGridViewColumn.Name, i.e. the underlying Account property
        // name) in the same order as checkedListBox's items, so a checkbox index can be
        // mapped back to the column it belongs to.
        private readonly List<string> columnNames;

        /// After ShowDialog() returns DialogResult.OK, this holds the column name -> visible
        /// mapping the user chose. Null until then.
        public Dictionary<string, bool> SelectedVisibility { get; private set; }

        /// <param name="columns">Each column's underlying name, its displayed header text,
        /// and whether it's currently visible - in the order they should be listed.</param>
        public ColumnConfigForm(IEnumerable<(string Name, string Header, bool Visible)> columns)
        {
            this.Text = "Configure Columns";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.Base;
            this.Size = new Size(380, 380);

            var columnList = columns.ToList();
            columnNames = columnList.Select(c => c.Name).ToList();

            checkedListBox = new CheckedListBox
            {
                Font = AppTheme.Base,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false
            };

            foreach (var column in columnList)
            {
                checkedListBox.Items.Add(column.Header, column.Visible);
            }

            var btnOk = DialogControls.CreatePrimaryButton("OK", 90, 32);
            btnOk.Click += BtnOk_Click;

            var btnCancel = DialogControls.CreateSecondaryButton("Cancel", 90, 32);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            var lblFormTitle = new Label
            {
                Text = "Configure Columns",
                Dock = DockStyle.Top,
                Height = 36,
                Font = AppTheme.Heading,
                ForeColor = AppTheme.TextPrimary,
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(20, 0, 0, 6)
            };

            var listGroup = DialogControls.CreateFieldGroup("Choose the columns to show", checkedListBox, 180);

            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 2, 20, 2), BackColor = AppTheme.Background };
            contentPanel.Controls.Add(listGroup);

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(20, 8, 20, 12), BackColor = AppTheme.Background };
            btnCancel.Margin = new Padding(0, 3, 0, 3);
            btnOk.Margin = new Padding(10, 3, 0, 3);
            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnOk);

            this.Controls.Add(contentPanel);
            this.Controls.Add(actionPanel);
            this.Controls.Add(lblFormTitle);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BtnOk_Click(object sender, System.EventArgs e)
        {
            // Hiding every column would leave the grid with nothing to show and no way to
            // get a column back other than re-opening this same dialog - block it here
            // rather than let the user land in that state.
            if (checkedListBox.CheckedIndices.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "At least one column must stay visible.",
                    "Configure Columns",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var visibility = new Dictionary<string, bool>();
            for (int i = 0; i < columnNames.Count; i++)
            {
                visibility[columnNames[i]] = checkedListBox.GetItemChecked(i);
            }

            SelectedVisibility = visibility;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}