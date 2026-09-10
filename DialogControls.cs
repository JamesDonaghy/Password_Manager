using System.Windows.Forms;

namespace PasswordManager
{
    /// Shared building blocks for the app's dialog forms, extracted from AddEntryForm (the
    /// first form built with these patterns) so later dialogs reuse the same field/button
    /// look instead of each inventing their own - per the project's "establish a design
    /// decision once, reuse it everywhere after" convention.
    public static class DialogControls
    {
        /// Builds one "label above field" group for a dialog's stacked layout - a caption
        /// above the value, matching the reference apps' field style. fieldRow can be a bare
        /// input (a single-field row) or a small Panel containing an input plus inline
        /// buttons (e.g. AddEntryForm's password row with its generate/show/etc. icons).
        public static Panel CreateFieldGroup(string labelText, Control fieldRow, int fieldHeight)
        {
            const int labelHeight = 18;
            const int groupBottomMargin = 10;

            var wrapper = new Panel
            {
                Dock = DockStyle.Top,
                Height = labelHeight + fieldHeight + groupBottomMargin,
                Padding = new Padding(0, 0, 0, groupBottomMargin),
                BackColor = AppTheme.Background
            };

            var label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Top,
                Height = labelHeight,
                Font = AppTheme.Caption,
                ForeColor = AppTheme.TextSecondary,
                TextAlign = System.Drawing.ContentAlignment.BottomLeft
            };

            fieldRow.Dock = DockStyle.Top;
            fieldRow.Height = fieldHeight;

            // Dock=Top siblings stack in reverse of add order (last added ends up closest to
            // the top edge) - same quirk called out in MainForm - so the field goes in first.
            wrapper.Controls.Add(fieldRow);
            wrapper.Controls.Add(label);

            return wrapper;
        }

        /// Builds one small flat icon button (Show/Generate/Manage/etc.), styled consistently
        /// with MainForm's nav buttons - flat, borderless, accent-tinted hover.
        public static Button CreateIconButton(string icon, int size)
        {
            var button = new Button
            {
                Text = icon,
                Dock = DockStyle.Right,
                Width = size,
                Height = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.PanelBackground,
                Font = new System.Drawing.Font(AppTheme.Base.FontFamily, 11f),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AppTheme.AccentSubtle;
            button.FlatAppearance.MouseDownBackColor = AppTheme.AccentSubtle;
            return button;
        }

        /// Solid accent-filled button for a dialog's primary action (Save, Change Password,
        /// etc.) - first established on AddEntryForm's Save button.
        public static Button CreatePrimaryButton(string text, int width, int height)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Accent,
                ForeColor = System.Drawing.Color.White,
                Font = AppTheme.Base,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
            button.FlatAppearance.MouseDownBackColor = AppTheme.AccentHover;
            return button;
        }

        /// Outlined button for a dialog's secondary action (Cancel, etc.) - first established
        /// on AddEntryForm's Cancel button.
        public static Button CreateSecondaryButton(string text, int width, int height)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.Base,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = AppTheme.Border;
            button.FlatAppearance.MouseOverBackColor = AppTheme.AccentSubtle;
            button.FlatAppearance.MouseDownBackColor = AppTheme.AccentSubtle;
            return button;
        }

        /// Solid red button for a dialog's destructive confirm action (e.g. "Delete" in
        /// ConfirmationDialog) - deliberately distinct from CreatePrimaryButton's accent
        /// fill, so a destructive action never reads as the same "normal" action as Save.
        public static Button CreateDangerButton(string text, int width, int height)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Danger,
                ForeColor = System.Drawing.Color.White,
                Font = AppTheme.Base,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AppTheme.DangerHover;
            button.FlatAppearance.MouseDownBackColor = AppTheme.DangerHover;
            return button;
        }

        /// Small flat, borderless button for an inline text action next to a value (e.g.
        /// "Copy"/"Show" beside a details-panel field) - too minor an action to warrant a
        /// filled primary/secondary button, but still themed and hover-tinted rather than
        /// a plain default Button. Accent-coloured text signals it's actionable, similar to
        /// a text link.
        public static Button CreateInlineActionButton(string text, int width, int height)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.PanelBackground,
                ForeColor = AppTheme.Accent,
                Font = AppTheme.Base,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AppTheme.AccentSubtle;
            button.FlatAppearance.MouseDownBackColor = AppTheme.AccentSubtle;
            return button;
        }

        /// Wraps a read-only value control (and optional trailing action buttons, e.g.
        /// Copy/Show/Open) in a bordered, white "field box" - the same hairline-border-plus-
        /// white-inset look already established for the search box and AddEntryForm's
        /// inputs, applied here to read-only details-panel fields instead of editable ones.
        public static Panel CreateBorderedFieldRow(Control valueControl, params Control[] trailingButtons)
        {
            var border = new Panel { BackColor = AppTheme.Border, Padding = new Padding(1) };

            // Only drop the right padding when there's a trailing button to sit flush
            // against the edge - a field with no button (just Notes, currently) still
            // needs its own breathing room on that side for the text itself.
            int rightPadding = trailingButtons.Length > 0 ? 0 : 10;
            var inset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(10, 0, rightPadding, 0) };

            valueControl.Dock = DockStyle.Fill;
            inset.Controls.Add(valueControl);

            // Right-docked siblings render in the order added (unlike Top/Left, where it's
            // reversed) - the last one added lands flush against the true right edge - so
            // adding left-to-right in reading order here places trailing buttons correctly.
            foreach (Control button in trailingButtons)
            {
                button.Dock = DockStyle.Right;
                inset.Controls.Add(button);
            }

            border.Controls.Add(inset);
            return border;
        }
    }
}