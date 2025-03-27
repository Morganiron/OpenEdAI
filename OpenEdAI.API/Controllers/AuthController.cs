using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenEdAI.API.DTOs;

namespace OpenEdAI.API.Controllers
{
    [Route("auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _tokenEndpoint;

        public AuthController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _http = httpClientFactory.CreateClient();
            _clientId = config["AWS:Cognito:AppClientId"];
            _clientSecret = config["AWS:Cognito:ClientSecret"];
            _redirectUri = config["AWS:Cognito:RedirectUri"];
            _tokenEndpoint = $"https://{config["AWS:Cognito:Domain"]}/oauth2/token";
        }

        // POST: /auth/exchange - Exchange Cognito auth code for tokens
        [HttpPost("exchange")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthTokenResponse>> ExchangeCode([FromBody] AuthCodeExchangeRequest request)
        {
            // debug
            Console.WriteLine("Entered ExchangeCode endpoint.");

            Console.WriteLine($"\n\nAWS:Cognito:AppClientId = {_clientId}\nAWS:Cognito:ClientSecret = {_clientSecret}\nAWS:Cognito:RedirectUri = {_redirectUri}\ntokenEndpoint = {_tokenEndpoint}\n\n", LogLevel.Debug);

            // Prepare Basic Auth credentials for Cognito
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

            // Build request body for token exchange
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", request.Code },
                { "client_id", _clientId },
                { "redirect_uri", _redirectUri }
            });

            var req = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            req.Headers.Add("Authorization", $"Basic {credentials}");
            req.Content = body;

            // Send the request to Cognito
            var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("Token exchange failed with status: " + res.StatusCode);
                return Unauthorized("Failed to exchange code for token");
            }

            // Read the raw JSON response for debugging
            var json = await res.Content.ReadAsStringAsync();
            Console.WriteLine("Token exchange response: " + json);

            // Parse response and return tokens
            var content = await res.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = content.GetProperty("access_token").GetString();
            var idToken = content.GetProperty("id_token").GetString();

            // Check if the response contains a refresh token
            string refreshToken = "";
            if (content.TryGetProperty("refresh_token", out JsonElement refreshTokenElement))
            {
                refreshToken = refreshTokenElement.GetString() ?? "";
            }

            Console.WriteLine("Parsed AccessToken: " + accessToken);
            Console.WriteLine("Parsed IdToken: " + idToken);
            Console.WriteLine("Parsed RefreshToken: " + refreshToken);

            return new AuthTokenResponse
            {
                AccessToken = accessToken ?? "",
                IdToken = idToken ?? "",
                RefreshToken = refreshToken ?? ""
            };
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthTokenResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            Console.WriteLine("Entered RefreshToken endpoint.");

            // Prepare the credentials
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

            // Build the request body for token refresh
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", request.RefreshToken },
                { "client_id", _clientId }
            });

            var req = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            req.Headers.Add("Authorization", $"Basic {credentials}");
            req.Content = body;

            var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("Token refresh failed with status: " + res.StatusCode);
                return Unauthorized("Failed to refresh token");
            }

            var json = await res.Content.ReadAsStringAsync();
            Console.WriteLine("Refresh token response: " + json);

            var content = await res.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = content.GetProperty("access_token").GetString();
            var idToken = content.GetProperty("id_token").GetString();

            var refreshToken = content.TryGetProperty("refresh_token", out JsonElement rtElement) ? rtElement.GetString() : null;

            Console.WriteLine("Parsed refreshed AccessToken: " + accessToken);
            Console.WriteLine("Parsed refreshed IdToken: " + idToken);

            return new AuthTokenResponse
            {
                AccessToken = accessToken ?? "",
                IdToken = idToken ?? "",
                RefreshToken = refreshToken ?? ""
            };
        }
    }
}
