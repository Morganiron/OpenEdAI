using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.Data;
using OpenEdAI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace OpenEdAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAmazonCognitoIdentityProvider _cognitoProvider;

        public UsersController(ApplicationDbContext context, IAmazonCognitoIdentityProvider cognitoProvider)
        {
            _context = context;
            _cognitoProvider = cognitoProvider;
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

            return user;
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
                UserPoolId = "", // Cognito User Pool ID
                Username = id,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "preferred_username", Value = newName }
                }
            };

            await _cognitoProvider.AdminUpdateUserAttributesAsync(request);

            // Update local database
            user.GetType().GetProperty("Name").SetValue(user, newName);

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Username updated successfully" });
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
                UserPoolId = "", // Cognito User Pool ID
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
