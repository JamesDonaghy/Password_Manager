using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace PasswordManager
{
    /// <summary>
    /// Owns how the account grid renders what it's given: filtering, sorting, column
    /// setup, password masking, date formatting, and stale-entry highlighting. Doesn't
    /// own the account list itself, or which passwords are currently revealed - those
    /// are supplied by the caller (MainForm) each time, since selection and reveal-state
    /// are interaction/menu concerns, not grid rendering.
    /// </summary>
    public class AccountGridPresenter
    {
        private readonly DataGridView grid;
        private readonly Func<Account, bool> isPasswordRevealed;

        // Which column is currently sorted, and in which direction. Null means unsorted
        // (insertion order). Cached alongside the last-seen accounts/search text so a
        // column header click can re-run the same filter+sort without MainForm needing
        // to supply them again.
        private string sortColumn;
        private bool sortAscending = true;
        private IEnumerable<Account> lastAccounts = Enumerable.Empty<Account>();
        private string lastSearchText = string.Empty;

        // Columns the user has chosen to hide via the header's "Configure Columns..." menu,
        // by underlying column name (i.e. the Account property name). Persists here across
        // refreshes since AutoGenerateColumns rebuilds the column objects from scratch every
        // time DataSource is reassigned - the grid itself has nowhere lasting to remember it.
        // Loaded from ColumnVisibilityStore up front so the choice also survives an app
        // restart, and saved back to it every time it changes (see ConfigureColumns_Click).
        private readonly HashSet<string> hiddenColumns = new HashSet<string>(ColumnVisibilityStore.LoadHiddenColumns());

        // Shown when right-clicking a column header, KeePass-style. Assigned directly to
        // each column's HeaderCell.ContextMenuStrip (rather than left as the grid's general
        // ContextMenuStrip) so it replaces the row context menu specifically over the header
        // row, without interfering with right-clicking an actual row.
        private readonly ContextMenuStrip headerContextMenu;

        public AccountGridPresenter(DataGridView grid, Func<Account, bool> isPasswordRevealed)
        {
            this.grid = grid;
            this.isPasswordRevealed = isPasswordRevealed;

            this.headerContextMenu = new ContextMenuStrip();
            this.headerContextMenu.Items.Add("Configure Columns...", null, ConfigureColumns_Click);

            // Masks the Password column's displayed text unless the row has been revealed,
            // and formats the Created/Modified date columns.
            grid.CellFormatting += Grid_CellFormatting;

            // Click a column header to sort by it. BindingList<T> (what the grid is bound
            // to) doesn't support sorting on its own, so this is handled manually.
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        }

        /// Rebuilds the grid's contents from the given accounts and search text, applying
        /// whatever sort is currently active. Call this whenever the account list changes
        /// or the search text changes - it re-caches both so a later column header click
        /// can refresh again without needing them re-supplied.
        public void Refresh(IEnumerable<Account> accounts, string searchText)
        {
            lastAccounts = accounts ?? Enumerable.Empty<Account>();
            lastSearchText = searchText ?? string.Empty;

            RefreshInternal();
        }

        private void RefreshInternal()
        {
            string filterText = lastSearchText.Trim();

            // Deliberately not searching Password - matching against plaintext passwords in
            // a search box isn't something a password manager should be doing, even locally.
            IEnumerable<Account> filtered = string.IsNullOrEmpty(filterText)
                ? lastAccounts
                : lastAccounts.Where(a =>
                    Contains(a.Service, filterText) ||
                    Contains(a.Username, filterText) ||
                    Contains(a.Url, filterText) ||
                    Contains(a.Notes, filterText));

            filtered = ApplySort(filtered);

            grid.DataSource = new BindingList<Account>(filtered.ToList());

            // Columns are regenerated whenever DataSource is reassigned (AutoGenerateColumns
            // is on), so all of these need reapplying every time, not just once at startup.
            foreach (DataGridViewColumn column in grid.Columns)
            {
                // Programmatic means DataGridView won't attempt its own automatic sorting -
                // which BindingList<T> doesn't support anyway - and instead leaves header
                // clicks entirely to Grid_ColumnHeaderMouseClick.
                column.SortMode = DataGridViewColumnSortMode.Programmatic;
                column.HeaderCell.SortGlyphDirection = column.Name == sortColumn
                    ? (sortAscending ? SortOrder.Ascending : SortOrder.Descending)
                    : SortOrder.None;

                // Re-apply whatever show/hide choice the user last made in Configure Columns.
                column.Visible = !hiddenColumns.Contains(column.Name);

                // Right-click a header for the Configure Columns menu, same as left-click is
                // sorting - assigned per-column since HeaderCell is recreated along with it.
                column.HeaderCell.ContextMenuStrip = headerContextMenu;
            }

            // Rows also get rebuilt every time DataSource changes, so re-highlight stale
            // entries here too. Selecting a row still shows the normal selection highlight
            // on top of this - that takes precedence, no conflict.
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is Account account && StaleEntryPolicy.IsStale(account))
                {
                    row.DefaultCellStyle.BackColor = StaleEntryPolicy.HighlightColor;

                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        cell.ToolTipText = StaleEntryPolicy.ExplanationText;
                    }
                }
            }
        }

        private IEnumerable<Account> ApplySort(IEnumerable<Account> source)
        {
            switch (sortColumn)
            {
                case nameof(Account.Service):
                    return Sort(source, a => a.Service, StringComparer.OrdinalIgnoreCase);
                case nameof(Account.Username):
                    return Sort(source, a => a.Username, StringComparer.OrdinalIgnoreCase);
                case nameof(Account.Url):
                    return Sort(source, a => a.Url, StringComparer.OrdinalIgnoreCase);
                case nameof(Account.Notes):
                    return Sort(source, a => a.Notes, StringComparer.OrdinalIgnoreCase);
                // Nulls (entries with no timestamp) sort first in ascending order via the
                // default DateTime? comparer - reasonable as "unknown/oldest" by default.
                case nameof(Account.CreatedAt):
                    return Sort(source, a => a.CreatedAt, Comparer<DateTime?>.Default);
                case nameof(Account.ModifiedAt):
                    return Sort(source, a => a.ModifiedAt, Comparer<DateTime?>.Default);
                default:
                    return source;
            }
        }

        private IEnumerable<Account> Sort<TKey>(IEnumerable<Account> source, Func<Account, TKey> keySelector, IComparer<TKey> comparer)
        {
            return sortAscending
                ? source.OrderBy(keySelector, comparer)
                : source.OrderByDescending(keySelector, comparer);
        }

        private void Grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return; // Right-click is handled by headerContextMenu (Configure Columns...) instead
            }

            string columnName = grid.Columns[e.ColumnIndex].Name;

            if (columnName == nameof(Account.Password))
            {
                return; // Deliberately not sortable, same reasoning as excluding it from search
            }

            if (sortColumn == columnName)
            {
                sortAscending = !sortAscending; // Clicking the same column again reverses direction
            }
            else
            {
                sortColumn = columnName;
                sortAscending = true;
            }

            RefreshInternal(); // Re-applies the (now-changed) sort using the last-seen accounts/search text
        }

        private void ConfigureColumns_Click(object sender, EventArgs e)
        {
            // Built from the grid's live columns (name + current header text + current
            // visibility) rather than a hardcoded list, so the dialog always matches
            // whatever's actually on the grid - nothing to keep in sync by hand.
            var columnStates = grid.Columns
                .Cast<DataGridViewColumn>()
                .Select(c => (Name: c.Name, Header: c.HeaderText, Visible: c.Visible))
                .ToList();

            using (var configForm = new ColumnConfigForm(columnStates))
            {
                if (configForm.ShowDialog(grid.FindForm()) != DialogResult.OK)
                {
                    return;
                }

                hiddenColumns.Clear();
                foreach (var entry in configForm.SelectedVisibility)
                {
                    if (!entry.Value)
                    {
                        hiddenColumns.Add(entry.Key);
                    }

                    // Apply immediately to the live columns rather than waiting for the next
                    // Refresh() - there's no reason to force a full filter+sort+rebind just
                    // to toggle visibility, and this keeps the current selection/scroll
                    // position intact. The string indexer returns null instead of throwing
                    // when a column by that name doesn't exist.
                    var liveColumn = grid.Columns[entry.Key];
                    if (liveColumn != null)
                    {
                        liveColumn.Visible = entry.Value;
                    }
                }

                ColumnVisibilityStore.SaveHiddenColumns(hiddenColumns);
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            string columnName = grid.Columns[e.ColumnIndex].Name;

            if (columnName == nameof(Account.Password))
            {
                if (!(grid.Rows[e.RowIndex].DataBoundItem is Account account) || isPasswordRevealed(account))
                {
                    return; // Either not a real data row, or this row has been revealed - show the real value
                }

                // Fixed-length mask regardless of the actual password's length, so the mask
                // itself doesn't leak how long the real password is.
                e.Value = "••••••••";
                e.FormattingApplied = true;
                return;
            }

            if (columnName == nameof(Account.CreatedAt) || columnName == nameof(Account.ModifiedAt))
            {
                // Null means this entry was saved before these fields existed - show a
                // placeholder rather than a misleading default date.
                e.Value = e.Value is DateTime dateValue ? dateValue.ToString("g") : "-";
                e.FormattingApplied = true;
            }
        }

        private static bool Contains(string value, string searchText)
        {
            return value != null && value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}