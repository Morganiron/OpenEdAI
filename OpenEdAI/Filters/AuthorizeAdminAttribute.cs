using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OpenEdAI.Filters
{
    /// <summary>
    /// AuthorizeAdminAttribute is an attribute that can be applied to controller actions to ensure that the user is an admin.
    /// </summary>
    public class AuthorizeAdminAttribute : Attribute, IAuthorizationFilter
    {
        
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Get the Authorization header from the request
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            // If the header is missing or doesn't start with "Bearer ",
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            
            // Extract the token from the header
            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            // Create a handler to read the token
            var handler = new JwtSecurityTokenHandler();
            
            // Read the token
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
            
            // If the token is null, return Unauthorized
            if (jwtToken == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            
            // Get the roles from the token
            var roles = jwtToken.Claims
                .Where(claim => claim.Type == "cognito:groups")
                .Select(c => c.Value)
                .ToList();

            // If the user is not an admin, return Forbidden
            if (roles == null || !roles.Contains("AdminGroup"))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
