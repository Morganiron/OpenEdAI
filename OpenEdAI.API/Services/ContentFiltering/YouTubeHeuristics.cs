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
    /// Central acceptance logic for YouTube videos:
    ///   • duration window  
    ///   • (optional) captions required  
    ///   • fuzzy relevance of *title + description*  
    /// </summary>
    public sealed class YouTubeHeuristics : IYouTubeHeuristics
    {
        private readonly YouTubeService _yt;
        private readonly ILogger<YouTubeHeuristics> _log;

        private readonly TimeSpan _minDur;
        private readonly TimeSpan _maxDur;
        private readonly int _fuzzThreshold;
        private readonly bool _needCaptions;

        public YouTubeHeuristics(
            YouTubeService yt,
            IOptions<YouTubeHeuristicsSettings> cfg,
            ILogger<YouTubeHeuristics> log)
        {
            _yt = yt ?? throw new ArgumentNullException(nameof(yt));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            var s = cfg?.Value ?? throw new ArgumentNullException(nameof(cfg));
            _minDur = TimeSpan.FromMinutes(Math.Max(1, s.MinDurationMinutes));
            _maxDur = TimeSpan.FromMinutes(Math.Max(s.MinDurationMinutes, s.MaxDurationMinutes));
            _fuzzThreshold = Math.Clamp(s.FuzzyThreshold, 0, 100);
            _needCaptions = s.RequireCaptions;
        }

        public async Task<bool> IsRelevantAsync(
            string videoUrlOrId,
            string lessonTopic,
            CancellationToken ct = default)
        {
            // 1) Extract the 11-character YouTube ID
            var id = ExtractId(videoUrlOrId);
            if (id == null)
            {
                _log.LogDebug("Could not extract YouTube ID from '{Input}'", videoUrlOrId);
                return false;
            }

            // 2) Fetch snippet + contentDetails
            var req = _yt.Videos.List("snippet,contentDetails");
            req.Id = id;
            var resp = await req.ExecuteAsync(ct);
            if (resp.Items.Count == 0)
            {
                _log.LogInformation("Video '{Id}' not found via YouTube API", id);
                return false;
            }

            var v = resp.Items[0];

            // 3) Parse ISO 8601 duration string, e.g. "PT3M45S"
            TimeSpan duration;
            try
            {
                duration = XmlConvert.ToTimeSpan(v.ContentDetails.Duration);
            }
            catch (FormatException ex)
            {
                _log.LogError(ex, "Video '{Id}' rejected – failed to parse duration '{RawDuration}'",
                              id, v.ContentDetails.Duration);
                return false;
            }

            // 4) Check duration window
            if (duration < _minDur || duration > _maxDur)
            {
                _log.LogInformation(
                    "Video '{Id}' rejected – duration {Dur} outside allowed range ({Min} - {Max})",
                    id, duration, _minDur, _maxDur
                );
                return false;
            }

            // 5) If captions are required, ensure they exist
            if (_needCaptions &&
                !string.Equals(v.ContentDetails.Caption, "true", StringComparison.OrdinalIgnoreCase))
            {
                _log.LogInformation("Video '{Id}' rejected – captions missing", id);
                return false;
            }

            // 6) Fuzzy-match title + description against lessonTopic
            var snippetText = $"{v.Snippet.Title}\n{v.Snippet.Description}".Trim();
            int score = Fuzz.TokenSetRatio(lessonTopic, snippetText);

            _log.LogInformation(
                "YT relevance score={Score} for lessonTopic='{Topic}'\nSnippet:\n{Snippet}",
                score, lessonTopic, snippetText
            );

            if (score < _fuzzThreshold)
            {
                _log.LogInformation("Video '{Id}' rejected – fuzzy score {Score} < {Threshold}",
                                     id, score, _fuzzThreshold);
                return false;
            }

            _log.LogDebug("Video '{Id}' accepted – duration={Dur}, score={Score}", id, duration, score);
            return true;
        }

        // ---- helper to extract a standard 11-character ID from various YouTube URL formats ----
        private static string? ExtractId(string input)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                input,
                @"(?:v=|youtu\.be/|embed/|watch\?v=)?(?<id>[A-Za-z0-9_-]{11})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return m.Success ? m.Groups["id"].Value : null;
        }
    }
}
