using System.Text;
using System.Text.Json;
using Azure.Core.GeoJson;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using OpenEdAI.API.DTOs;

namespace OpenEdAI.API.Services
{
    public class AIDrivenSearchPlanService
    {
        private readonly OpenAIClient _ai;
        private readonly ILogger _logger;

        public AIDrivenSearchPlanService(OpenAIClient ai, ILogger<AIDrivenSearchPlanService> logger)
        {
            _ai = ai;
            _logger = logger;
        }

        public async Task<List<LessonSearchPlanDTO>> GeneratePlanAsync(CoursePersonalizationInput userInput, CoursePlanDTO coursePlan, StudentProfileDTO profile, CancellationToken ct)
        {
            // Build system prompt conditionally
            var systemPrompt = BuildSearchPlanSystemPrompt(profile);

            // Check if the optional fields are null or empty and set to "None" if so
            var specialNeeds = string.IsNullOrWhiteSpace(profile.SpecialConsiderations)
                ? "None"
                : profile.SpecialConsiderations;

            var additionalConsiderations = string.IsNullOrWhiteSpace(profile.AdditionalConsiderations)
                ? "None"
                : profile.AdditionalConsiderations;

            var addtionalInformation = string.IsNullOrWhiteSpace(userInput.AdditionalContext)
                ? "None"
                : userInput.AdditionalContext;

            // Build user prompt
            var userPrompt = new StringBuilder()
                .AppendLine("### User Profile ###")
                .AppendLine($"User Education Level: {profile.EducationLevel}")
                .AppendLine($"Special Education Needs: {specialNeeds}")
                .AppendLine($"Additonal Considerations: {additionalConsiderations}")
                .AppendLine()
                .AppendLine("### Course Details ###")
                .AppendLine($"Course Topic: {coursePlan.Title}")
                .AppendLine($"Course Description: {coursePlan.Description}")
                .AppendLine($"Experience Level with Topic: {userInput.ExperienceLevel}")
                .AppendLine($"Additional Information: {addtionalInformation}")
                .AppendLine()
                .AppendLine("Lessons (as JSON array):")
                .AppendLine(JsonSerializer.Serialize(coursePlan.Lessons))
                .AppendLine()
                .Append("Generate the search plan now.")
                .ToString();

            // Build request
            var messages = new[]
            {
                new Message(Role.System, systemPrompt),
                new Message(Role.User, userPrompt)
            };
            var chatReq = new ChatRequest(messages, model: Model.GPT4oMini.Id, temperature: 0.3);

            var resp = await _ai.ChatEndpoint.GetCompletionAsync(chatReq, cancellationToken: ct);

            // Get the content from the response
            var adjustedPlanJson = resp.Choices[0].Message.Content;

            // Convert the JSON element to fixed string
            string fixedJson = adjustedPlanJson.ValueKind == JsonValueKind.String
                ? adjustedPlanJson.GetString()
                : adjustedPlanJson.GetRawText().Trim();

            // If the string starts with a code fence, remove it
            if (fixedJson.StartsWith("```"))
            {
                // Split lines
                var lines = fixedJson
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                    .ToList();

                // Remove the first and last lines (the code fence)
                if (lines.Count >= 2)
                {
                    lines.RemoveAt(0); // Remove the first line
                    if (lines.Last().StartsWith("```"))
                        lines.RemoveAt(lines.Count - 1); // Remove the last line
                }

                fixedJson = string.Join('\n', lines).Trim();
            }

            // Deserialize the JSON to a list of LessonSearchPlanDTO
            try
            {
                return JsonSerializer.Deserialize<List<LessonSearchPlanDTO>>(fixedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<LessonSearchPlanDTO>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize the search plan JSON: {Json}", fixedJson);
                return new List<LessonSearchPlanDTO>();
            }
        }

        private string BuildSearchPlanSystemPrompt(StudentProfileDTO profile)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert at crafting search queries for free, high-quality educational content.");
            sb.AppendLine("Produce a JSON array matching this schema exactly (no extra text):");
            sb.AppendLine("[");
            sb.AppendLine("  {");
            sb.AppendLine("    \"LessonTitle\": string,");
            sb.AppendLine("    \"Queries\": [");
            sb.AppendLine("      {");
            sb.AppendLine("        \"Provider\": \"YouTube\"|\"CustomSearch\",");
            sb.AppendLine("        \"Query\": string,");
            sb.AppendLine("        \"ExcludedSites\": [string],");
            sb.AppendLine("        \"MaxResults\": 4");
            sb.AppendLine("      }");
            sb.AppendLine("    ]");
            sb.AppendLine("  }");
            sb.AppendLine(" ]");

            var prefs = profile.PreferredContentTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (prefs.Contains("Video tutorials"))
            {
                sb.AppendLine();
                sb.AppendLine("### Video Queries ###");
                sb.AppendLine("1) Build a string to query YouTube (Provider=\"YouTube\") for content that is appropriate based on the user's profile, lesson title, lesson description, and any additional context provided.");
                sb.AppendLine("2) Build a string to query Custom Search (Provider=\"CustomSearch\") for content that is appropriate based on the user's profile, lesson title, lesson description, and any additional context provided. Include qualifiers to ensure this search is only for videos(eg. filetype:mp4 OR \"watch?v=\" OR vimeo.com). Include \"-site:youtube.com\" in the \"ExcludedSites\" section of the JSON array for this lesson.");
            }

            if (prefs.Contains("Articles"))
            {
                sb.AppendLine();
                sb.AppendLine("### Article Queries ###");
                sb.AppendLine("Build a string to query Custom Search (Provider=\"CustomSearch\") for content that is appropriate based on the user's profile, lesson title, lesson description, and any additional context provided.");
                sb.AppendLine("Include exclusions for common video sites (e.g., \"-site:youtube.com -site:vimeo.com -site:dailymotion.com\") in the \"ExcludedSites\" section of the JSON array to avoid video results.");
                sb.AppendLine("Prioritize trusted sources that provide free, high-quality educational content. Such as:");
                sb.AppendLine("ocw.mit.edu, khanacademy.org, edx.org, coursera.org, openlearn.open.ac.uk, saylor.org, oercommons.org, ted.com/ted‑ed, freecodecamp.org, developer.mozilla.org.");
            }

            if (prefs.Contains("Discussion forums"))
            {
                sb.AppendLine();
                sb.AppendLine("### Discussion Forum Queries ###");
                sb.AppendLine("Build a string to query Custom Search (Provider=\"CustomSearch\") for content that is appropriate based on the user's profile, lesson title, lesson description, and any additional context provided.");
                sb.AppendLine("Include exclusions for common video sites (e.g., \"-site:youtube.com -site:vimeo.com -site:dailymotion.com\") in the \"ExcludedSites\" section of the JSON array to avoid video results for this lesson.");
                sb.AppendLine("Prioritize trusted discussion forums that provide free, high-quality educational content. Such as:");
                sb.AppendLine("reddit.com, stackoverflow.com, quora.com, dev.to, codeproject.com, site:github.com, and other community Q&A sites.");
            }
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
