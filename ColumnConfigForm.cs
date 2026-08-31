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

            var font = SystemFonts.MessageBoxFont;
            var columnList = columns.ToList();
            columnNames = columnList.Select(c => c.Name).ToList();

            var lblInstructions = new Label
            {
                Text = "Choose the columns to show in the main window:",
                Font = font,
                AutoSize = true,
                Location = new Point(12, 12)
            };

            checkedListBox = new CheckedListBox
            {
                Font = font,
                Location = new Point(12, 36),
                Size = new Size(260, 130),
                CheckOnClick = true,
                IntegralHeight = false
            };

            foreach (var column in columnList)
            {
                checkedListBox.Items.Add(column.Header, column.Visible);
            }

            int buttonsTop = checkedListBox.Bottom + 15;

            var btnOk = new Button
            {
                Text = "OK",
                Width = 90,
                Height = 28,
                Font = font,
                Location = new Point(102, buttonsTop)
            };
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 28,
                Font = font,
                Location = new Point(197, buttonsTop)
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblInstructions);
            this.Controls.Add(checkedListBox);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ClientSize = new Size(284, buttonsTop + 28 + 12);
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