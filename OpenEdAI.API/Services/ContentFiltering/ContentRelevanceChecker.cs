using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FuzzySharp;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace OpenEdAI.Services.ContentFiltering
{
    /// <summary>
    /// Fuzzy-snippet relevance checker for **non-video** links
    /// (articles, forums, PDFs, etc.).
    /// </summary>
    public sealed class ContentRelevanceChecker
    {
        private readonly HttpClient _http;
        private readonly ILogger<ContentRelevanceChecker> _logger;

        private const int SnippetSize = 32 * 1024; // 32 KB
        private const int RelevanceThreshold = 60;        // 0‒100 fuzzy score

        public ContentRelevanceChecker(HttpClient http,
                                       ILogger<ContentRelevanceChecker> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Fetches a short HTML snippet and fuzzy-matches it against
        /// <paramref name="lessonTopic"/>.  Returns <c>true</c> when
        /// the score ≥ <see cref="RelevanceThreshold"/>.
        /// </summary>
        public async Task<bool> IsHtmlSnippetRelevantAsync(
            string url,
            string lessonTopic,
            CancellationToken ct = default)
        {
            string? html;
            try
            {
                using var res = await _http.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET {Url} returned {Status}", url, res.StatusCode);
                    return false;
                }

                using var stream = await res.Content.ReadAsStreamAsync(ct);
                var buf = new byte[SnippetSize];
                var read = await stream.ReadAsync(buf, 0, buf.Length, ct);
                html = Encoding.UTF8.GetString(buf, 0, read);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception downloading {Url}", url);
                return false;
            }

            if (string.IsNullOrWhiteSpace(html))
                return false;

            var text = ExtractReadableText(html);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int score = Fuzz.TokenSetRatio(lessonTopic, text);
            _logger.LogInformation(
                "HTML relevance score={Score} for URL={Url}\nExtracted:\n{Extract}",
                score, url, text);

            return score >= RelevanceThreshold;
        }

        // ---- helpers ------------------------------------------------------------

        private static string ExtractReadableText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var sb = new StringBuilder();

            void Append(string? s)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    sb.AppendLine(s.Trim());
            }

            Append(doc.DocumentNode.SelectSingleNode("//title")?.InnerText);
            Append(doc.DocumentNode
                      .SelectSingleNode("//meta[@name='description']")
                      ?.GetAttributeValue("content", null));
            Append(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText);
            Append(doc.DocumentNode.SelectSingleNode("//p")?.InnerText);

            return sb.ToString().Trim();
        }
    }
}
