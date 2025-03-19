using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenEdAI.API.Filters;

namespace OpenEdAI.API.Controllers
{
    /// <summary>
    /// BaseController is a base class for all controllers in the application.
    /// </summary>
    [ApiController]
    [Authorize]
    [AuthorizeAdmin]
    public class BaseController : ControllerBase
    {
        protected string GetUserIdFromToken()
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            // If the Authorization header is present and starts with "Bearer", extract the token
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

                // Get the user ID from the token
                return jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
            }
            // If the token is missing or invalid, return null
            return null;
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
