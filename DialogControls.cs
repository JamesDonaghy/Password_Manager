using System;
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

        /// Wraps a value control (and optional trailing action buttons, e.g. Copy/Show)
        /// in a bordered, white "field box". Single-line TextBox/ComboBox inputs are
        /// vertically centered so the caret is not stuck at the top of a taller row.
        public static Panel CreateBorderedFieldRow(Control valueControl, params Control[] trailingButtons)
        {
            var border = new Panel { BackColor = AppTheme.Border, Padding = new Padding(1) };

            // Only drop the right padding when there's a trailing button to sit flush
            // against the edge - a field with no button still needs breathing room on
            // that side for the text itself.
            int rightPadding = trailingButtons.Length > 0 ? 0 : 12;
            var inset = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(12, 0, rightPadding, 0) };

            bool isMultiline = valueControl is TextBox multi && multi.Multiline;

            if (isMultiline)
            {
                valueControl.Dock = DockStyle.Fill;
                inset.Controls.Add(valueControl);
            }
            else
            {
                // Don't Dock=Fill a single-line input: WinForms draws the caret/text at the
                // top of the client area, which looks high inside a 34–36px bordered row.
                valueControl.Dock = DockStyle.None;
                valueControl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                inset.Controls.Add(valueControl);

                void CenterValue()
                {
                    int trailingWidth = 0;
                    foreach (Control button in trailingButtons)
                    {
                        trailingWidth += button.Width;
                    }

                    int height = valueControl.PreferredSize.Height;
                    if (valueControl is TextBox)
                    {
                        height = Math.Max(height, valueControl.Font.Height + 6);
                    }
                    else if (valueControl is ComboBox combo)
                    {
                        height = Math.Max(height, combo.PreferredHeight);
                    }

                    // SetBounds ignores Panel.Padding (unlike Dock), so apply it manually —
                    // otherwise single-line text sits flush against the left border.
                    int left = inset.Padding.Left;
                    int availableHeight = inset.ClientSize.Height - inset.Padding.Top - inset.Padding.Bottom;
                    int y = inset.Padding.Top + Math.Max(0, (availableHeight - height) / 2);
                    int width = Math.Max(0, inset.ClientSize.Width - left - inset.Padding.Right - trailingWidth);
                    valueControl.SetBounds(left, y, width, height);
                }

                inset.Layout += (s, e) => CenterValue();
                inset.Resize += (s, e) => CenterValue();
            }

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