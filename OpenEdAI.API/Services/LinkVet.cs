using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenEdAI.Services.ContentFiltering;

namespace OpenEdAI.API.Services
{
    /// <summary>
    /// Central coordinator that decides whether a link is suitable for use as a lesson resource.
    /// </summary>
    public static class LinkVet
    {
        private static ILogger? _logger;                     // Set via <see cref="Initialize"/>
        private static readonly DomainFilter _domainFilter = new();   // Pure-function helper – OK to keep static/readonly
        private static ContentRelevanceChecker? _relevanceChecker; // Injected at runtime via <see cref="Initialize"/>

        /// <summary>
        /// Must be called once during app start-up (see <c>Program.cs</c>).
        /// </summary>
        public static void Initialize(ILoggerFactory loggerFactory,
                              ContentRelevanceChecker relevanceChecker)
        {
            // create a logger named “LinkVet”
            _logger = loggerFactory.CreateLogger(nameof(LinkVet));
            _relevanceChecker = relevanceChecker ?? throw new ArgumentNullException(nameof(relevanceChecker));
        }

        /// <summary>
        /// Determines whether <paramref name="url"/> should be accepted for a given lesson.
        /// </summary>
        /// <param name="url">Candidate URL.</param>
        /// <param name="requestedType">"Video", "Article", or "Forum" (case-insensitive).</param>
        /// <param name="lessonTopic">Lesson topic used for relevance checks.</param>
        /// <param name="http">Shared <see cref="HttpClient"/>.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task<bool> IsAcceptableAsync(
            string url,
            string requestedType,
            string lessonTopic,
            HttpClient http,
            CancellationToken ct)
        {
            // Validate & parse the content type
            if (!Enum.TryParse(requestedType, ignoreCase: true, out ContentType contentType))
                return false;

            // 1) Domain/path rules -----------------------------------------------------------
            if (!_domainFilter.IsAllowed(url, contentType))
                return false;

            // 2) Content-based relevance (skip for pure video – handled by YouTube heuristics)
            if (contentType != ContentType.Video)
            {
                var relevant = await _relevanceChecker!.IsRelevantAsync(url, lessonTopic, ct);
                if (!relevant)
                {
                    _logger?.LogInformation("Rejected – low relevance. URL: {Url}", url);
                    return false;
                }
            }

            // 3) Optional MIME heuristics ---------------------------------------------------
            var mediaType = await GetMediaTypeAsync(http, url, ct);
            if (mediaType != null && !PassesMimeHeuristic(contentType, mediaType, url))
            {
                _logger?.LogInformation("Rejected – MIME mismatch ({Mime}) for {Url}", mediaType, url);
                return false;
            }

            // Allow preferred-domain links even when MIME is unknown
            return true;
        }

        #region Helper methods
        private static bool PassesMimeHeuristic(ContentType type, string mime, string url)
        {
            var lowerUrl = url.ToLowerInvariant();
            return type switch
            {
                ContentType.Video => mime.StartsWith("video/") || lowerUrl.EndsWith(".mp4") || lowerUrl.EndsWith(".webm"),
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
                _logger?.LogDebug(ex, "HEAD failed – fallback to GET for {Url}", url);
            }

            try
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                return res.IsSuccessStatusCode ? res.Content.Headers.ContentType?.MediaType : null;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "GET failed – treating MIME as unknown for {Url}", url);
                return null;
            }
        }
        #endregion
    }
}