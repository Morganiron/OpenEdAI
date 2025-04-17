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

        public AIDrivenContentSearchService(AIDrivenSearchPlanService planSvc, IConfiguration config)
        {
            _planSvc = planSvc;

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
            var results = new List<string>();

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
                    results.AddRange(searchResponse.Items.Select(item => $"https://youtu.be/{item.Id.VideoId}"));
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
                    results.AddRange(searchResponse.Items.Select(item => item.Link));
                }
            }

            // Dedupe and return only well formed URLs
            return results
                .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                .Distinct()
                .ToList();
        }
    }
}
