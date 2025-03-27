namespace OpenEdAI.Client.Models
{
    public class AuthConfig
    {
        public string CognitoDomain { get; set; }
        public string AppClientId { get; set; }
        public string RedirectUri { get; set; }
        public string PostLogoutRedirectUri { get; set; }
        public string ResponseType { get; set; }
        public string Scope { get; set; }

        public string CognitoLoginUrl =>
            $"{CognitoDomain}/login?" +
            $"client_id={AppClientId}&" +
            $"response_type={ResponseType}&" +
            $"scope={Scope}&" +
            $"redirect_uri={Uri.EscapeDataString(RedirectUri)}";

        public string CognitoLogoutUrl =>
            $"{CognitoDomain}/logout?" +
            $"client_id={AppClientId}&" +
            $"logout_uri={Uri.EscapeDataString(PostLogoutRedirectUri)}";
    }
}
