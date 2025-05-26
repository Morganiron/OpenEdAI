namespace OpenEdAI.Services.ContentFiltering
{
    // Enumeration for supported content types
    public enum ContentType
    {
        Video,
        Article,
        Forum
    }

    // Handles filtering of domains and URL patterns to determine content eligibility
    public sealed class DomainFilter
    {
        // Allow-lists by content type, used to validate trusted sources
        private static readonly Dictionary<ContentType, HashSet<string>> AllowLists = new()
        {
            [ContentType.Video] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "youtube.com",
                "youtu.be",
                "vimeo.com",
                "dailymotion.com",
                "coursera.org",
                "edx.org",
                "khanacademy.org"
            },
            [ContentType.Article] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "medium.com",
                "khanacademy.org",
                "freecodecamp.org",
                "developer.mozilla.org",
                "ocw.mit.edu",
                "openlearn.open.ac.uk",
                "saylor.org",
                "oercommons.org",
                "ted.com",
                "dev.to"
            },
            [ContentType.Forum] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "stackoverflow.com",
                "quora.com",
                "reddit.com",
                "github.com"
            }
        };

        // Global deny-list of hosts to exclude from all results
        private static readonly HashSet<string> GlobalDenyList = new(StringComparer.OrdinalIgnoreCase)
        {
            "facebook.com",
            "twitter.com",
            "x.com",
            "instagram.com",
            "tumblr.com",
            "pinterest.com",
            "linkedin.com"
        };

        // URL path or query fragments to block (marketing pages, signup flows, etc.)
        private static readonly string[] DenyPathKeywords = new[]
        {
            "/programs/",
            "/enroll/",
            "/careers/",
            "/profile/",
            "/jobs",
            "?jid=",
            "/apply",
            "/admissions",
            "/certificate",
            ".social"
        };

        // Main entry: returns true if a URL is allowed based on host and path heuristics
        public bool IsAllowed(string url, ContentType type)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Validate and parse the URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            string host = uri.Host.ToLowerInvariant();
            string path = uri.AbsolutePath.ToLowerInvariant() + uri.Query.ToLowerInvariant();

            // Block known disallowed domains
            if (GlobalDenyList.Contains(host))
                return false;

            // Block URLs containing blacklisted path fragments
            if (DenyPathKeywords.Any(fragment => path.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Allow if host is explicitly trusted
            if (AllowLists.TryGetValue(type, out var allowedDomains) && allowedDomains.Contains(host))
                return true;

            // Fallback: allow subdomain match against trusted base domains
            return allowedDomains?.Any(allowed => host == allowed || host.EndsWith('.' + allowed)) == true;
        }
    }
}
