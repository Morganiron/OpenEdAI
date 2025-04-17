using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.CustomSearchAPI.v1;
using OpenEdAI.API.DTOs;


namespace OpenEdAI.API.Services
{
    public class AIDrivenContentSearchService : IContentSearchService
    {
        private readonly AIDrivenSearchPlanService _planSvc;
        private readonly YouTubeService _youTube;
        private readonly CustomSearchAPIService _customSearch;
        private readonly string _cseId;
        private readonly ILogger<AIDrivenContentSearchService> _logger;

        public AIDrivenContentSearchService(AIDrivenSearchPlanService planSvc, IConfiguration config, ILogger<AIDrivenContentSearchService> logger)
        {
            _planSvc = planSvc;
            _logger = logger;

            // Read the ApiKey from appsettings.Development.json
            var apiKey = config["GoogleApis:ApiKey"]
                ?? throw new InvalidOperationException("Missing GoogleApis:ApiKey");

            // Read the Custom Search Engine Id
            _cseId = config["GoogleApis:CustomSearchEngineId"]
                ?? throw new InvalidOperationException("Missing GoogleApis:CustomSearchEngineId");

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

        // Generates an AI-driven search plan for the given course input and student profile
        public async Task<List<string>> SearchContentLinksAsync(CoursePersonalizationInput userInput, CoursePlanDTO coursePlan, LessonSearchPlanDTO searchPlan, StudentProfileDTO profile, CancellationToken token)
        {
            var rawLinks = new List<string>();

            // Execute each query the AI generated
            foreach (var q in searchPlan.Queries)
            {
                // If the query is for YouTube, search YouTube
                if (q.Provider.Equals("YouTube", StringComparison.OrdinalIgnoreCase))
                {
                    var searchRequest = _youTube.Search.List("snippet");
                    searchRequest.Q = q.Query;
                    searchRequest.Type = "video";
                    searchRequest.MaxResults = q.MaxResults;
                    
                    var searchResponse = await searchRequest.ExecuteAsync(token);
                    rawLinks.AddRange(searchResponse.Items.Select(item => $"https://youtu.be/{item.Id.VideoId}"));
                }
                // If the query is for Custom Search, search Custom Search
                else if (q.Provider.Equals("CustomSearch", StringComparison.OrdinalIgnoreCase))
                {
                    var searchRequest = _customSearch.Cse.List();
                    searchRequest.Cx = _cseId;

                    // Build the "exclude" portion into the query
                    var exclude = q.ExcludedSites != null && q.ExcludedSites.Any()
                        ? " " + string.Join(" ", q.ExcludedSites.Select(site => site))
                        : "";

                    // Concatenate the query with the exclude portion
                    searchRequest.Q = $"{q.Query}{exclude}";
                    searchRequest.Num = q.MaxResults;

                    var searchResponse = await searchRequest.ExecuteAsync(token);
                    rawLinks.AddRange(searchResponse.Items.Select(item => item.Link));
                }
            }

            // Vet, deduplicate, and limit the links
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };

            var byType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var url in rawLinks.Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute)))
            {
                // Skip obvious "search result" redirect pages
                if (url.Contains("/search", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Normalize: videos vs everything-else
                var type = url.Contains("youtu", StringComparison.OrdinalIgnoreCase)
                    ? "Video"
                    : "Article";

                // Use the link vetting service to check if the URL is acceptable for the given type
                if (!await LinkVet.IsAcceptableAsync(url, type, httpClient, token))
                {
                    _logger.LogDebug("Link vetting failed for {Url}", url);
                    continue;
                }


                // Keep at most 2 links of each type
                if (!byType.TryGetValue(type, out var list))
                {
                    byType[type] = list = new(2);
                }

                // Add the URL if we don't have 2 of this type already
                if (list.Count < 2)
                {
                    list.Add(url);
                }
            }
            // Flatten to a single list while preserving insertion order
            return byType.Values.SelectMany(x => x).Distinct().ToList();
        }
    }
}
