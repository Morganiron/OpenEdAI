using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenEdAI.API.Services;
using OpenEdAI.Services.ContentFiltering;
using Xunit;

namespace OpenEdAI.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="LinkVet"/> verifying domain filtering, MIME heuristics, and network fall‑back logic.
    /// The real <see cref="ContentRelevanceChecker"/> is used with a stub <see cref="HttpClient"/> so we don’t have
    /// to mock a sealed class.
    /// </summary>
    public class LinkVetTests
    {
        private readonly ContentRelevanceChecker _relevanceChecker;
        private readonly ILoggerFactory _loggerFactory;

        public LinkVetTests()
        {
            // Stub HttpClient that always returns a tiny HTML page containing the lesson topic string.
            var handler = new StubMessageHandler((request, _) =>
            {
                var html = "<title>" + request.RequestUri!.AbsoluteUri + "</title><p>lesson topic</p>";
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html)
                };
                resp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return resp;
            });
            var httpClient = new HttpClient(handler);

            _loggerFactory = LoggerFactory.Create(b => { /* no sinks */ });
            _relevanceChecker = new ContentRelevanceChecker(httpClient, _loggerFactory.CreateLogger<ContentRelevanceChecker>());

            // Initialise LinkVet for all tests.
            LinkVet.Initialize(_loggerFactory, _relevanceChecker);
        }

        #region Helpers
        private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock.Protected()
                       .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                       .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => responder(req));
            return new HttpClient(handlerMock.Object);
        }

        /// <summary>
        /// Simple in‑memory <see cref="HttpMessageHandler"/> used by the relevance checker.
        /// </summary>
        private sealed class StubMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;
            public StubMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) => _responder = responder;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(_responder(request, cancellationToken));
        }
        #endregion

        [Fact]
        public async Task PreferredDomain_AllRequestsFail_StillAllowed()
        {
            var http = CreateHttpClient(_ => throw new HttpRequestException());
            var ok = await LinkVet.IsAcceptableAsync("https://ocw.mit.edu/index.htm", "Article", "lesson topic", http, CancellationToken.None);
            Assert.True(ok);
        }

        [Fact]
        public async Task NonPreferredDomain_AllRequestsFail_Rejected()
        {
            var http = CreateHttpClient(_ => throw new HttpRequestException());
            var ok = await LinkVet.IsAcceptableAsync("https://example.com/bad", "Article", "lesson topic", http, CancellationToken.None);
            Assert.False(ok);
        }

        [Fact]
        public async Task HeadOk_ValidMime_AllowsLink()
        {
            var http = CreateHttpClient(_ =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new StringContent(string.Empty);
                resp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return resp;
            });
            var ok = await LinkVet.IsAcceptableAsync("https://medium.com/p/abc", "Article", "lesson topic", http, CancellationToken.None);
            Assert.True(ok);
        }

        [Fact]
        public async Task HeadOk_InvalidMime_RejectsLink()
        {
            var http = CreateHttpClient(_ =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new StringContent(string.Empty);
                resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return resp;
            });
            var ok = await LinkVet.IsAcceptableAsync("https://medium.com/p/abc", "Article", "lesson topic", http, CancellationToken.None);
            Assert.False(ok);
        }

        [Fact]
        public async Task HeadFails_GetOk_ValidMime_AllowsLink()
        {
            var http = CreateHttpClient(req =>
            {
                if (req.Method == HttpMethod.Head) throw new HttpRequestException();
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new StringContent(string.Empty);
                resp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return resp;
            });
            var ok = await LinkVet.IsAcceptableAsync("https://developer.mozilla.org/docs/Web/API", "Article", "lesson topic", http, CancellationToken.None);
            Assert.True(ok);
        }

        [Fact]
        public async Task PreferredVideoHost_RequestedArticle_Rejects()
        {
            var http = CreateHttpClient(_ =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new StringContent(string.Empty);
                resp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return resp;
            });
            var ok = await LinkVet.IsAcceptableAsync("https://youtube.com/watch?v=123", "Article", "lesson topic", http, CancellationToken.None);
            Assert.False(ok);
        }

        [Theory]
        [InlineData("invalid-url", "Article")]
        [InlineData("", "Video")]
        public async Task InvalidUrl_ReturnsFalse(string url, string type)
        {
            var http = CreateHttpClient(_ => throw new InvalidOperationException());
            var ok = await LinkVet.IsAcceptableAsync(url, type, "topic", http, CancellationToken.None);
            Assert.False(ok);
        }
    }
}
