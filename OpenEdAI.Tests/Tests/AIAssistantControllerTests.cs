using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;
using OpenEdAI.API.Services;
using OpenEdAI.Tests.TestHelpers;
using Xunit;

namespace OpenEdAI.Tests.Tests
{
    public class AIAssistantControllerTests: IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IBackgroundTaskQueue> _mockQueue;
        private readonly Mock<IContentSearchService> _mockSearch;
        private readonly Mock<IServiceScopeFactory> _mockScope;
        private readonly Mock<ILogger<AIAssistantController>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfig;

        public AIAssistantControllerTests() 
        {
            _context = InMemoryDbContextFactory.Create();
            _mockQueue = new();
            _mockSearch = new();
            _mockScope = new();
            _mockLogger = new();
            _mockConfig = new();
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public void Ctor_MissingApiKey_Throws()
        {
            // Arrange: no OpenAi key in config
            _mockConfig.Setup(c => c["OpenAi:LearningPathKey"]).Returns<string>(null);

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() =>
                new AIAssistantController(
                    _context,
                    _mockConfig.Object,
                    _mockLogger.Object,
                    _mockQueue.Object,
                    _mockSearch.Object,
                    _mockScope.Object));
        }

        [Fact]
        public async Task GenerateCourse_NoToken_ReturnsUnauthorized()
        {
            // Arrange: Set up the mock config with a valid API key
            _mockConfig.Setup(c => c["OpenAi:LearningPathKey"]).Returns("sk-test-key");
            
            var ctrl = new AIAssistantController(
                _context,
                _mockConfig.Object,
                _mockLogger.Object,
                _mockQueue.Object,
                _mockSearch.Object,
                _mockScope.Object);
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext() // no user or header
            };

            // Act
            var result = await ctrl.GenerateCourse(new CoursePersonalizationInput());

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User ID not found in token.", unauthorized.Value);
        }

        [Fact]
        public async Task GenerateCourse_NoProfile_ReturnsBadRequest()
        {
            // Arrange: Set up the mock config with a valid API key
            _mockConfig.Setup(c => c["OpenAi:LearningPathKey"]).Returns("sk-test-key");
            
            var ctrl = new AIAssistantController(
                _context,
                _mockConfig.Object,
                _mockLogger.Object,
                _mockQueue.Object,
                _mockSearch.Object,
                _mockScope.Object);

            // Simulate a bearer token and a "sub" claim for a user that has no profile
            var userId = "student-foo";
            var ctx = new DefaultHttpContext();

            // Override BaseController.GetUserIdFromToken via HttpContext.User
            ctx.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]{
                    new System.Security.Claims.Claim("sub", userId)
                }, "mock")
            );
            ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

            // Act
            var result = await ctrl.GenerateCourse(new CoursePersonalizationInput());

            // Assert
            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("User profile not found.", badReq.Value);
        }

    }
}
