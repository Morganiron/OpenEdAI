using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenEdAI.API.Configuration;
using OpenEdAI.API.DTOs;
using System.Net.Http;

namespace OpenEdAI.API.Services
{
    /// <summary>
    /// Performs Google / YouTube searches and filters raw links through <see cref="LinkVet"/>.
    /// </summary>
    public sealed class AIDrivenContentSearchService : IContentSearchService
    {
        private readonly YouTubeService _youTube;
        private readonly CustomSearchAPIService _customSearch;
        private readonly string _cseId;
        private readonly ILogger<AIDrivenContentSearchService> _logger;

        public AIDrivenContentSearchService(
            AIDrivenSearchPlanService _ /* kept for DI compatibility */,
            IOptions<AppSettings> settings,
            ILogger<AIDrivenContentSearchService> logger)
        {
            _logger = logger;

            var apiKey = settings.Value.GoogleAPIs.ApiKey
                ?? throw new InvalidOperationException("Missing GoogleApis.ApiKey");

            _cseId = settings.Value.GoogleAPIs.CustomSearchEngineId
                ?? throw new InvalidOperationException("Missing GoogleApis.CustomSearchEngineId");

            _youTube = new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = apiKey,
                ApplicationName = "OpenEdAI"
            });

            _customSearch = new CustomSearchAPIService(new BaseClientService.Initializer
            {
                ApiKey = apiKey,
                ApplicationName = "OpenEdAI"
            });
        }

        /// <inheritdoc />
        public async Task<List<string>> SearchContentLinksAsync(
            CoursePersonalizationInput userInput,
            CoursePlanDTO coursePlan,
            LessonSearchPlanDTO searchPlan,
            StudentProfileDTO profile,
            CancellationToken token)
        {
            var rawLinks = new List<string>();

            // 1. Execute each AI-generated query -----------------------------------------
            foreach (var q in searchPlan.Queries)
            {
                if (q.Provider.Equals("YouTube", StringComparison.OrdinalIgnoreCase))
                {
                    var ytReq = _youTube.Search.List("snippet");
                    ytReq.Q = q.Query;
                    ytReq.Type = "video";
                    ytReq.MaxResults = q.MaxResults;

                    var ytResp = await ytReq.ExecuteAsync(token);
                    rawLinks.AddRange(
                        ytResp.Items.Select(item => $"https://youtu.be/{item.Id.VideoId}"));
                }
                else if (q.Provider.Equals("CustomSearch", StringComparison.OrdinalIgnoreCase))
                {
                    var csReq = _customSearch.Cse.List();
                    csReq.Cx = _cseId;

                    // Build "-site:" exclusions into the query string
                    var exclude = (q.ExcludedSites?.Any() == true)
                        ? " " + string.Join(' ', q.ExcludedSites)
                        : string.Empty;

                    csReq.Q = q.Query + exclude;
                    csReq.Num = q.MaxResults;

                    var csResp = await csReq.ExecuteAsync(token);
                    rawLinks.AddRange(csResp.Items.Select(item => item.Link));
                }
            }

            // 2. Vet + deduplicate --------------------------------------------------------
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var bucket = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var url in rawLinks.Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute)))
            {
                // Skip obvious redirects to other search pages
                if (url.Contains("/search", StringComparison.OrdinalIgnoreCase))
                    continue;

                var contentType = url.Contains("youtu", StringComparison.OrdinalIgnoreCase)
                    ? "Video"
                    : "Article"; // treat non-YouTube as Article for now

                try
                {
                    var ok = await LinkVet.IsAcceptableAsync(
                        url,
                        contentType,
                        lessonTopic: searchPlan.LessonTitle,
                        http: httpClient,
                        ct: token);

                    if (!ok) continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Link vetting threw for {Url}", url);
                    continue;
                }

                // Keep ≤2 links per type
                if (!bucket.TryGetValue(contentType, out var list))
                    bucket[contentType] = list = new(2);

                if (list.Count < 2)
                    list.Add(url);
            }

            // 3. Flatten while preserving order ------------------------------------------
            return bucket.Values.SelectMany(x => x).Distinct().ToList();
        }
    }
}
