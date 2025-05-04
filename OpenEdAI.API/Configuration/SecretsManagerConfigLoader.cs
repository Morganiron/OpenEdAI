using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;

namespace OpenEdAI.API.Configuration
{
    internal static class SecretsManagerConfigLoader
    {
        
        internal static async Task<Dictionary<string, string>> LoadSecretsAsync()
        {
            // Dictionary to store all secrets
            var config = new Dictionary<string, string>();

            using var client = new AmazonSecretsManagerClient();

            // Add all expected secret paths (matches setup from AWS Secrets Manager)
            var secretNames = new[]
            {
                "dev/OpenEdAI/AppSettings"
            };
            // Iterate through each secret path
            foreach (var secretName in secretNames)
            {
                // Get the secret value from Secrets Manager
                var request = new GetSecretValueRequest { SecretId = secretName };
                var response = await client.GetSecretValueAsync(request);

                // Deserialize the Json string and merge into the dictionary
                var secrets = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.SecretString);
                foreach (var kvp in secrets)
                {
                    config[kvp.Key] = kvp.Value;
                }
            }
            return config;
        }
    }
}
