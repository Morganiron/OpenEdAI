// BaseControllerTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenEdAI.API.Controllers;

namespace OpenEdAI.Tests.Tests
{
    /// <summary>
    /// A minimal controller subclass exposing BaseController's protected members for testing.
    /// </summary>
    public class TestableBaseController : BaseController
    {
        public string? PublicGetUserIdFromToken() => GetUserIdFromToken();
        public bool PublicTryValidateUserId(string expected) => TryValidateUserId(expected);
        public bool PublicIsAdmin() => IsAdmin();
    }

    public class BaseControllerTests
    {
        private readonly TestableBaseController _controller;

        public BaseControllerTests()
        {
            // Initialize controller with a blank HttpContext
            _controller = new TestableBaseController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        [Fact]
        public void GetUserIdFromToken_BearerHeader_ExtractsSub()
        {
            // Arrange: Craft a JWT with a "sub" claim and put it in the Authorization header
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.WriteToken(new JwtSecurityToken(
                claims: new[] { new Claim("sub", "user123") }
            ));
            _controller.HttpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";

            // Act: call the method
            var result = _controller.PublicGetUserIdFromToken();

            // Assert: check that the "sub" claim was extracted correctly
            Assert.Equal("user123", result);
        }

        [Fact]
        public void GetUserIdFromToken_UserClaimFallback_ExtractsSubFromUser()
        {
            // Arrange: No header, but HttpContext.User has a "sub" claim
            var ctx = new DefaultHttpContext();
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", "user456")
            }, "mock"));
            _controller.ControllerContext.HttpContext = ctx;

            // Act: call the method
            var result = _controller.PublicGetUserIdFromToken();

            // Assert: check that the "sub" claim was extracted from HttpContext.User
            Assert.Equal("user456", result);
        }

        [Fact]
        public void TryValidateUserId_MatchingUserId_ReturnsTrue()
        {
            // Arrange: HttpContext.User has "sub" = match123
            var ctx = new DefaultHttpContext();
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", "match123")
            }, "mock"));
            _controller.ControllerContext.HttpContext = ctx;

            // Act: call the method with the same userId
            var isValid = _controller.PublicTryValidateUserId("match123");

            // Assert: check that the method returned true
            Assert.True(isValid);
        }

        [Fact]
        public void TryValidateUserId_NonMatchingUserId_ReturnsFalse()
        {
            // Arrange: HttpContext.User has "sub" = a, but we expect "b"
            var ctx = new DefaultHttpContext();
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", "a")
            }, "mock"));
            _controller.ControllerContext.HttpContext = ctx;

            // Act: call the method with a different userId
            var isValid = _controller.PublicTryValidateUserId("b");

            // Assert: check that the method returned false
            Assert.False(isValid);
        }

        [Theory]
        [InlineData("AdminGroup", true)]
        [InlineData("OtherGroup", false)]
        public void IsAdmin_VariousGroups_ReturnsExpected(string group, bool expected)
        {
            // Arrange: Create a JWT containing a cognito:groups claim
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.WriteToken(new JwtSecurityToken(
                claims: new[] { new Claim("cognito:groups", group) }
            ));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = $"Bearer {jwt}";
            _controller.ControllerContext.HttpContext = ctx;

            // Act: call the method
            var isAdmin = _controller.PublicIsAdmin();

            // Assert: check that the method returned the expected result
            Assert.Equal(expected, isAdmin);
        }

        [Fact]
        public void IsAdmin_NoHeader_ReturnsFalse()
        {
            // Arrange: No Authorization header at all

            // Act: call the method
            var isAdmin = _controller.PublicIsAdmin();

            // Assert: check that the method returned false
            Assert.False(isAdmin);
        }
    }
}
