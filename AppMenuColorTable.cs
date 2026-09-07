using System.Drawing;
using System.Windows.Forms;

namespace PasswordManager
{
    /// Colour table for the top menu bar and the grid's right-click context menu, so their
    /// hover/selected state uses the app's accent tint instead of Windows' default blue.
    /// ToolStripProfessionalRenderer reads these to paint menus - overriding just the colours
    /// (rather than a fully custom ToolStripRenderer) keeps native menu behaviour (keyboard
    /// navigation, submenus, etc.) intact and only changes how it's painted.
    public class AppMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => AppTheme.AccentSubtle;
        public override Color MenuItemSelectedGradientBegin => AppTheme.AccentSubtle;
        public override Color MenuItemSelectedGradientEnd => AppTheme.AccentSubtle;
        public override Color MenuItemPressedGradientBegin => AppTheme.AccentSubtle;
        public override Color MenuItemPressedGradientEnd => AppTheme.AccentSubtle;
        public override Color MenuItemBorder => AppTheme.Accent;
        public override Color MenuBorder => AppTheme.Border;
        public override Color ToolStripBorder => AppTheme.Border;

        public override Color ToolStripDropDownBackground => AppTheme.Surface;
        public override Color ImageMarginGradientBegin => AppTheme.Surface;
        public override Color ImageMarginGradientMiddle => AppTheme.Surface;
        public override Color ImageMarginGradientEnd => AppTheme.Surface;

        public override Color MenuStripGradientBegin => AppTheme.PanelBackground;
        public override Color MenuStripGradientEnd => AppTheme.PanelBackground;

        public override Color SeparatorDark => AppTheme.Border;
        public override Color SeparatorLight => AppTheme.Border;
    }
}