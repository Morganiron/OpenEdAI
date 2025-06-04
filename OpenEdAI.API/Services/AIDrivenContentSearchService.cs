using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenEdAI.API.Configuration;         // for AppSettings, etc.
using OpenEdAI.API.DTOs;                  // for LessonSearchPlanDTO, SearchQueryDTO
using OpenEdAI.Services.ContentFiltering; // for ContentRelevanceChecker, LinkVet
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEdAI.API.Services
{
    public class AIDrivenContentSearchService : IContentSearchService
    {
        private readonly YouTubeService _youTube;
        private readonly CustomSearchAPIService _customSearch;
        private readonly string _cseId;
        private readonly ContentRelevanceChecker _contentRelevanceChecker;
        private readonly ILogger<AIDrivenContentSearchService> _logger;
        private readonly HttpClient _httpClient;

        public AIDrivenContentSearchService(
            YouTubeService youTube,
            CustomSearchAPIService customSearch,
            IOptions<AppSettings> settings,
            ContentRelevanceChecker contentRelevanceChecker,
            ILogger<AIDrivenContentSearchService> logger,
            HttpClient httpClient)
        {
            _youTube = youTube
                ?? throw new ArgumentNullException(nameof(youTube));
            _customSearch = customSearch
                ?? throw new ArgumentNullException(nameof(customSearch));
            _cseId = settings?.Value?.GoogleAPIs?.CustomSearchEngineId
                ?? throw new InvalidOperationException("Missing GoogleAPIs:CustomSearchEngineId");
            _contentRelevanceChecker = contentRelevanceChecker
                ?? throw new ArgumentNullException(nameof(contentRelevanceChecker));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient
                ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <inheritdoc />
        /// <remarks>
        ///  - For each `SearchQueryDTO q` in `searchPlan.Queries`:
        ///     • If `q.Provider == "YouTube"`, run a YT search → snippet relevance → keep up to 2 videos.  
        ///     • If `q.Provider == "CustomSearch"`, do a “siterestrict” search → LinkVet → HTML relevance → keep up to 2.  
        ///       If fewer than 2, do an unrestricted search → LinkVet → HTML relevance → fill up to 2.  
        ///  - After gathering all raw links, bucket them into Video/Forum/Article and call `DedupeAndLimit(...)`.  
        ///  - Finally return a combined list, respecting the user’s content‐type quotas (Video=2, Article=2, Forum=2), or 4 if they only requested one non‐video type.
        /// </remarks>
        public async Task<List<string>> SearchContentLinksAsync(
            CoursePersonalizationInput userInput,
            CoursePlanDTO coursePlan,
            LessonSearchPlanDTO searchPlan,
            StudentProfileDTO profile,
            CancellationToken token)
        {
            // Find the corresponding LessonPlanDTO in coursePlan.Lessons by title
            var lessonDto = coursePlan.Lessons
                .FirstOrDefault(l => string.Equals(l.Title, searchPlan.LessonTitle, StringComparison.OrdinalIgnoreCase));

            // If not found, fallback to using only the lesson title
            string lessonDescription = lessonDto?.Description ?? "";

            // If lessonDto is null, use an empty list of tags
            List<string> lessonTags = lessonDto != null ? lessonDto.Tags ?? new List<string>() : new List<string>();

            // Build combined context: title + tags + description
            string combinedContext = $"{searchPlan.LessonTitle} " +
                                     $"{string.Join(" ", lessonTags)} " +
                                     $"{lessonDescription}";

            var rawLinks = new List<string>();

            foreach (var q in searchPlan.Queries)
            {
                if (string.Equals(q.Provider, "YouTube", StringComparison.OrdinalIgnoreCase))
                {
                    // ───────────────────────────────────────────────────────────────────────────
                    // 1) YOUTUBE BRANCH: do YT search, then only snippet relevance (no LinkVet).
                    // ───────────────────────────────────────────────────────────────────────────
                    var ytRequest = _youTube.Search.List("snippet");
                    ytRequest.Q = q.Query;
                    ytRequest.Type = "video";
                    ytRequest.MaxResults = q.MaxResults;

                    var ytResponse = await ytRequest.ExecuteAsync(token);
                    int keptVideos = 0;

                    foreach (var item in ytResponse.Items)
                    {
                        if (keptVideos >= 2)
                            break;

                        var snippetTitle = item.Snippet.Title ?? "";
                        var snippetDesc = item.Snippet.Description ?? "";

                        // Use combinedContext instead of just lesson title
                        bool isRelevant = _contentRelevanceChecker
                            .IsYouTubeSnippetRelevant(snippetTitle, snippetDesc, combinedContext);

                        if (!isRelevant)
                            continue;

                        string videoUrl = $"https://youtu.be/{item.Id.VideoId}";
                        rawLinks.Add(videoUrl);
                        keptVideos++;
                    }
                }
                else if (string.Equals(q.Provider, "CustomSearch", StringComparison.OrdinalIgnoreCase))
                {
                    // ───────────────────────────────────────────────────────────────────────────
                    // 2) CUSTOMSEARCH BRANCH (allow-list first, then fallback to general)
                    //
                    //    (a) “Preferred-sites” pass → perform ONE “site:host1 OR site:host2 …” query
                    //    (b) If < 2 links found, do one unrestricted search → vet → collect until 2
                    // ───────────────────────────────────────────────────────────────────────────

                    // 1) We assume all CustomSearch queries here are “Article”
                    var desiredType = ContentType.Article;

                    // 2) Grab the allow-list of hosts from DomainFilter
                    var domainFilter = new DomainFilter();
                    var allowedHosts = domainFilter.GetAllowedHosts(desiredType);

                    // 3) Build a single “site:…” clause that covers all allowedHosts
                    //    e.g. "site:medium.com OR site:khanacademy.org OR site:freecodecamp.org"
                    var siteClause = string.Join(" OR ",
                        allowedHosts.Select(h => $"site:{h}"));

                    var vettedLinks = new List<string>();

                    // (a) SITE-INCLUDE phase: one search combining all hosts
                    {
                        var srReq = _customSearch.Cse.List();
                        srReq.Cx = _cseId;
                        srReq.Q = $"{q.Query} ({siteClause})";
                        srReq.Num = q.MaxResults;

                        var srResp = await srReq.ExecuteAsync(token);
                        if (srResp.Items != null)
                        {
                            foreach (var item in srResp.Items)
                            {
                                if (vettedLinks.Count >= 2)
                                    break;

                                var url = item.Link;
                                if (string.IsNullOrWhiteSpace(url))
                                    continue;

                                // 4) Run LinkVet (re-check host/path heuristics)
                                bool passesVet;
                                try
                                {
                                    passesVet = await LinkVet.IsAcceptableAsync(
                                        url: url,
                                        requestedType: desiredType.ToString(), // “Article”
                                        lessonTopic: combinedContext,            // use combinedContext
                                        http: _httpClient,
                                        ct: token
                                    );
                                }
                                catch
                                {
                                    continue;
                                }

                                if (!passesVet)
                                    continue;

                                // 5) Check fuzzy HTML snippet relevance using combinedContext
                                bool isRelevant = await _contentRelevanceChecker
                                    .IsHtmlSnippetRelevantAsync(url, combinedContext, token);
                                if (!isRelevant)
                                    continue;

                                vettedLinks.Add(url);
                            }
                        }
                    }

                    // (b) FALLBACK – if fewer than 2 found above, do one unrestricted search
                    if (vettedLinks.Count < 2)
                    {
                        var genReq = _customSearch.Cse.List();
                        genReq.Cx = _cseId;
                        genReq.Q = q.Query;
                        genReq.Num = q.MaxResults;

                        var genResp = await genReq.ExecuteAsync(token);
                        var genItems = genResp.Items ?? Array.Empty<Result>();

                        foreach (var item in genItems)
                        {
                            if (vettedLinks.Count >= 2)
                                break;

                            var url = item.Link;
                            if (string.IsNullOrWhiteSpace(url) || vettedLinks.Contains(url))
                                continue;

                            bool passesVet;
                            try
                            {
                                passesVet = await LinkVet.IsAcceptableAsync(
                                    url: url,
                                    requestedType: desiredType.ToString(),
                                    lessonTopic: combinedContext,            // use combinedContext
                                    http: _httpClient,
                                    ct: token
                                );
                            }
                            catch
                            {
                                continue;
                            }
                            if (!passesVet)
                                continue;

                            bool isRelevant = await _contentRelevanceChecker
                                .IsHtmlSnippetRelevantAsync(url, combinedContext, token);
                            if (!isRelevant)
                                continue;

                            vettedLinks.Add(url);
                        }
                    }

                    // Finally add up to 2 vetted links into rawLinks
                    rawLinks.AddRange(vettedLinks.Take(2));
                }
            }

            // ───────────────────────────────────────────────────────────────────────────
            // 3) FINAL DEDUPE + BUCKET LIMITS
            // ───────────────────────────────────────────────────────────────────────────
            var finalLinks = DedupeAndLimit(rawLinks, searchPlan, profile);
            return finalLinks;
        }

        /// <summary>
        /// Given a list of “rawLinks” (which may contain YouTube, generic‐article, forum‐article, etc.),
        /// bucket each URL by Video/Forum/Article, then
        /// - take up to 2 Video (if Videos were requested),
        /// - up to 2 Forum (if Forums were requested),
        /// - up to 2 Article (if Articles were requested),
        /// or if the user only asked for a single non‐Video type, allow up to 4 of that type.
        /// </summary>
        private List<string> DedupeAndLimit(
            List<string> rawLinks,
            LessonSearchPlanDTO searchPlan,
            StudentProfileDTO profile)
        {
            // First: determine which content types the user actually wants for this lesson:
            var prefs = profile.PreferredContentTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet();

            bool wantsVideo = prefs.Contains("video") || prefs.Contains("video tutorials");
            bool wantsArticle = prefs.Contains("articles");
            bool wantsForum = prefs.Contains("discussion forums") || prefs.Contains("forums");

            // If only “Articles” was chosen → we allow up to 4 articles.
            // If only “Forums” was chosen → allow 4 forums. 
            // Otherwise, for each bucket (Video / Article / Forum) we cap at 2.
            int capVideo = wantsVideo ? 2 : 0;
            int capArticle = (wantsArticle && !wantsForum && !wantsVideo) ? 4 : (wantsArticle ? 2 : 0);
            int capForum = (wantsForum && !wantsVideo && !wantsArticle) ? 4 : (wantsForum ? 2 : 0);

            // Bucket the URLs:
            var videos = new List<string>();
            var articles = new List<string>();
            var forums = new List<string>();

            foreach (var url in rawLinks.Distinct())
            {
                switch (ClassifyUrl(url))
                {
                    case ContentBucket.Video:
                        if (videos.Count < capVideo)
                            videos.Add(url);
                        break;

                    case ContentBucket.Forum:
                        if (forums.Count < capForum)
                            forums.Add(url);
                        break;

                    case ContentBucket.Article:
                    default:
                        if (articles.Count < capArticle)
                            articles.Add(url);
                        break;
                }
            }

            // Merge in “Video first, then Articles, then Forums” (you can reorder if desired)
            var result = new List<string>();
            result.AddRange(videos);
            result.AddRange(articles);
            result.AddRange(forums);
            return result;
        }

        /// <summary>
        /// Very basic URL classification into Video/Forum/Article based on hostname or file extension.
        /// </summary>
        private enum ContentBucket
        {
            Video,
            Forum,
            Article
        }

        private static ContentBucket ClassifyUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return ContentBucket.Article;

            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath.ToLowerInvariant();

            // 1) If it looks like a YouTube/vimeo/MP4/WEBM → Video
            if (host.Contains("vimeo.com") ||
                path.EndsWith(".mp4") ||
                path.EndsWith(".webm"))
            {
                return ContentBucket.Video;
            }

            // 2) If host is known forum domain → Forum:
            var forumHosts = new[] { "stackoverflow.com", "reddit.com", "quora.com", "github.com" };
            if (forumHosts.Any(fh => host.Contains(fh)))
                return ContentBucket.Forum;

            // 3) Everything else → Article
            return ContentBucket.Article;
        }
    }
}
