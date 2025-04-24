namespace OpenEdAI.API.Services
{
    public class LinkVet
    {
        private static ILogger _logger;

        // Domain allow-/deny-list
        private static readonly string[] _deny =
        {
            // Common marketing or enroll-now pages
            "/programs/",
            "/enroll/",
            "/careers/",
            "/profile/",
            "/jobs",
            "?jid=",
            "/apply",
            "/admissions",
            "/certificate",
            ".social",
            "facebook.com",
            "twitter.com",
            "x.com",
            "instagram.com",
            "tubmlr.com"
        };

        // Trusted host allow list
        private static readonly HashSet<string> _videoHosts =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "youtube.com",
                "youtu.be",
                "vimeo.com",
                "dailymotion.com",
                "coursera.org",
                "edx.org",
                "khanacademy.org"
            };

        private static readonly HashSet<string> _articleHosts =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // high-quality long-form /blog domains
                "medium.com",
                "khanacademy.org",
                "freecodecamp.org",
                "developer.mozilla.org",
                "ocw.mit.edu",
                "openlearn.open.ac.uk",
                "saylor.org",
                "oercommons.org",
                "ted.com/ted-ed",
                "dev.to"
            };

        private static readonly HashSet<string> _forumHosts =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "stackoverflow.com",
                "quora.com",
                "reddit.com",
                "github.com"
            };

        public static void Initialize(ILoggerFactory factory) => _logger = factory.CreateLogger<LinkVet>();

        // Link vetting
        public static async Task<bool> IsAcceptableAsync(string url, string requestedType, HttpClient http, CancellationToken ct)
        {
            // Check if the url is well-formed
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            // Structural deny rules
            if (_deny.Any(d => uri.AbsolutePath.Contains(d, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var host = uri.Host;

            // Host allow list
            if (QuickHostAllow(uri, requestedType, host))
            {
                return true;
            }

            // Head request, fallback to GET if HEAD fails
            string? mediaType = await GetMediaTypeAsync(http, uri, ct);
            if (mediaType == null) return false;

            // Check if the URL passes the MIME type test
            return PassesMimeTest(requestedType, mediaType, uri);

        }

        // Check if the URL is a trusted host
        private static bool QuickHostAllow(Uri uri, string requestedType, string host) =>
            requestedType switch
            {
                "Video" => _videoHosts.Any(h => host.EndsWith(h, StringComparison.OrdinalIgnoreCase)),
                "Article" => _articleHosts.Any(h => host.EndsWith(h, StringComparison.OrdinalIgnoreCase)),
                "Forum" => _forumHosts.Any(h => host.EndsWith(h, StringComparison.OrdinalIgnoreCase)),
                _ => false
            };

        // Check if the URL passes the MIME type test
        private static bool PassesMimeTest(string requestedType, string ctHeader, Uri uri) =>
            requestedType switch
            {
                // Allowed extensions for video
                "Video" => ctHeader.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                            uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                            uri.AbsolutePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase),

                // Allowed extensions for article
                "Article" => ctHeader.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                            ctHeader.Contains("pdf", StringComparison.OrdinalIgnoreCase),

                // Allowed extensions for forum
                "Forum" => ctHeader.Contains("html", StringComparison.OrdinalIgnoreCase),

                // Default case
                _ => false
            };

        // Check the media type of the URL
        private static async Task<string?> GetMediaTypeAsync(HttpClient http, Uri uri, CancellationToken ct)
        {
            try
            {
                // Try HEAD request first
                using var head = new HttpRequestMessage(HttpMethod.Head, uri);
                using var res = await http.SendAsync(head,
                                      HttpCompletionOption.ResponseHeadersRead, ct);
                if (res.IsSuccessStatusCode)
                    return res.Content.Headers.ContentType?.MediaType;
            }
            catch (Exception)
            {
                _logger.LogWarning("HEAD request failed, falling back to GET");
            }

            // Some websites do not support HEAD requests, so fallback to GET
            try
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, uri);
                using var res = await http.SendAsync(get,
                                      HttpCompletionOption.ResponseHeadersRead, ct);

                return res.IsSuccessStatusCode
                    ? res.Content.Headers.ContentType?.MediaType
                    : null;
            }
            catch (Exception)
            {
                // Failed, so skip
                _logger.LogWarning("Error checking URL in LinkVet");
                return null;
            }

        }
    }
}
