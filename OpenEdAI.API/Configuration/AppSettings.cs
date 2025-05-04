namespace OpenEdAI.API.Configuration
{
    public class AppSettings
    {
        public AWSSettings AWS { get; set; }
        public OpenAISettings OpenAI { get; set; }
        public GoogleAPISettings GoogleAPIs { get; set; }
    }

    public class AWSSettings
    {
        public string Region { get; set; }
        public CognitoSettings Cognito { get; set; }
    }

    public class CognitoSettings
    {
        public string AppClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string Domain { get; set; }
        public string UserPoolId { get; set; }
    }

    public class OpenAISettings
    {
        public string LearningPathKey { get; set; }
    }

    public class GoogleAPISettings
    {
        public string ApiKey { get; set; }
        public string CustomSearchEngineId { get; set; }
    }
}
