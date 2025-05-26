using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using FuzzySharp;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenEdAI.Configuration;

namespace OpenEdAI.Services.ContentFiltering
{
    /// <summary>
    /// Heuristics for determining whether a YouTube video is suitable for a lesson.
    /// </summary>
    /// <remarks>
    /// * Checks duration, caption availability, and fuzzy relevance of title/description.
    /// * Call once per candidate video link.
    /// </remarks>
    public sealed class YouTubeHeuristics : IYouTubeHeuristics
    {
        private readonly YouTubeService _youTube;
        private readonly ILogger<YouTubeHeuristics> _logger;

        // Values come from appsettings (YouTubeHeuristics section)
        private readonly TimeSpan _minDuration;
        private readonly TimeSpan _maxDuration;
        private readonly int _fuzzThreshold;
        private readonly bool _requireCaptions;

        public YouTubeHeuristics(
            YouTubeService youTube,
            IOptions<YouTubeHeuristicsSettings> options,
            ILogger<YouTubeHeuristics> logger)
        {
            _youTube = youTube ?? throw new ArgumentNullException(nameof(youTube));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var cfg = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _minDuration = TimeSpan.FromMinutes(Math.Max(1, cfg.MinDurationMinutes));
            _maxDuration = TimeSpan.FromMinutes(Math.Max(cfg.MinDurationMinutes, cfg.MaxDurationMinutes));
            _fuzzThreshold = Math.Clamp(cfg.FuzzyThreshold, 0, 100);
            _requireCaptions = cfg.RequireCaptions;
        }

        /// <summary>
        /// Returns <c>true</c> if the YouTube video referenced by <paramref name="videoUrlOrId"/>
        /// is relevant to <paramref name="lessonTopic"/> and meets duration/caption heuristics.
        /// </summary>
        public async Task<bool> IsRelevantAsync(string videoUrlOrId, string lessonTopic, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(videoUrlOrId)) return false;
            var id = ExtractVideoId(videoUrlOrId);
            if (id is null)
            {
                _logger.LogDebug("Could not extract YouTube ID from {Input}", videoUrlOrId);
                return false;
            }

            // Request snippet + contentDetails for the single video
            var req = _youTube.Videos.List("snippet,contentDetails");
            req.Id = id;
            var resp = await req.ExecuteAsync(ct);
            if (resp.Items.Count == 0)
            {
                _logger.LogInformation("Video {Id} not found via YouTube Data API", id);
                return false;
            }
            var vid = resp.Items[0];

            // 1) Duration window ----------------------------------------------------------
            if (!TryParseIsoDuration(vid.ContentDetails.Duration, out var dur))
            {
                _logger.LogInformation("Unable to parse duration for video {Id}", id);
                return false;
            }
            if (dur < _minDuration || dur > _maxDuration)
            {
                _logger.LogInformation("Rejected video {Id} – duration {Dur} outside [{Min}, {Max}]", id, dur, _minDuration, _maxDuration);
                return false;
            }

            // 2) Captions available -------------------------------------------------------
            if (_requireCaptions && !string.Equals(vid.ContentDetails.Caption, "true", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Rejected video {Id} – no captions", id);
                return false;
            }

            // 3) Fuzzy relevance ----------------------------------------------------------
            var text = new StringBuilder()
                .Append(vid.Snippet.Title).Append(' ').Append(vid.Snippet.Description)
                .ToString();
            int score = Fuzz.TokenSetRatio(lessonTopic, text);
            if (score < _fuzzThreshold)
            {
                _logger.LogInformation("Rejected video {Id} – fuzzy score {Score} < {Threshold}", id, score, _fuzzThreshold);
                return false;
            }

            _logger.LogDebug("Video {Id} accepted – score {Score}, duration {Dur}", id, score, dur);
            return true;
        }

        #region Helpers
        private static string? ExtractVideoId(string input)
        {
            // Handles watch?v=, youtu.be/, embed/, or raw 11‑char ID
            var match = System.Text.RegularExpressions.Regex.Match(input,
                "(?:v=|youtu\\.be/|embed/|watch\\?v=)?(?<id>[A-Za-z0-9_-]{11})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["id"].Value : null;
        }

        private static bool TryParseIsoDuration(string iso, out TimeSpan timeSpan)
        {
            try
            {
                timeSpan = XmlConvert.ToTimeSpan(iso);
                return true;
            }
            catch
            {
                timeSpan = default;
                return false;
            }
        }
        #endregion
    }
}
