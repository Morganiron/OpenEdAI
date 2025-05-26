using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenEdAI.API.Services;
using OpenEdAI.Services.ContentFiltering;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OpenEdAI.Tests.Services
{
    /// <summary>
    /// Unit tests for LinkVet, verifying fallback logic and domain filtering.
    /// </summary>
    public class LinkVetTests
    {
        private readonly DomainFilter _filter = new();

        private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) => responder(request));

            return new HttpClient(handlerMock.Object);
        }

        [Fact]
        public async Task IsAcceptableAsync_HeadAndGetFail_PreferredDomain_ReturnsTrue()
        {
            // Arrange: both methods throw or return non-success
            var http = CreateHttpClient(req => throw new HttpRequestException());
            string url = "https://ocw.mit.edu/index.htm";

            // Act
            bool result = await LinkVet.IsAcceptableAsync(url, "Article", http, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAcceptableAsync_HeadAndGetFail_NonPreferredDomain_ReturnsFalse()
        {
            var http = CreateHttpClient(req => throw new HttpRequestException());
            string url = "https://example.com/page";

            bool result = await LinkVet.IsAcceptableAsync(url, "Article", http, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task IsAcceptableAsync_HeadSuccess_ValidMime_ReturnsTrue()
        {
            // Arrange: HEAD returns HTML
            var http = CreateHttpClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html") }
                }
            });
            string url = "https://medium.com/sample-article";

            // Act
            bool result = await LinkVet.IsAcceptableAsync(url, "Article", http, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAcceptableAsync_HeadSuccess_InvalidMime_ReturnsFalse()
        {
            var http = CreateHttpClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") }
                }
            });
            string url = "https://medium.com/sample-article";

            bool result = await LinkVet.IsAcceptableAsync(url, "Article", http, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task IsAcceptableAsync_HeadFails_GetSucceeds_ValidMime_ReturnsTrue()
        {
            // Arrange: HEAD fails, GET returns valid HTML
            var http = CreateHttpClient(req =>
            {
                if (req.Method == HttpMethod.Head)
                    throw new HttpRequestException();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html") }
                    }
                };
            });
            string url = "https://developer.mozilla.org/docs/Web/HTML";

            // Act
            bool result = await LinkVet.IsAcceptableAsync(url, "Article", http, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAcceptableAsync_ValidHost_WrongType_ReturnsFalse()
        {
            var http = CreateHttpClient(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html") }
                }
            });
            // Known video host but requested as Article
            string url = "https://youtube.com/watch?v=test";

            bool result = await LinkVet.IsAcceptableAsync(url, "Article", http, CancellationToken.None);

            Assert.False(result);
        }

        [Theory]
        [InlineData("invalid-url", "Article")]
        [InlineData("", "Video")]
        public async Task IsAcceptableAsync_InvalidUrl_ReturnsFalse(string url, string type)
        {
            var http = CreateHttpClient(req => throw new InvalidOperationException());

            bool result = await LinkVet.IsAcceptableAsync(url, type, http, CancellationToken.None);

            Assert.False(result);
        }
    }
}
