using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenEdAI.Services.ContentFiltering;

namespace OpenEdAI.API.Services
{
    /// <summary>
    /// Central gatekeeper for external links – runs host/path heuristics, per-type tests
    /// (YouTube-specific rules, MIME sniffing, etc.).  *Does NOT do fuzzy-snippet
    /// relevance;* that is now handled in AIDrivenContentSearchService.
    /// </summary>
    public static class LinkVet
    {
        private static ILogger? _logger;
        private static readonly DomainFilter _domainFilter = new();

        // Only needed for video-specific checks
        private static IYouTubeHeuristics? _ytHeuristics;

        /// <summary>Call once during app start-up.</summary>
        public static void Initialize(ILoggerFactory loggerFactory,
                                      IYouTubeHeuristics ytHeuristics)
        {
            _logger = loggerFactory.CreateLogger(nameof(LinkVet));
            _ytHeuristics = ytHeuristics ?? throw new ArgumentNullException(nameof(ytHeuristics));
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="url" /> passes *all* heuristics for the
        /// requested <paramref name="requestedType" /> in the context of
        /// <paramref name="lessonTopic" />.
        /// </summary>
        public static async Task<bool> IsAcceptableAsync(
            string url,
            string requestedType,
            string lessonTopic,
            HttpClient http,
            CancellationToken ct)
        {
            // ---------------------------------------------------------------------
            // 0.  Parse & validate
            // ---------------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(url) ||
                !Enum.TryParse(requestedType, true, out ContentType contentType))
            {
                return false;
            }

            // ---------------------------------------------------------------------
            // 1.  Domain / path heuristics
            // ---------------------------------------------------------------------
            if (!_domainFilter.IsAllowed(url, contentType))
                return false;

            // ---------------------------------------------------------------------
            // 2.  Type-specific checks
            // ---------------------------------------------------------------------
            if (contentType == ContentType.Video)
            {
                // Only video-specific heuristics remain here
                if (!await _ytHeuristics!.IsRelevantAsync(url, lessonTopic, ct))
                    return false;

                // Skip MIME sniffing for YouTube – it always serves HTML
                return true;
            }

            // (Article / Forum) – no snippet relevance here any more.
            // ---------------------------------------------------------------------
            // 3.  Optional lightweight MIME heuristic
            // ---------------------------------------------------------------------
            var mime = await GetMediaTypeAsync(http, url, ct);
            if (mime != null && !PassesMimeHeuristic(contentType, mime, url))
            {
                _logger?.LogInformation("Rejected – MIME mismatch ({Mime}) for {Url}", mime, url);
                return false;
            }

            return true;   // passed everything we check inside LinkVet
        }

        #region helpers
        private static bool PassesMimeHeuristic(ContentType type, string mime, string url)
        {
            return type switch
            {
                ContentType.Article => mime.Contains("html") || mime.Contains("pdf"),
                ContentType.Forum => mime.Contains("html"),
                _ => true
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
                _logger?.LogDebug(ex, "HEAD failed for {Url}", url);
            }

            try
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                return res.IsSuccessStatusCode ? res.Content.Headers.ContentType?.MediaType : null;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "GET failed for {Url}", url);
                return null;
            }
        }
        #endregion
    }
}
