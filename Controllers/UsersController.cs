using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.Data;
using OpenEdAI.Models;

namespace OpenEdAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAmazonCognitoIdentityProvider _cognitoProvider;
        private readonly string _userPoolId;

        public UsersController(ApplicationDbContext context,
            IAmazonCognitoIdentityProvider cognitoProvider,
            IConfiguration configuration)
        {
            _context = context;
            _cognitoProvider = cognitoProvider;
            _userPoolId = configuration["AWS:UserPoolId"];
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/Users/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            // Fetch Cognito attributes
            var request = new AdminGetUserRequest
            {
                UserPoolId = _userPoolId,
                Username = id
            };
            var response = await _cognitoProvider.AdminGetUserAsync(request);

            // Extract preferred_username
            var displayName = response.UserAttributes.FirstOrDefault(attr => attr.Name == "preferred_username")?.Value;

            return Ok(new
            {
                user.UserID,
                DisplayName = displayName,
                user.Email,
                user.Role
            });
        }

        // PATCH: api/Users/{id}/update-name
        [HttpPatch("{id}/update-name")]
        public async Task<IActionResult> UpdateUserName(string id, [FromBody] string newName)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            // Update Cognito preferred_username
            var request = new AdminUpdateUserAttributesRequest
            {
                UserPoolId = _userPoolId,
                Username = id,
                UserAttributes = new List<AttributeType>
        {
            new AttributeType { Name = "preferred_username", Value = newName }
        }
            };

            await _cognitoProvider.AdminUpdateUserAttributesAsync(request);

            return Ok(new { message = "Display name updated successfully" });
        }

        // DELETE: api/Users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Delete user from Cognito
            var deleteRequest = new AdminDeleteUserRequest
            {
                UserPoolId = _userPoolId, // Cognito User Pool ID
                Username = id
            };

            await _cognitoProvider.AdminDeleteUserAsync(deleteRequest);

            // Delete user from database
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }


    }
}
