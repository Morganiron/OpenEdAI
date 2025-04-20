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
        private readonly ILogger<CustomJwtAuthenticationStateProvider> _logger;

        // String to store the previous token to detect changes
        private string? _previousToken;

        public CustomJwtAuthenticationStateProvider(IJSRuntime js, TokenManager tokenManager, NavigationManager navigation, AuthConfig authConfig, ILogger<CustomJwtAuthenticationStateProvider> logger)
        {
            _js = js;
            _tokenManager = tokenManager;
            _navigation = navigation;

            // Subscribe to token chaanges
            _tokenManager.OnTokenChanged += async () =>
            {
                var token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");

                if (token != _previousToken)
                {
                    _previousToken = token;
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                }
            };

            // Subscribe to token refresh failures
            _tokenManager.OnTokenRefreshFailed += () =>
            {
                // Redirect to the login page
                _navigation.NavigateTo(authConfig.CognitoLoginUrl, forceLoad: true);
            };
            _logger = logger;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Read token from localStorage
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");

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
                    // Trigger a refresh
                    await _tokenManager.RefreshTokenAsync();

                    // Get the new token after refresh
                    token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
                    if (string.IsNullOrWhiteSpace(token) || token == "null")
                    {
                        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                    }
                    jwt = handler.ReadJwtToken(token);
                }

                // Skip re-parsing and avoid unnecessary state changes if the token hasn't changed
                if (_previousToken == token)
                {
                    var cachedClaims = new ClaimsIdentity(ParseClaims(token), "cognito-jwt");
                    return new AuthenticationState(new ClaimsPrincipal(cachedClaims));
                }

                var identity = new ClaimsIdentity(jwt.Claims, "cognito-jwt");
                var user = new ClaimsPrincipal(identity);
                
                // Cache the current token for change detection
                _previousToken = token;
                return new AuthenticationState(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CustomJwtAuthStateProvider] Exception parsing token");

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

        // Extract claims manually for cache reuse
        private IEnumerable<Claim> ParseClaims(string jwt)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims;
        }
    }
}
