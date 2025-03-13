using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using OpenEdAI.Data;

namespace OpenEdAI.Tests.TestHelpers
{
    public abstract class BaseTest : IDisposable
    {
        protected readonly ApplicationDbContext _context;
        protected readonly ControllerBase _testController;

        public BaseTest()
        {
            // Create a single shared DbContext instance
            _context = InMemoryDbContextFactory.Create();
        }

        protected ClaimsPrincipal GetMockUser()
        {
            return new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "Admin-001"),
                new Claim("cognito:groups", "AdminGroup")
            }, "mock"));
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
