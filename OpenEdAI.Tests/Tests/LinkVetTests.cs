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
    public class LinkVetTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ContentRelevanceChecker _relevance;
        private readonly IYouTubeHeuristics _ytHeuristicsStub; // interface‑based stub

        public LinkVetTests()
        {
            // --- stub HttpClient used by ContentRelevanceChecker ----------------------
            var stubHtmlHandler = new StubHandler((_, __) =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<title>lesson topic</title><p>sample</p>")
                };
                msg.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return msg;
            });
            var relevanceHttp = new HttpClient(stubHtmlHandler);

            // --- logger / relevance -----------------------------------------------
            _loggerFactory = LoggerFactory.Create(b => { });
            _relevance = new ContentRelevanceChecker(relevanceHttp,
                           _loggerFactory.CreateLogger<ContentRelevanceChecker>());

            // --- YouTube heuristics stub (always passes) --------------------------
            var ytMock = new Mock<IYouTubeHeuristics>(MockBehavior.Strict);
            ytMock.Setup(y => y.IsRelevantAsync(It.IsAny<string>(),
                                                 It.IsAny<string>(),
                                                 It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);
            _ytHeuristicsStub = ytMock.Object;

            // --- initialise LinkVet once for all tests ----------------------------
            LinkVet.Initialize(_loggerFactory, _relevance, _ytHeuristicsStub);
        }

        #region helper types / methods

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _fn;
            public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> fn) => _fn = fn;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t)
                => Task.FromResult(_fn(r, t));
        }

        private static HttpClient FakeClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                         ItExpr.IsAny<HttpRequestMessage>(),
                         ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage r, CancellationToken _) => responder(r));
            return new HttpClient(mock.Object);
        }

        #endregion

        [Fact]  // preferred domain, network failures → still allowed
        public async Task PreferredDomain_AllRequestsFail_Allowed()
        {
            var http = FakeClient(_ => throw new HttpRequestException());
            Assert.True(await LinkVet.IsAcceptableAsync(
                "https://ocw.mit.edu/index.htm",
                "Article",
                "lesson topic",
                http,
                CancellationToken.None));
        }

        [Fact]  // non-preferred domain, network failures → reject
        public async Task NonPreferredDomain_AllRequestsFail_Rejected()
        {
            var http = FakeClient(_ => throw new HttpRequestException());
            Assert.False(await LinkVet.IsAcceptableAsync(
                "https://example.com/page",
                "Article",
                "lesson topic",
                http,
                CancellationToken.None));
        }

        [Fact]  // HEAD succeeds + valid mime → allow
        public async Task HeadOk_ValidMime_Allowed()
        {
            var http = FakeClient(_ =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new StringContent(string.Empty);
                msg.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return msg;
            });
            Assert.True(await LinkVet.IsAcceptableAsync(
                "https://medium.com/p/abc",
                "Article",
                "lesson topic",
                http,
                CancellationToken.None));
        }

        [Theory]  // bad URLs → false
        [InlineData("", "Article")]
        [InlineData("bad-url", "Video")]
        public async Task InvalidUrl_ReturnsFalse(string url, string type)
        {
            var http = FakeClient(_ => throw new InvalidOperationException());
            Assert.False(await LinkVet.IsAcceptableAsync(
                url,
                type,
                "topic",
                http,
                CancellationToken.None));
        }
    }
}
