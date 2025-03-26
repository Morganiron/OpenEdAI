using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace OpenEdAI.Client.Services
{
    public class CustomJwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;

        public CustomJwtAuthenticationStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Read token from localStorage
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
            Console.WriteLine($"[CustomJwtAuthStateProvider] token from local_Storage: {token}");

            if (string.IsNullOrWhiteSpace(token))
            {
                // Not logged in => anonymous user
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(anonymous);
            }

            try
            {
                // Parse the token's claims
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var identity = new ClaimsIdentity(jwt.Claims, "cognito-jwt");
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CustomJwtAuthStateProvider] Exception parsing token: {ex.Message}");

                // If they token is invalid, treat as anonymous
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(anonymous);
            }
        }

        // Used to manually re-check after a login/logout event:
        public void NotifyUserAuthenticationChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
