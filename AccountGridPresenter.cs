using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace PasswordManager
{
    /// <summary>
    /// Owns how the account list renders what it's given: filtering and a custom-painted
    /// "card" row (service icon badge, service name, username underneath, favourite/chevron
    /// placeholders) instead of the old plain data table. Doesn't own the account list
    /// itself - that's supplied by the caller (MainForm) each time via Refresh().
    ///
    /// Column-header sorting and per-column show/hide (Configure Columns) were removed
    /// along with the table layout they belonged to - there's only one merged display
    /// column now, so neither had anywhere left to attach. See ColumnConfigForm's removal
    /// in the same commit as this file's rewrite.
    /// </summary>
    public class AccountGridPresenter
    {
        private readonly DataGridView grid;
        private IEnumerable<Account> lastAccounts = Enumerable.Empty<Account>();
        private string lastSearchText = string.Empty;

        // Which row the mouse is currently over, for the hover highlight - DataGridView
        // has no built-in concept of row hover, so this is tracked manually and the
        // affected rows are invalidated (repainted) on change.
        private int hoveredRowIndex = -1;

        public AccountGridPresenter(DataGridView grid)
        {
            this.grid = grid;

            // A single column spanning the whole grid, entirely custom-painted (see
            // Grid_CellPainting) rather than showing its bound value as text - there's no
            // per-field column structure anymore now that the list is a merged
            // service+username card rather than a table. AutoGenerateColumns off means
            // this column is created once here and persists across DataSource reassignments
            // in Refresh(), rather than being torn down and rebuilt every time.
            grid.AutoGenerateColumns = false;
            grid.ColumnHeadersVisible = false;
            var entryColumn = new DataGridViewTextBoxColumn
            {
                Name = nameof(Account.Service),
                DataPropertyName = nameof(Account.Service),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
            grid.Columns.Add(entryColumn);

            grid.CellPainting += Grid_CellPainting;
            grid.CellMouseMove += Grid_CellMouseMove;
            grid.MouseLeave += Grid_MouseLeave;
        }

        /// Rebuilds the list's contents from the given accounts and search text. Call this
        /// whenever the account list changes or the search text changes.
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

            grid.DataSource = new BindingList<Account>(filtered.ToList());
            hoveredRowIndex = -1; // Row indices are meaningless after a full rebind

            // Rows are rebuilt every time DataSource changes, so re-highlight stale entries
            // here too. Grid_CellPainting reads this back as the row's base background
            // colour - selecting/hovering a row still takes precedence on top of it.
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is Account account && StaleEntryPolicy.IsStale(account))
                {
                    row.DefaultCellStyle.BackColor = StaleEntryPolicy.HighlightColor;
                    row.Cells[0].ToolTipText = StaleEntryPolicy.ExplanationText;
                }
            }
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(grid.Rows[e.RowIndex].DataBoundItem is Account account))
            {
                return; // Nothing bound yet, or not a real data row
            }

            bool isSelected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
            bool isHovered = e.RowIndex == hoveredRowIndex && !isSelected;

            Color background = isSelected
                ? AppTheme.AccentSubtle
                : isHovered
                    ? AppTheme.PanelBackground
                    : e.CellStyle.BackColor; // Respects the stale-entry highlight set above, or the grid's default fill otherwise

            using (var backgroundBrush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(backgroundBrush, e.CellBounds);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Service-initial badge, standing in for a real per-service icon.
            const int badgeSize = 30;
            var badgeRect = new Rectangle(
                e.CellBounds.Left + 12,
                e.CellBounds.Top + (e.CellBounds.Height - badgeSize) / 2,
                badgeSize,
                badgeSize);

            Color badgeColor = ServiceBadge.ColorFor(account.Service);
            using (var badgeBrush = new SolidBrush(badgeColor))
            using (var badgePath = ServiceBadge.RoundedRect(badgeRect, 7))
            {
                e.Graphics.FillPath(badgeBrush, badgePath);
            }

            string initial = ServiceBadge.InitialFor(account.Service);
            using (var badgeFont = new Font(AppTheme.Base.FontFamily, 11f, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, initial, badgeFont, badgeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // Service name (top line) and username (bottom line), each ellipsised
            // independently rather than manually measured - TextRenderer handles that.
            int textLeft = badgeRect.Right + 10;
            int textRight = e.CellBounds.Right - 56; // Leaves room for the star/chevron on the right
            int textWidth = Math.Max(0, textRight - textLeft);
            int lineHeight = (e.CellBounds.Height - 6) / 2;
            var serviceRect = new Rectangle(textLeft, e.CellBounds.Top + 3, textWidth, lineHeight);
            var usernameRect = new Rectangle(textLeft, serviceRect.Bottom, textWidth, lineHeight);

            string serviceText = string.IsNullOrEmpty(account.Service) ? "(no service name)" : account.Service;
            const TextFormatFlags textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            using (var serviceFont = new Font(AppTheme.Base.FontFamily, 9.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, serviceText, serviceFont, serviceRect, AppTheme.TextPrimary, textFlags);
            }

            TextRenderer.DrawText(e.Graphics, account.Username ?? string.Empty, AppTheme.Base, usernameRect, AppTheme.TextSecondary, textFlags);

            // Favourite star and a trailing chevron, matching the reference design's list-
            // item structure. Both are purely decorative for now - there's no Favourites
            // feature yet to back the star (a later stage, once the core list layout and
            // details panel are both settled).
            using (var glyphFont = new Font(AppTheme.Base.FontFamily, 12f))
            {
                var starRect = new Rectangle(e.CellBounds.Right - 52, e.CellBounds.Top, 24, e.CellBounds.Height);
                TextRenderer.DrawText(e.Graphics, "☆", glyphFont, starRect, AppTheme.Border,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                var chevronRect = new Rectangle(e.CellBounds.Right - 26, e.CellBounds.Top, 20, e.CellBounds.Height);
                TextRenderer.DrawText(e.Graphics, "›", glyphFont, chevronRect, AppTheme.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // Thin divider between entries, replacing the grid's own default gridlines
            // (disabled - see MainForm's CellBorderStyle setting) for full control over it.
            using (var dividerPen = new Pen(AppTheme.Border))
            {
                e.Graphics.DrawLine(dividerPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            e.Handled = true;
        }

        private void Grid_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex == hoveredRowIndex)
            {
                return;
            }

            int previousHoveredRow = hoveredRowIndex;
            hoveredRowIndex = e.RowIndex;

            if (previousHoveredRow >= 0 && previousHoveredRow < grid.Rows.Count)
            {
                grid.InvalidateRow(previousHoveredRow);
            }

            if (hoveredRowIndex >= 0)
            {
                grid.InvalidateRow(hoveredRowIndex);
            }
        }

        private void Grid_MouseLeave(object sender, EventArgs e)
        {
            if (hoveredRowIndex < 0)
            {
                return;
            }

            int previousHoveredRow = hoveredRowIndex;
            hoveredRowIndex = -1;
            grid.InvalidateRow(previousHoveredRow);
        }

        private static bool Contains(string value, string searchText)
        {
            return value != null && value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}