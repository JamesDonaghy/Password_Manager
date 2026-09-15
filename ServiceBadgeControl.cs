using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PasswordManager
{
    /// A badge for the details panel header: shows the website favicon when one is
    /// available for WebsiteUrl, otherwise the coloured service-initial badge. The entry
    /// list (AccountGridPresenter) paints the same way in CellPainting; both use
    /// FaviconCache / ServiceBadge so list and details stay in sync.
    public class ServiceBadgeControl : Panel
    {
        private string serviceName = string.Empty;
        private string websiteUrl = string.Empty;

        public string ServiceName
        {
            get => serviceName;
            set
            {
                serviceName = value ?? string.Empty;
                Invalidate();
            }
        }

        public string WebsiteUrl
        {
            get => websiteUrl;
            set
            {
                websiteUrl = value ?? string.Empty;
                Invalidate();
            }
        }

        public ServiceBadgeControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FaviconCache.IconLoaded += OnIconLoaded;
        }

        private void OnIconLoaded(string host)
        {
            string source = !string.IsNullOrWhiteSpace(websiteUrl) ? websiteUrl : serviceName;
            string key = FaviconCache.HostKeyFrom(source);
            if (key != null && string.Equals(key, host, StringComparison.OrdinalIgnoreCase))
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            string iconSource = !string.IsNullOrWhiteSpace(websiteUrl) ? websiteUrl : serviceName;
            Image favicon = FaviconCache.TryGet(iconSource);

            if (favicon != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var path = ServiceBadge.RoundedRect(bounds, Math.Max(4, Width / 5)))
                {
                    var oldClip = e.Graphics.Clip;
                    e.Graphics.SetClip(path);
                    e.Graphics.DrawImage(favicon, bounds);
                    e.Graphics.Clip = oldClip;
                }
                return;
            }

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