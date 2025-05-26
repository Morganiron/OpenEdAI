using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using FuzzySharp;

namespace OpenEdAI.Services.ContentFiltering
{
    /// <summary>
    /// Fetches HTML snippets and evaluates their relevance to a lesson topic using fuzzy matching.
    /// </summary>
    public sealed class ContentRelevanceChecker
    {
        private readonly HttpClient _http;
        private readonly ILogger<ContentRelevanceChecker> _logger;

        // Maximum number of bytes to read from the response stream
        private const int SnippetSize = 32 * 1024; // 32 KB

        // Minimum fuzzy-match score (0-100) considered relevant
        private const int RelevanceThreshold = 70;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentRelevanceChecker"/>.
        /// </summary>
        /// <param name="http">HttpClient for fetching content.</param>
        /// <param name="logger">Logger for diagnostic messages.</param>
        public ContentRelevanceChecker(HttpClient http, ILogger<ContentRelevanceChecker> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Fetches the first <see cref="SnippetSize"/> bytes of HTML from the specified URL.
        /// </summary>
        /// <param name="url">The URL of the page to fetch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Snippet of HTML or null if failed.</returns>
        public async Task<string?> FetchSnippetAsync(string url, CancellationToken ct)
        {
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch snippet, HTTP {Status} for URL {Url}", response.StatusCode, url);
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[SnippetSize];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                return Encoding.UTF8.GetString(buffer, 0, read);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception fetching snippet from URL {Url}", url);
                return null;
            }
        }

        /// <summary>
        /// Determines if the page at <paramref name="url"/> is relevant to <paramref name="lessonTopic"/>.
        /// </summary>
        /// <param name="url">Page URL.</param>
        /// <param name="lessonTopic">Lesson topic to compare against.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if relevance score meets threshold; otherwise false.</returns>
        public async Task<bool> IsRelevantAsync(string url, string lessonTopic, CancellationToken ct)
        {
            var snippet = await FetchSnippetAsync(url, ct);
            if (string.IsNullOrWhiteSpace(snippet))
            {
                _logger.LogInformation("Empty or missing snippet for URL {Url}", url);
                return false;
            }

            // Extract key text elements from the snippet
            var text = ExtractText(snippet);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogInformation("No extractable text for URL {Url}", url);
                return false;
            }

            // Compute fuzzy-match score
            int score = Fuzz.TokenSetRatio(lessonTopic, text);
            _logger.LogInformation("Relevance score {Score} for URL {Url}", score, url);

            return score >= RelevanceThreshold;
        }

        /// <summary>
        /// Parses HTML to extract title, meta description, first H1 and P.
        /// </summary>
        private static string ExtractText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var sb = new StringBuilder();

            // Title tag
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
            if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);

            // Meta description
            var meta = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", null);
            if (!string.IsNullOrWhiteSpace(meta)) sb.AppendLine(meta);

            // First heading
            var h1 = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText;
            if (!string.IsNullOrWhiteSpace(h1)) sb.AppendLine(h1);

            // First paragraph
            var p = doc.DocumentNode.SelectSingleNode("//p")?.InnerText;
            if (!string.IsNullOrWhiteSpace(p)) sb.AppendLine(p);

            return sb.ToString();
        }
    }
}
