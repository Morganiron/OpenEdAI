﻿using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenEdAI.API.Data;

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

        protected ClaimsPrincipal GetMockUser(string userId = "student-003", string username = "Student Three")
        {
            // Regular student user
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("sub", userId),
                new Claim("username", username)
            }, "mock");

            return new ClaimsPrincipal(identity);
        }

        protected ClaimsPrincipal GetMockAdmin(string adminId = "admin-001")
        {
            // Admin user: include the 'AdminGroup' claim
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("sub", adminId),
                new Claim("cognito:groups", "AdminGroup")
            }, "mock");

            return new ClaimsPrincipal(identity);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
