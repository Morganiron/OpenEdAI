using OpenEdAI.Services.ContentFiltering;

namespace OpenEdAI.API.Services
{
    /// <summary>
    /// LinkVet evaluates URLs to determine if they are acceptable for lesson use.
    /// </summary>
    public class LinkVet
    {
        private static ILogger _logger;
        private static readonly DomainFilter _domainFilter = new();

        public static void Initialize(ILoggerFactory factory) => _logger = factory.CreateLogger<LinkVet>();

        /// <summary>
        /// Evaluates whether a given URL is valid and relevant for the requested content type.
        /// </summary>
        public static async Task<bool> IsAcceptableAsync(string url, string requestedType, HttpClient http, CancellationToken ct)
        {
            if (!Enum.TryParse(requestedType, ignoreCase: true, out ContentType contentType))
                return false;

            // First apply domain/path filtering
            bool isPreferredDomain = _domainFilter.IsAllowed(url, contentType);
            if (!isPreferredDomain)
                return false;

            // Attempt to get MIME type (optional)
            string? mediaType = await GetMediaTypeAsync(http, url, ct);

            // If we get a type, validate it
            if (mediaType != null)
                return PassesMimeHeuristic(contentType, mediaType, url);

            // Fallback: allow links from preferred domains if MIME is unavailable
            _logger?.LogInformation("Link allowed from preferred domain despite unknown media type: {Url}", url);
            return true;
        }

        /// <summary>
        /// MIME type and extension check per content type.
        /// </summary>
        private static bool PassesMimeHeuristic(ContentType type, string mime, string url)
        {
            var ext = url.ToLowerInvariant();

            return type switch
            {
                ContentType.Video => mime.StartsWith("video/") || ext.EndsWith(".mp4") || ext.EndsWith(".webm"),
                ContentType.Article => mime.Contains("html") || mime.Contains("pdf"),
                ContentType.Forum => mime.Contains("html"),
                _ => false
            };
        }

        /// <summary>
        /// Tries HEAD then falls back to GET to determine media type.
        /// </summary>
        private static async Task<string?> GetMediaTypeAsync(HttpClient http, string url, CancellationToken ct)
        {
            try
            {
                using var head = new HttpRequestMessage(HttpMethod.Head, url);
                using var res = await http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
                if (res.IsSuccessStatusCode)
                    return res.Content.Headers.ContentType?.MediaType;
            }
            catch (Exception)
            {
                _logger?.LogWarning("HEAD request failed, falling back to GET: {Url}", url);
            }

            try
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                return res.IsSuccessStatusCode ? res.Content.Headers.ContentType?.MediaType : null;
            }
            catch (Exception)
            {
                _logger?.LogWarning("GET request failed for URL: {Url}", url);
                return null;
            }
        }
    }
}
