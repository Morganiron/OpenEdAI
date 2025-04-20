using System.Text;
using System.Text.Json;
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

        public async Task<List<LessonSearchPlanDTO>> GeneratePlanAsync(
            CoursePersonalizationInput userInput,
            CoursePlanDTO coursePlan,
            StudentProfileDTO profile,
            CancellationToken ct)
        {
            // Build prompts
            var systemPrompt = BuildSearchPlanSystemPrompt(profile, userInput);

            var specialNeeds = string.IsNullOrWhiteSpace(profile.SpecialConsiderations) ? "None" : profile.SpecialConsiderations;
            var additionalConsiderations = string.IsNullOrWhiteSpace(profile.AdditionalConsiderations) ? "None" : profile.AdditionalConsiderations;
            var additionalInformation = string.IsNullOrWhiteSpace(userInput.AdditionalContext) ? "None" : userInput.AdditionalContext;

            var userPrompt = new StringBuilder()
                .AppendLine("### User Profile ###")
                .AppendLine($"User Education Level: {profile.EducationLevel}")
                .AppendLine($"Special Education Needs: {specialNeeds}")
                .AppendLine($"Additonal Considerations: {additionalConsiderations}")
                .AppendLine()
                .AppendLine("### Course Details ###")
                .AppendLine($"Course Topic: {coursePlan.Title}")
                .AppendLine($"Course Description: {coursePlan.Description}")
                .AppendLine($"Course Tags: {string.Join(", ", coursePlan.Tags)}")
                .AppendLine($"Experience Level with Topic: {userInput.ExperienceLevel}")
                .AppendLine($"Additional Information: {additionalInformation}")
                .AppendLine()
                .AppendLine("Lessons (as JSON array; each object includes Title, Description, and Tags):")
                .AppendLine(JsonSerializer.Serialize(
                    coursePlan.Lessons.Select(l => new {
                        LessonTitle = l.Title,
                        LessonDescription = l.Description,
                        Tags = l.Tags
                    }),
                    new JsonSerializerOptions { WriteIndented = false }
                ))
                .ToString();

            // Call OpenAI
            var messages = new[]
            {
                new Message(Role.System, systemPrompt),
                new Message(Role.User,  userPrompt)
            };
            var chatReq = new ChatRequest(messages, Model.GPT4oMini.Id, temperature: 0.5);
            var resp = await _ai.ChatEndpoint.GetCompletionAsync(chatReq, cancellationToken: ct);

            // clean AI output
            var raw = resp.Choices[0].Message.Content.GetRawText();
            var json = NormalizePayload(raw);

            try
            {
                return JsonSerializer.Deserialize<List<LessonSearchPlanDTO>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Strict parse failed – using fallback parser.");
                return FallbackParse(json);
            }
        }

        // Unwrap quotes and remove ``` fences
        private static string NormalizePayload(string s)
        {
            s = s.Trim();
            if (s.StartsWith('"') && s.EndsWith('"'))
                s = JsonSerializer.Deserialize<string>(s) ?? s;

            if (s.StartsWith("```"))
            {
                var lines = s.Split('\n').ToList();
                lines.RemoveAt(0);
                if (lines.Count > 0 && lines[^1].StartsWith("```"))
                    lines.RemoveAt(lines.Count - 1);
                s = string.Join('\n', lines).Trim();
            }
            return s;
        }

        // Tolerant parser for messy AI JSON
        private static List<LessonSearchPlanDTO> FallbackParse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Root element is not an array.");

            var lessons = new List<LessonSearchPlanDTO>();
            foreach (var lessonEl in root.EnumerateArray())
            {
                var title = lessonEl.GetProperty("LessonTitle").GetString() ?? "Untitled";
                var queries = new List<SearchQueryDTO>();
                foreach (var qEl in lessonEl.GetProperty("Queries").EnumerateArray())
                {
                    if (qEl.ValueKind == JsonValueKind.Object && qEl.TryGetProperty("Provider", out _))
                    {
                        queries.Add(JsonSerializer.Deserialize<SearchQueryDTO>(
                            qEl.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);
                    }
                    else if (qEl.ValueKind == JsonValueKind.Object)
                    {
                        var val = qEl.EnumerateObject().First().Value.GetString();
                        if (val != null) AddLooseString(val, queries);
                    }
                    else if (qEl.ValueKind == JsonValueKind.String)
                    {
                        AddLooseString(qEl.GetString()!, queries);
                    }
                }
                lessons.Add(new LessonSearchPlanDTO { LessonTitle = title, Queries = queries });
            }
            return lessons;
        }

        // Convert loose string to SearchQueryDTO
        private static void AddLooseString(string raw, ICollection<SearchQueryDTO> list)
        {
            var isCustom = raw.Contains("-site:");
            var provider = isCustom ? "CustomSearch" : "YouTube";
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var excludes = parts.Where(p => p.StartsWith("-site:")).ToList();
            var queryText = string.Join(' ', parts.Where(p => !p.StartsWith("-site:")));
            list.Add(new SearchQueryDTO
            {
                Provider = provider,
                Query = queryText,
                ExcludedSites = excludes,
                MaxResults = 2
            });
        }

        private string BuildSearchPlanSystemPrompt(StudentProfileDTO profile, CoursePersonalizationInput userInput)
        {
            var sb = new StringBuilder();
            var prefs = profile.PreferredContentTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Header + instructions
            sb.AppendLine($"### System Guidance — Student at {profile.EducationLevel} level" +
                          $" who is at the {userInput.ExperienceLevel} experience level" +
                          (!string.IsNullOrWhiteSpace(profile.SpecialConsiderations)
                              ? $" with special considerations: {profile.SpecialConsiderations}."
                              : "."));
            sb.AppendLine("You are an expert at crafting search queries for free, high‑quality educational content.");
            sb.AppendLine("Produce a JSON array matching this schema exactly (no extra text):");

            // Build the schema array dynamically
            sb.AppendLine("[");
            sb.AppendLine("{");
            sb.AppendLine("\"LessonTitle\": string,");
            sb.AppendLine("\"Queries\": [");

            // Collect each slot
            var slots = new List<string>();

            if (prefs.Contains("Video tutorials"))
            {
                // 2 YouTube slots
                slots.Add("{ \"Provider\": \"YouTube\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
                slots.Add("{ \"Provider\": \"YouTube\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
                // 2 CustomSearch video slots
                slots.Add("{ \"Provider\": \"CustomSearch\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
                slots.Add("{ \"Provider\": \"CustomSearch\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
            }

            if (prefs.Contains("Articles"))
            {
                // 2 Article slots
                slots.Add("{ \"Provider\": \"CustomSearch\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
                slots.Add("{ \"Provider\": \"CustomSearch\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
            }

            if (prefs.Contains("Discussion forums"))
            {
                // 2 Forum slots
                slots.Add("{ \"Provider\": \"CustomSearch\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
                slots.Add("{ \"Provider\": \"CustomSearch\", \"Query\": string, \"ExcludedSites\": [string], \"MaxResults\": 2 }");
            }

            // Emit them with commas
            for (int i = 0; i < slots.Count; i++)
            {
                var line = slots[i] + (i < slots.Count - 1 ? "," : "");
                sb.AppendLine(line);
            }

            sb.AppendLine("]");
            sb.AppendLine("}");
            sb.AppendLine("]");

            // 3) Rest of your guidance unchanged
            sb.AppendLine("### Additional Guidance");
            sb.AppendLine("0. Adjust query complexity and phrasing to match a student at the " + profile.EducationLevel + " level" +
                          (!string.IsNullOrWhiteSpace(profile.SpecialConsiderations)
                              ? $" and account for special considerations: {profile.SpecialConsiderations}."
                              : "."));
            sb.AppendLine("1. Evaluate lesson topic complexity against the student's education level and special considerations.");
            sb.AppendLine("2. Prefer links written for the appropriate comprehension level. Avoid overly academic content unless profile indicates advanced capability.");
            sb.AppendLine("3. Example Query Patterns:");
            sb.AppendLine("   * \"{LessonTitle} tutorial for {EducationLevel}\"");
            sb.AppendLine("   * \"{LessonTitle} videos for people with {SpecialConsiderations}\"");
            sb.AppendLine("   * \"Intro to {LessonTitle} for {ExperienceLevel}\"");
            sb.AppendLine("4. Avoid returning PDFs or research-heavy papers unless explicitly relevant.");
            sb.AppendLine("5. Ensure result diversity but maintain relevance.");
            sb.AppendLine("6. IMPORTANT: Use each Lesson's Tags array to provide better context for that lesson's search queries.");
            sb.AppendLine("7. **For each lesson**, generate exactly **two** queries of each requested content type:");
            // Dynamically add the instructions for the preferred content types
            if (prefs.Contains("Video tutorials"))
                sb.AppendLine("   - **Video tutorials:** 2 × YouTube + 2 × CustomSearch video (filetype:mp4 OR \"watch?v=\" OR vimeo.com)");
            if (prefs.Contains("Articles"))
                sb.AppendLine("   - **Articles:** 2 × CustomSearch article (with exclusions -site:youtube.com -site:vimeo.com -site:dailymotion.com -site:facebook.com -site:.social)");
            if (prefs.Contains("Discussion forums"))
                sb.AppendLine("   - **Discussion forums:** 2 × CustomSearch forum (same exclusions as Articles)");
            
            sb.AppendLine();

            return sb.ToString();
        }

    }
}
