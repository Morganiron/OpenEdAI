using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class CustomJwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;
        private readonly TokenManager _tokenManager;
        private readonly NavigationManager _navigation;

        public CustomJwtAuthenticationStateProvider(IJSRuntime js, TokenManager tokenManager, NavigationManager navigation, AuthConfig authConfig)
        {
            _js = js;
            _tokenManager = tokenManager;
            _navigation = navigation;

            // Subscribe to token chaanges
            _tokenManager.OnTokenChanged += () =>
            {
                Console.WriteLine("[CustomJwtAuthStateProvider] Token changed, notifying auth state changed");
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            };

            // Subscribe to token refresh failures
            _tokenManager.OnTokenRefreshFailed += () =>
            {
                Console.WriteLine("[CustomJwtAuthStateProvider] Token refresh failed, logging out");
                // Redirect to the login page
                _navigation.NavigateTo(authConfig.CognitoLoginUrl, forceLoad: true);
            };
            
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Read token from localStorage
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
            Console.WriteLine($"[CustomJwtAuthStateProvider] token from local_Storage: {token}");

            if (string.IsNullOrWhiteSpace(token) || token == "null")
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

                // Check if the token is expired
                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    Console.WriteLine("[CustomJwtAuthStateProvider] Token is expired");
                    // Trigger a refresh
                    await _tokenManager.RefreshTokenAsync();

                    // Get the new token after refresh
                    token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
                    if (string.IsNullOrWhiteSpace(token) || token == "null")
                    {
                        Console.WriteLine("[CustomJwtAuthStateProvider] Token refresh failed.");
                        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                    }
                    jwt = handler.ReadJwtToken(token);
                }

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
