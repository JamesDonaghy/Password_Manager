using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PasswordManager
{
    /// <summary>
    /// Fetches and caches website favicons for entry badges. Uses Google's public favicon
    /// endpoint so we don't have to scrape each site. Icons are kept in memory for the
    /// process lifetime; missing/failed loads fall back to the letter badge as before.
    /// Failed hosts are remembered so we do not retry (and repaint) on every paint.
    /// </summary>
    public static class FaviconCache
    {
        private static readonly HttpClient Http = CreateClient();
        private static readonly ConcurrentDictionary<string, Image> Icons = new ConcurrentDictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> InFlight = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> Failed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// Fired on a UI thread when a favicon finishes loading successfully. Subscribe to
        /// invalidate the matching list rows / details badge so the new icon can paint.
        public static event Action<string> IconLoaded;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PasswordManager/1.0");
            return client;
        }

        /// Returns a host key suitable for caching, or null if nothing usable.
        public static string HostKeyFrom(string urlOrHost)
        {
            if (string.IsNullOrWhiteSpace(urlOrHost))
            {
                return null;
            }

            string trimmed = urlOrHost.Trim();
            if (!trimmed.Contains("://", StringComparison.Ordinal))
            {
                trimmed = "https://" + trimmed;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host))
            {
                return null;
            }

            // Strip leading "www." so youtube.com and www.youtube.com share one cache entry.
            string host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4)
            {
                host = host.Substring(4);
            }

            return host.ToLowerInvariant();
        }

        /// Returns a cached icon if already loaded; otherwise kicks off a background fetch
        /// (once) and returns null (caller should draw the letter-badge fallback).
        /// Returns null immediately when website icons are disabled in Settings.
        public static Image TryGet(string urlOrHost)
        {
            if (!AppPreferences.ShowWebsiteIcons)
            {
                return null;
            }

            string host = HostKeyFrom(urlOrHost);
            if (host == null)
            {
                return null;
            }

            if (Icons.TryGetValue(host, out Image icon))
            {
                return icon;
            }

            if (Failed.ContainsKey(host))
            {
                return null; // Already tried; stay on letter badge without re-fetching
            }

            EnsureLoading(host);
            return null;
        }

        private static void EnsureLoading(string host)
        {
            if (Failed.ContainsKey(host) || Icons.ContainsKey(host))
            {
                return;
            }

            if (!InFlight.TryAdd(host, 0))
            {
                return; // Already downloading
            }

            _ = Task.Run(async () =>
            {
                bool loaded = false;
                try
                {
                    // sz=64 gives a crisp icon for the 30–48px badges we draw.
                    string requestUrl = $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(host)}&sz=64";
                    using (HttpResponseMessage response = await Http.GetAsync(requestUrl).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Failed.TryAdd(host, 0);
                            return;
                        }

                        byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (bytes == null || bytes.Length == 0)
                        {
                            Failed.TryAdd(host, 0);
                            return;
                        }

                        Image image;
                        using (var stream = new System.IO.MemoryStream(bytes))
                        {
                            // Clone so we can dispose the stream and keep a GDI+ image that
                            // owns its own buffer (Bitmap from a stream is stream-lifetime-bound).
                            using (var temp = Image.FromStream(stream))
                            {
                                image = new Bitmap(temp);
                            }
                        }

                        Icons[host] = image;
                        loaded = true;
                    }
                }
                catch
                {
                    Failed.TryAdd(host, 0);
                }
                finally
                {
                    InFlight.TryRemove(host, out _);
                    // Only notify on success - failure notifications caused full-grid
                    // repaints with no visual change, which looked like constant flicker.
                    if (loaded)
                    {
                        RaiseIconLoaded(host);
                    }
                }
            });
        }

        private static void RaiseIconLoaded(string host)
        {
            Action<string> handler = IconLoaded;
            if (handler == null)
            {
                return;
            }

            // Paint must happen on the UI thread.
            Form anyForm = null;
            try
            {
                if (Application.OpenForms.Count > 0)
                {
                    anyForm = Application.OpenForms[0];
                }
            }
            catch
            {
                // OpenForms can throw if the app is shutting down.
            }

            if (anyForm != null && anyForm.IsHandleCreated && !anyForm.IsDisposed)
            {
                try
                {
                    anyForm.BeginInvoke(new Action(() => handler(host)));
                    return;
                }
                catch
                {
                    // Handle destroyed between check and invoke.
                }
            }

            handler(host);
        }
    }
}