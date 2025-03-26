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
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public AuthController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _http = httpClientFactory.CreateClient();
        }

        // POST: /auth/exchange - Exchange Cognito auth code for tokens
        [HttpPost("exchange")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthTokenResponse>> ExchangeCode([FromBody] AuthCodeExchangeRequest request)
        {
            // debug
            Console.WriteLine("Entered ExchangeCode endpoint.");

            var clientId = _config["AWS:Cognito:AppClientId"];
            var clientSecret = _config["AWS:Cognito:ClientSecret"];
            var redirectUri = _config["AWS:Cognito:RedirectUri"];
            var tokenEndpoint = $"https://{_config["AWS:Cognito:Domain"]}/oauth2/token";

            Console.WriteLine($"\n\nAWS:Cognito:AppClientId = {clientId}\nAWS:Cognito:ClientSecret = {clientSecret}\nAWS:Cognito:RedirectUri = {redirectUri}\ntokenEndpoint = {tokenEndpoint}\n\n", LogLevel.Debug);

            // Prepare Basic Auth credentials for Cognito
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            // Build request body for token exchange
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", request.Code },
                { "client_id", clientId },
                { "redirect_uri", redirectUri }
            });

            var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
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

            Console.WriteLine("Parsed AccessToken: " + accessToken);
            Console.WriteLine("Parsed IdToken: " + idToken);

            return new AuthTokenResponse
            {
                AccessToken = accessToken ?? "",
                IdToken = idToken ?? ""
            };
        }
    }
}
