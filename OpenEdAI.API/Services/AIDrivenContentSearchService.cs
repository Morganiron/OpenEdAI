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

            // --- execute YouTube / CustomSearch queries -----------------------------
            foreach (var q in searchPlan.Queries)
            {
                if (q.Provider.Equals("YouTube", StringComparison.OrdinalIgnoreCase))
                {
                    var yt = _youTube.Search.List("snippet");
                    yt.Q = q.Query;
                    yt.Type = "video";
                    yt.MaxResults = q.MaxResults;
                    rawLinks.AddRange((await yt.ExecuteAsync(token))
                                      .Items.Select(i => $"https://youtu.be/{i.Id.VideoId}"));
                }
                else if (q.Provider.Equals("CustomSearch", StringComparison.OrdinalIgnoreCase))
                {
                    var cs = _customSearch.Cse.List();
                    cs.Cx = _cseId;
                    cs.Q = q.Query + (q.ExcludedSites?.Any() == true
                                        ? " " + string.Join(' ', q.ExcludedSites)
                                        : "");
                    cs.Num = q.MaxResults;
                    rawLinks.AddRange((await cs.ExecuteAsync(token)).Items.Select(i => i.Link));
                }
            }

            // --- vet / dedupe --------------------------------------------------------
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var bucket = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var url in rawLinks.Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute)))
            {
                if (url.Contains("/search", StringComparison.OrdinalIgnoreCase)) continue;

                var ctype = url.Contains("youtu", StringComparison.OrdinalIgnoreCase) ? "Video" : "Article";

                try
                {
                    if (!await LinkVet.IsAcceptableAsync(
                            url, ctype, searchPlan.LessonTitle, http, token))
                        continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Link vetting threw for {Url}", url);
                    continue;
                }

                if (!bucket.TryGetValue(ctype, out var list))
                    bucket[ctype] = list = new(2);

                if (list.Count < 2) list.Add(url);
            }

            return bucket.Values.SelectMany(x => x).Distinct().ToList();
        }
    }
}
