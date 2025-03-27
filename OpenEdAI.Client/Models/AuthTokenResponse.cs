using System.Text.Json.Serialization;

namespace OpenEdAI.Client.Models
{
    public class AuthTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }
        [JsonPropertyName("idToken")]
        public string IdToken { get; set; }
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }
}
