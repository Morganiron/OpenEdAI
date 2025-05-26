using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenEdAI.Services.ContentFiltering;

namespace OpenEdAI.API.Services
{
    /// <summary>
    /// Central coordinator that decides whether an external link is suitable
    /// for use as a lesson resource.
    /// </summary>
    public static class LinkVet
    {
        private static ILogger? _logger;
        private static readonly DomainFilter _domainFilter = new();

        private static ContentRelevanceChecker? _relevanceChecker;
        private static IYouTubeHeuristics? _ytHeuristics;

        /// <summary>
        /// Call once on application start-up (see <c>Program.cs</c>).
        /// </summary>
        public static void Initialize(
            ILoggerFactory loggerFactory,
            ContentRelevanceChecker relevanceChecker,
            IYouTubeHeuristics ytHeuristics)
        {
            _logger = loggerFactory.CreateLogger(nameof(LinkVet));
            _relevanceChecker = relevanceChecker ?? throw new ArgumentNullException(nameof(relevanceChecker));
            _ytHeuristics = ytHeuristics ?? throw new ArgumentNullException(nameof(ytHeuristics));
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="url"/> passes all heuristics for the requested
        /// <paramref name="requestedType"/> in the context of <paramref name="lessonTopic"/>.
        /// </summary>
        public static async Task<bool> IsAcceptableAsync(
            string url,
            string requestedType,
            string lessonTopic,
            HttpClient http,
            CancellationToken ct)
        {
            // -------------------------------------------------------------------------
            // 0. Validate arguments / parse content-type
            // -------------------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(url) ||
                !Enum.TryParse(requestedType, ignoreCase: true, out ContentType contentType))
            {
                return false;
            }

            // -------------------------------------------------------------------------
            // 1. Domain / path filtering
            // -------------------------------------------------------------------------
            if (!_domainFilter.IsAllowed(url, contentType))
                return false;

            // -------------------------------------------------------------------------
            // 2. Type-specific checks
            // -------------------------------------------------------------------------
            if (contentType == ContentType.Video)
            {
                // YouTube-specific heuristics (duration, captions, fuzzy topic)
                if (!await _ytHeuristics!.IsRelevantAsync(url, lessonTopic, ct))
                    return false;

                // For videos we skip MIME heuristics – YouTube always serves HTML
                return true;
            }

            // For Articles / Forums → text-based relevance check
            if (!await _relevanceChecker!.IsRelevantAsync(url, lessonTopic, ct))
            {
                _logger?.LogInformation("Rejected – low relevance. URL: {Url}", url);
                return false;
            }

            // -------------------------------------------------------------------------
            // 3. Optional MIME heuristics (HEAD→GET fallback)
            // -------------------------------------------------------------------------
            var mediaType = await GetMediaTypeAsync(http, url, ct);
            if (mediaType != null && !PassesMimeHeuristic(contentType, mediaType, url))
            {
                _logger?.LogInformation("Rejected – MIME mismatch ({Mime}) for {Url}", mediaType, url);
                return false;
            }

            // Unknown MIME is tolerated for preferred domains
            return true;
        }

        #region helpers --------------------------------------------------------------

        private static bool PassesMimeHeuristic(ContentType type, string mime, string url)
        {
            var lower = url.ToLowerInvariant();
            return type switch
            {
                ContentType.Article => mime.Contains("html") || mime.Contains("pdf"),
                ContentType.Forum => mime.Contains("html"),
                _ => false
            };
        }

        private static async Task<string?> GetMediaTypeAsync(HttpClient http, string url, CancellationToken ct)
        {
            try
            {
                using var head = new HttpRequestMessage(HttpMethod.Head, url);
                using var res = await http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
                if (res.IsSuccessStatusCode)
                    return res.Content.Headers.ContentType?.MediaType;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "HEAD failed – falling back to GET for {Url}", url);
            }

            try
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                return res.IsSuccessStatusCode ? res.Content.Headers.ContentType?.MediaType : null;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "GET failed – MIME unknown for {Url}", url);
                return null;
            }
        }

        #endregion
    }
}
