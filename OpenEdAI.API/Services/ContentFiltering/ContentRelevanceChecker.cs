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
    public sealed class ContentRelevanceChecker
    {
        private readonly HttpClient _http;
        private readonly ILogger<ContentRelevanceChecker> _logger;

        private const int SnippetSize = 32 * 1024;      // 32 KB
        private const int RelevanceThreshold = 60;      // 0–100 fuzzy score

        public ContentRelevanceChecker(HttpClient http, ILogger<ContentRelevanceChecker> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// For YouTube (or other video) results: no HTTP GET is performed.
        /// We simply fuzzy-match the video’s Snippet.Title + Snippet.Description
        /// against the lessonTopic.  Returns true if score ≥ threshold.
        /// </summary>
        public bool IsYouTubeSnippetRelevant(
            string snippetTitle,
            string snippetDescription,
            string lessonTopic)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(snippetTitle))
                sb.AppendLine(snippetTitle.Trim());
            if (!string.IsNullOrWhiteSpace(snippetDescription))
                sb.AppendLine(snippetDescription.Trim());

            var combined = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(combined))
            {
                _logger.LogInformation("Empty YouTube snippet for lessonTopic='{Topic}'", lessonTopic);
                return false;
            }

            int score = Fuzz.TokenSetRatio(lessonTopic, combined);
            _logger.LogInformation("YouTube snippet relevance score={Score} for lessonTopic='{Topic}\nwith snippet:\n{Combined}'", score, lessonTopic, combined);

            return score >= RelevanceThreshold;
        }

        /// <summary>
        /// For non-YouTube URLs (Article or Forum): fetch up to SnippetSize bytes of HTML,
        /// extract <title>, <meta name='description'>, first <h1> and first <p>, then fuzzy-match.
        /// </summary>
        public async Task<bool> IsHtmlSnippetRelevantAsync(
            string url,
            string lessonTopic,
            CancellationToken ct)
        {
            string? htmlSnippet;
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to GET HTML (status {Status}) for URL={Url}", response.StatusCode, url);
                    return false;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[SnippetSize];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                htmlSnippet = Encoding.UTF8.GetString(buffer, 0, read);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception fetching HTML snippet for URL={Url}", url);
                return false;
            }

            if (string.IsNullOrWhiteSpace(htmlSnippet))
            {
                _logger.LogInformation("Empty HTML snippet for URL={Url}", url);
                return false;
            }

            var extractedText = ExtractText(htmlSnippet);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogInformation("No extractable text for URL={Url}", url);
                return false;
            }

            int score = Fuzz.TokenSetRatio(lessonTopic, extractedText);
            _logger.LogInformation("HTML snippet relevance score={Score} for URL={Url}\nwith extracted text:\n{extractedText}", score, url, extractedText);
            return score >= RelevanceThreshold;
        }

        /// <summary>
        /// Extracts text from <title>, <meta name='description'>, first <h1>, first <p> in the HTML.
        /// </summary>
        private static string ExtractText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var sb = new StringBuilder();

            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null && !string.IsNullOrWhiteSpace(titleNode.InnerText))
                sb.AppendLine(titleNode.InnerText.Trim());

            var metaDesc = doc.DocumentNode
                              .SelectSingleNode("//meta[@name='description']")?
                              .GetAttributeValue("content", null);
            if (!string.IsNullOrWhiteSpace(metaDesc))
                sb.AppendLine(metaDesc.Trim());

            var h1Node = doc.DocumentNode.SelectSingleNode("//h1");
            if (h1Node != null && !string.IsNullOrWhiteSpace(h1Node.InnerText))
                sb.AppendLine(h1Node.InnerText.Trim());

            var pNode = doc.DocumentNode.SelectSingleNode("//p");
            if (pNode != null && !string.IsNullOrWhiteSpace(pNode.InnerText))
                sb.AppendLine(pNode.InnerText.Trim());

            return sb.ToString().Trim();
        }
    }
}
