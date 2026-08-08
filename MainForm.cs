using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace PasswordManager
{
    public class MainForm : Form
    {
        private DataGridView dgvAccounts;
        private BindingList<Account> accounts; // Use BindingList for automatic updates
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem editEntryMenuItem;
        private ToolStripMenuItem deleteEntryMenuItem;

        public MainForm()
        {
            InitializeComponent();
            InitializeDataGridView();
        }

        private void InitializeComponent()
        {
            this.dgvAccounts = new DataGridView();
            this.contextMenu = new ContextMenuStrip();
            this.contextMenu.Items.Add("Add Entry", null, AddEntry_Click);
            this.editEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Edit Entry", null, EditEntry_Click);
            this.deleteEntryMenuItem = (ToolStripMenuItem)this.contextMenu.Items.Add("Delete Entry", null, DeleteEntry_Click);
            this.contextMenu.Opening += ContextMenu_Opening; // Enable/disable Edit and Delete based on whether a row is selected
            this.dgvAccounts.ContextMenuStrip = this.contextMenu;

            // Right-clicking a row doesn't select it by default in a DataGridView, so without
            // this, Edit/Delete could act on whatever row was last left-clicked instead of the
            // one the user just right-clicked.
            this.dgvAccounts.CellMouseDown += DgvAccounts_CellMouseDown;

            this.Controls.Add(this.dgvAccounts);
            this.Text = "Password Manager";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
        }

        private void InitializeDataGridView()
        {
            accounts = new BindingList<Account>();
            dgvAccounts.DataSource = accounts; // Set up DataGridView data binding
            dgvAccounts.Dock = DockStyle.Fill;
            dgvAccounts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

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

        private void AddEntry_Click(object sender, EventArgs e)
        {
            using (var addEntryForm = new AddEntryForm())
            {
                // Get the current cursor position
                var mousePos = Control.MousePosition;
                addEntryForm.StartPosition = FormStartPosition.Manual; // Set to manual
                addEntryForm.Location = new System.Drawing.Point(mousePos.X, mousePos.Y); // Position next to the cursor

                // Show the form as a dialog
                if (addEntryForm.ShowDialog() == DialogResult.OK)
                {
                    var account = new Account
                    {
                        Service = addEntryForm.Service,
                        Username = addEntryForm.Username,
                        Password = addEntryForm.Password,
                        Notes = addEntryForm.Notes
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
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show($"InvalidOperationException: {ex.Message}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding account: {ex.Message}");
                }
            }
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
            bool hasSelection = dgvAccounts.CurrentRow?.DataBoundItem is Account;
            editEntryMenuItem.Enabled = hasSelection;
            deleteEntryMenuItem.Enabled = hasSelection;
        }

        private void EditEntry_Click(object sender, EventArgs e)
        {
            if (!(dgvAccounts.CurrentRow?.DataBoundItem is Account selectedAccount))
            {
                return;
            }

            using (var editEntryForm = new AddEntryForm(selectedAccount))
            {
                var mousePos = Control.MousePosition;
                editEntryForm.StartPosition = FormStartPosition.Manual;
                editEntryForm.Location = new System.Drawing.Point(mousePos.X, mousePos.Y);

                if (editEntryForm.ShowDialog() == DialogResult.OK)
                {
                    selectedAccount.Service = editEntryForm.Service;
                    selectedAccount.Username = editEntryForm.Username;
                    selectedAccount.Password = editEntryForm.Password;
                    selectedAccount.Notes = editEntryForm.Notes;

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

            var confirmResult = MessageBox.Show(
                $"Delete the entry for '{selectedAccount.Service}'? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult == DialogResult.Yes)
            {
                accounts.Remove(selectedAccount);
            }
        }
    }
}