using OpenEdAI.API.DTOs;

namespace OpenEdAI.API.Services
{
    public class DummyContentSearchService : IContentSearchService
    {
        public Task<List<string>> SearchContentLinksAsync(
            string lessonTitle,
            string lessonDescription,
            List<string> tags,
            StudentProfileDTO studentProfile,
            CancellationToken cancellationToken)
        {
            // This is a dummy implementation that simulates a search
            var results = new List<string>
            {
                $"https://example.com/search?q={Uri.EscapeDataString(lessonTitle)}+video",
                $"https://example.com/search?q={Uri.EscapeDataString(lessonTitle)}+article",
                $"https://example.com/search?q={Uri.EscapeDataString(lessonTitle)}+tutorial"
            };

            return Task.FromResult(results);
        }
    }
}
