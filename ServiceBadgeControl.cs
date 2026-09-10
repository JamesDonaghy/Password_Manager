using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PasswordManager
{
    /// A single coloured service-initial badge that repaints itself when ServiceName
    /// changes - used for the details panel header. The entry list (AccountGridPresenter)
    /// draws the same badge per-row directly via CellPainting instead of using this control,
    /// since a DataGridView cell isn't a child control; both pull their colour/initial from
    /// the shared ServiceBadge helper so they always agree.
    public class ServiceBadgeControl : Panel
    {
        private string serviceName = string.Empty;

        public string ServiceName
        {
            get => serviceName;
            set
            {
                serviceName = value ?? string.Empty;
                Invalidate();
            }
        }

        public ServiceBadgeControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var brush = new SolidBrush(ServiceBadge.ColorFor(serviceName)))
            using (var path = ServiceBadge.RoundedRect(bounds, Math.Max(4, Width / 5)))
            {
                e.Graphics.FillPath(brush, path);
            }

            using (var font = new Font(AppTheme.Base.FontFamily, Math.Max(8f, Height * 0.4f), FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, ServiceBadge.InitialFor(serviceName), font, bounds, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }
}