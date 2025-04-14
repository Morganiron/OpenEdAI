using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace OpenEdAI.API.Services
{
    public class AWSSecretsManagerService
    {
        private readonly IAmazonSecretsManager _client;

        public AWSSecretsManagerService()
        {
            // AWS SDK automatically uses the credentials provided by the AWS Toolkit in Visual Studio.
            _client = new AmazonSecretsManagerClient(RegionEndpoint.USEast1);
        }

        // Method to get the secret string for a given secret name
        public async Task<string> GetSecretAsync(string secretName)
        {
            var request = new GetSecretValueRequest
            {
                SecretId = secretName
            };
            try
            {
                // Retrieve the secret value from Secrets Manager
                GetSecretValueResponse response = await _client.GetSecretValueAsync(request);
                return response.SecretString;
            }
            catch (Exception ex)
            {
                // Handle any errors (e.g., secret not found, access denied)
                throw new ApplicationException($"Error retrieving secret {secretName}: {ex.Message}");
            }
        }

        // Method to retrieve and deserialize the secrets from AWS Secrets Manager
        public async Task<IDictionary<string, string>> GetAppSecretsAsync()
        {
            string secretName = "dev/OpenEdAI/Secrets";
            var secrets = await GetSecretAsync(secretName);

            // Deserialize the secrets string into key-value pairs
            var secretsDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(secrets);
            return secretsDictionary;
        }

        public async Task<string> GetOpenAiKeyAsync()
        {
            // Retrieve the secret from AWS Secrets Manager
            string secretName = "dev/OpenEdAI/LearningPageGeneration";
            var secrets = await GetSecretAsync(secretName);

            // Deserialize and return the OpenAI key
            var secretDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(secrets);
            return secretDictionary["OpenAi_LearningPathKey"];
        }
    }
}
