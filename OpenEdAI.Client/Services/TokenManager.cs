using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class TokenManager
    {
        private readonly IJSRuntime _js;
        private readonly HttpClient _http;
        private Timer _refreshTimer;
        private readonly ILogger<TokenManager> _logger;

        // Event that other components/services can subscribe to, to see when the token changes
        public event Action OnTokenChanged;
        // Event for when a token refresh fails (no refresh token or other error)
        public event Action OnTokenRefreshFailed;

        public TokenManager(IJSRuntime js, HttpClient http, ILogger<TokenManager> logger)
        {
            _js = js;
            _http = http;
            _logger = logger;
        }

        // Gets the token from localStorage
        public async Task<string> GetTokenAsync()
        {
            return await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
        }

        // Check that the token is valid
        public bool IsTokenValid(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token == "null")
            {
                return false;
            }
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // Check if the token is expired
                return jwt.ValidTo > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token:");
                return false;
            }
        }

        // Initialize the token manager to ensure the token is valid and schedule a refresh
        public async Task InitializeAsync()
        {
            var token = await GetTokenAsync();
            if (IsTokenValid(token))
            {
                ScheduleRefresh(token);
            }
            else
            {
                await ClearTokensAsync();
            }
        }

        // Stores the token and schedules a refresh
        public async Task SetTokenAsync(string token)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "access_token", token);
            OnTokenChanged?.Invoke();
            ScheduleRefresh(token);
        }

        // Parses the token to determine when to schedule a refresh (5 minutes before expiration)
        private void ScheduleRefresh(string token)
        {
            // Parse the token to get the expiration time
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var jwt = handler.ReadJwtToken(token);

                // The 'exp' claim is in Unix time seconds
                if (jwt.Payload.Exp.HasValue)
                {
                    var expirationTime = DateTimeOffset.FromUnixTimeSeconds(jwt.Payload.Exp.Value);
                    var now = DateTimeOffset.UtcNow;

                    // Refresh 5 minutes before expiration
                    var refreshTime = expirationTime.AddMinutes(-5);
                    var delay = refreshTime - now;
                    // Ensure the delay is at least 30 seconds
                    if (delay <= TimeSpan.Zero)
                    {
                        delay = TimeSpan.FromSeconds(30);
                    }

                    _refreshTimer?.Dispose();
                    _refreshTimer = new Timer(async _ => await RefreshTokenAsync(), null, delay, Timeout.InfiniteTimeSpan);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling token refresh:");
            }
        }

        // Calls the refresh endpoint to get a new access token (and refresh token) and updates the stored token
        public async Task RefreshTokenAsync()
        {
            try
            {
                var refreshToken = await _js.InvokeAsync<string>("localStorage.getItem", "refresh_token");
                if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken == "null")
                {
                    OnTokenRefreshFailed?.Invoke();
                    await ClearTokensAsync();
                    return;
                }

                // Build the payload for token refresh
                var payload = new { refreshToken = refreshToken };

                // Call the backend refresh endpoint
                var response = await _http.PostAsJsonAsync("auth/refresh", payload);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponseJson = await response.Content.ReadAsStringAsync();

                    // Deserialize the refreshed tokens
                    var tokenResponse = JsonSerializer.Deserialize<AuthTokenResponse>(
                        tokenResponseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (tokenResponse != null && !string.IsNullOrWhiteSpace(tokenResponse.AccessToken) && tokenResponse.AccessToken != "null")
                    {
                        // Store the new token and reschedule the refresh
                        await SetTokenAsync(tokenResponse.AccessToken);

                        // Update the refresh token if one is returned
                        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken) && tokenResponse.RefreshToken != "null")
                        {
                            await _js.InvokeVoidAsync("localStorage.setItem", "refresh_token", tokenResponse.RefreshToken);
                        }
                    }
                    else
                    {
                        OnTokenRefreshFailed?.Invoke();
                        await ClearTokensAsync();
                    }
                }
                else
                {
                    _logger.LogWarning("Token refresh failed with status: " + response.StatusCode);
                    OnTokenRefreshFailed?.Invoke();
                    await ClearTokensAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token:");
                OnTokenRefreshFailed?.Invoke();
                await ClearTokensAsync();
            }
        }

        // Clears the access and refresh tokens from storage
        public async Task ClearTokensAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "access_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "refresh_token");
            _refreshTimer?.Dispose();
            OnTokenChanged?.Invoke();
        }
    }
}
