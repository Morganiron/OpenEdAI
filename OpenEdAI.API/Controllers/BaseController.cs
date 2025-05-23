﻿using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenEdAI.API.Controllers
{
    /// <summary>
    /// BaseController is a base class for all controllers in the application.
    /// </summary>
    [ApiController]
    [Authorize]
    public class BaseController : ControllerBase
    {
        protected string GetUserIdFromToken()
        {
            // The method first checks for an Authorization header, if present and valid, it extracts the token and reads the "sub" claim.
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            // If the Authorization header is present and starts with "Bearer", extract the token
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                var userId = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return userId;
                }
            }

            // Fallback: If no valid token is found in the header (as in tests using HttpContext.User),
            // fallback to returning the "sub" claim from HttpContext.User directly
            return HttpContext.User?.FindFirst("sub")?.Value;
        }

        protected bool TryValidateUserId(string expectedUserId)
        {
            var tokenUserId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(tokenUserId))
            {
                return false; // Token missing or invalid
            }
            return tokenUserId == expectedUserId; // Ensure the token calling the api is the same as the userId passed
        }

        // Check if the user is an admin
        protected bool IsAdmin()
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                var roles = jwtToken?.Claims.Where(claim => claim.Type == "cognito:groups").Select(c => c.Value).ToList();

                // Return true if the user is in the AdminGroup
                return roles != null && roles.Contains("AdminGroup");
            }
            return false;
        }
    }
}
