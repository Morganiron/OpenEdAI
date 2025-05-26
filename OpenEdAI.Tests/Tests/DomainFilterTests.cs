using OpenEdAI.Services.ContentFiltering;

namespace OpenEdAI.Tests.ContentFiltering
{
    /// <summary>
    /// Unit tests for the DomainFilter, ensuring allow/deny logic works as expected.
    /// </summary>
    public class DomainFilterTests
    {
        private readonly DomainFilter _filter = new();

        [Theory]
        [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ", ContentType.Video)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", ContentType.Video)]
        [InlineData("https://vimeo.com/123456", ContentType.Video)]
        public void IsAllowed_KnownVideoHosts_ReturnsTrue(string url, ContentType type)
        {
            // Act
            bool result = _filter.IsAllowed(url, type);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("https://medium.com/some-article", ContentType.Article)]
        [InlineData("https://khanacademy.org/learning-path", ContentType.Article)]
        [InlineData("https://developer.mozilla.org/en-US/docs/Web/HTML", ContentType.Article)]
        public void IsAllowed_KnownArticleHosts_ReturnsTrue(string url, ContentType type)
        {
            bool result = _filter.IsAllowed(url, type);
            Assert.True(result);
        }

        [Theory]
        [InlineData("https://stackoverflow.com/questions/12345", ContentType.Forum)]
        [InlineData("https://www.reddit.com/r/programming", ContentType.Forum)]
        public void IsAllowed_KnownForumHosts_ReturnsTrue(string url, ContentType type)
        {
            bool result = _filter.IsAllowed(url, type);
            Assert.True(result);
        }

        [Fact]
        public void IsAllowed_SubdomainMatchesBaseDomain_ReturnsTrue()
        {
            // Arrange
            string url = "https://learn.khanacademy.org/math";
            // Act
            bool result = _filter.IsAllowed(url, ContentType.Article);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("https://facebook.com/profile", ContentType.Article)]
        [InlineData("https://twitter.com/home", ContentType.Forum)]
        [InlineData("https://linkedin.com/jobs", ContentType.Article)]
        public void IsAllowed_GlobalDenyHosts_ReturnsFalse(string url, ContentType type)
        {
            bool result = _filter.IsAllowed(url, type);
            Assert.False(result);
        }

        [Theory]
        [InlineData("https://example.com/programs/introduction", ContentType.Article)]
        [InlineData("https://example.com/enroll/cs101", ContentType.Video)]
        [InlineData("https://example.com/jobs/listing", ContentType.Forum)]
        public void IsAllowed_DenyPathKeywords_ReturnsFalse(string url, ContentType type)
        {
            bool result = _filter.IsAllowed(url, type);
            Assert.False(result);
        }

        [Theory]
        [InlineData("https://youtube.com/watch?v=abc", ContentType.Article)]
        [InlineData("https://medium.com/post", ContentType.Video)]
        public void IsAllowed_ValidHostWrongContentType_ReturnsFalse(string url, ContentType wrongType)
        {
            bool result = _filter.IsAllowed(url, wrongType);
            Assert.False(result);
        }

        [Theory]
        [InlineData("invalid-url", ContentType.Article)]
        [InlineData("", ContentType.Video)]
        public void IsAllowed_InvalidUrlStrings_ReturnsFalse(string url, ContentType type)
        {
            bool result = _filter.IsAllowed(url, type);
            Assert.False(result);
        }
    }
}
