namespace OpenEdAI.API.DTOs
{
    // Request body for exchanging a Cognito auth code
    public class AuthCodeExchangeRequest
    {
        public string Code { get; set; }
    }

    // Response payload containing access and id tokens
    public class AuthTokenResponse
    {
        public string AccessToken { get; set; }
        public string IdToken { get; set; }
    }
}
