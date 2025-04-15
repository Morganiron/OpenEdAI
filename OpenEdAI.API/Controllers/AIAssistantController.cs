using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenEdAI.API.Models;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using System.Text.Json;
using System.Text;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Numerics;

namespace OpenEdAI.API.Controllers
{
    [Route("ai")]
    [ApiController]
    public class AIAssistantController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly OpenAIClient _openAiClient;
        private readonly ILogger<AIAssistantController> _logger;

        public AIAssistantController(ApplicationDbContext context, IConfiguration configuration, ILogger<AIAssistantController> logger)
        {
            _context = context;
            _logger = logger;

            // Retrieve the API key from configuration
            var apiKey = configuration["OpenAi:LearningPathKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured");
            }

            // Initialize the OpenAI client
            _openAiClient = new OpenAIClient(new OpenAIAuthentication(apiKey));
        }

        // POST: ai/GenerateCourse
        [HttpPost("generate-course")]
        public async Task<ActionResult<CoursePlanDTO>> GenerateCourse([FromBody] CoursePersonalizationInput input)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in token.");
                return Unauthorized("User ID not found in token.");
            }

            // Retrieve the user's profile to use in the AI prompt
            var profile = await _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                _logger.LogWarning("User profile not found for user {UserId}.", userId);
                return BadRequest("User profile not found.");
            }

            // Build the prompt using the user's input and profile
            var prompt = BuildPrompt(input, profile);

            ChatResponse chatResponse;
            try
            {
                // Create a ChatRequest with a single system & user message.
                var messages = new[]
                {
                    new Message(Role.System, "You are a helpful and inclusive AI course planner. " +
                                               "Your job is to generate structured course outlines based on user input and personal learning profiles. " +
                                               "Respond only in clean, valid JSON format and strictly follow the structure provided. " +
                                               "DO NOT include any additional commentary. " +
                                               "When generating lesson tags and descriptions, make them optimized for search engines — use keywords someone might type into YouTube or Google when looking to learn that lesson. " +
                                               "Try to infer how someone with this learner's background would phrase their search queries, and reflect that in the tags and lesson titles. " +
                                               "If the topic is offensive, inappropriate, or too broad, return a JSON warning message instead."),
                    new Message(Role.User, prompt)
                };

                // 0.7 is chosen for a balanced output between determinism and creativity.
                var chatRequest = new ChatRequest(messages, model: Model.GPT4oMini.Id, temperature: 0.7);
                // Call the ChatEndpoint asynchronously
                chatResponse = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API in GenerateCourse");
                return StatusCode(500, ex.Message);
            }

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                _logger.LogError("No output received from OpenAI API in GenerateCourse.");
                return StatusCode(500, "No output received from OpenAI API.");
            }
            
            // Get the response content
            var coursePlanJson = chatResponse.Choices[0].Message.Content;

            // Get the raw text from the json element as a string
            string fixedJson = coursePlanJson.ValueKind == JsonValueKind.String
                ? coursePlanJson.GetString()
                : coursePlanJson.GetRawText();

            CoursePlanDTO coursePlan;
            try
            {
                coursePlan = JsonSerializer.Deserialize<CoursePlanDTO>(
                    fixedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                // Validate deserialization produced required fields
                if (coursePlan == null || string.IsNullOrWhiteSpace(coursePlan.Title) ||
                    coursePlan.Lessons == null || !coursePlan.Lessons.Any())
                {
                    _logger.LogError("Error deserializing course plan. Missing required fields. CouserPlan:\n{Output}", fixedJson);
                    _logger.LogError("\n\ncoursePlanJson:\n{Output}", (object)coursePlanJson);

                    throw new JsonException("Missing required fields in CoursePlanDTO.");
                }
                return Ok(coursePlan);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Deserialization error in GenerateCourse. JSON Response:\n{Output}", fixedJson);
                // Fallback warning DTO for the front-end to show in the chat component
                coursePlan = new CoursePlanDTO
                {
                    Title = "Warning: Course Plan Issue",
                    Description = $"The AI response did not generate the expected JSON format. Response received:\n{fixedJson}",
                    Lessons = new List<LessonPlanDTO>()
                };
            }

            return Ok(coursePlan);
        }

        // POST: ai/AdjustCourse
        [HttpPost("adjust-course")]
        public async Task<IActionResult> AdjustCourse([FromBody] JsonElement payload)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in token.");
                return Unauthorized("User ID not found in token.");
            }

            _logger.LogInformation("Received payload for course adjustment.");
            foreach (var prop in payload.EnumerateObject())
            {
                _logger.LogInformation("Payload property: {PropertyName}", prop.Name);
            }

            // Validate required fields exist
            if (!payload.TryGetProperty("userMessage", out JsonElement userMessageElement) ||
                !payload.TryGetProperty("previousPlan", out JsonElement previousPlanElement))
            {
                _logger.LogWarning("Missing required fields: UserMessage and/or PreviousPlan.");
                return BadRequest("Missing required fields: UserMessage and/or PreviousPlan.");
            }

            string userMessage = userMessageElement.GetString();
            string previousPlanJson = previousPlanElement.GetString();

            if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(previousPlanJson))
            {
                _logger.LogWarning("UserMessage or PreviousPlan is empty.");
                return BadRequest("UserMessage and PreviousPlan must not be empty.");
            }

            // Deserialize previous course plan
            CoursePlanDTO previousPlan;
            try
            {
                previousPlan = JsonSerializer.Deserialize<CoursePlanDTO>(
                    previousPlanJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize previous plan in AdjustCourse.");
                return BadRequest("Failed to deserialize previous plan: " + ex.Message);
            }

            // Retrieve the user's profile
            var profile = await _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                _logger.LogWarning("User profile not found for user {UserId} in AdjustCourse.", userId);
                return BadRequest("User profile not found.");
            }

            // Build the adjustment prompt
            var prompt = BuildAdjustmentPrompt(previousPlanJson, userMessage, profile);
            _logger.LogInformation("Adjustment prompt generated:\n{Prompt}", prompt);

            ChatResponse chatResponse;
            try
            {
                // Create messages for the adjustment request:
                var messages = new[]
                {
                    new Message(Role.System, "You are a helpful and inclusive AI course planner. " +
                        "Your job is to adjust an existing course plan based on user feedback. " +
                        "Respond only in clean, valid JSON format and strictly follow the provided structure. " +
                        "Do not include any additional commentary. " +
                        "If the adjustment request is unclear or the topic is inappropriate, return a JSON warning message instead."),
                    new Message(Role.User, prompt)
                };

                var chatRequest = new ChatRequest(messages, model: Model.GPT4oMini.Id, temperature: 0.7);
                chatResponse = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API for course adjustment.");
                return StatusCode(500, ex.Message);
            }

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                _logger.LogError("No output received from OpenAI API for course adjustment.");
                return StatusCode(500, "No output received from OpenAI API.");
            }

            // Get the content from the response
            var adjustedPlanJson = chatResponse.Choices[0].Message.Content;

            // Convert the JSON element to fixed string
            string fixedJson = adjustedPlanJson.ValueKind == JsonValueKind.String
                ? adjustedPlanJson.GetString()
                : adjustedPlanJson.GetRawText().Trim();


            _logger.LogInformation("Raw AI-generated adjusted output:\n{Output}", fixedJson);

            CoursePlanDTO adjustedPlan;
            try
            {
                adjustedPlan = JsonSerializer.Deserialize<CoursePlanDTO>(
                    fixedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                // Validate that deserialization produced required fields
                if (adjustedPlan == null || string.IsNullOrWhiteSpace(adjustedPlan.Title) ||
                    adjustedPlan.Lessons == null || !adjustedPlan.Lessons.Any())
                {
                    throw new JsonException("Missing required fields in CoursePlanDTO.");
                }
                return Ok(adjustedPlan);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Deserialization error in AdjustCourse. AI response: {Output}", fixedJson);
                // Fallback warning DTO so that the front-end chat always has something to display
                adjustedPlan = new CoursePlanDTO
                {
                    Title = "Warning: Adjusted Course Plan Issue",
                    Description = $"The AI response did not generate the expected JSON format. Response received:\n{fixedJson}",
                    Lessons = new List<LessonPlanDTO>()
                };
            }

            return Ok(adjustedPlan);
        }

        // POST: ai/SubmitCoursePlan
        [HttpPost("submit-course")]
        public async Task<IActionResult> SubmitCoursePlan([FromBody] SubmittedCourseDTO plan)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in token in SubmitCoursePlan.");
                return Unauthorized("User ID not found in token.");
            }

            // Retrieve the student to update HasCompletedSetup
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserID == userId);
            if (student == null)
            {
                _logger.LogWarning("Student not found for user {UserId} in SubmitCoursePlan.", userId);
                return NotFound("Student not found.");
            }

            // Update the student's profile setup status if this is the first time
            if (!student.HasCompletedSetup)
            {
                student.MarkSetupComplete();
            }

            // Validate the course plan
            if (plan == null || string.IsNullOrWhiteSpace(plan.Title) || plan.Lessons == null || !plan.Lessons.Any())
            {
                _logger.LogWarning("Invalid course plan submitted by user {UserId}.", userId);
                return BadRequest("Invalid course plan.");
            }

            // TODO: Send the course plan to the search APIs to generate content links
            // (Not saving changes for testing purposes)
            //_context.SaveChangesAsync();

            _logger.LogInformation("Course finalized successfully for user {UserId}.", userId);
            return Ok(new { message = "Course finalized successfully." });
        }

        /// <summary>
        /// Builds a prompt for adjusting an existing course plan.
        /// </summary>
        private string BuildAdjustmentPrompt(string previousPlanJson, string userMessage, StudentProfile profile)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Current Course Plan (in JSON):");
            builder.AppendLine(previousPlanJson);
            builder.AppendLine();
            builder.AppendLine("User Adjustment:");
            builder.AppendLine(userMessage);
            builder.AppendLine();
            builder.AppendLine("Consider the following user information regarding learning style, education level, special needs, and preferences:");
            builder.AppendLine($"- Education Level: {profile.EducationLevel}");
            builder.AppendLine($"- Preferred Content Types: {profile.PreferredContentTypes}");
            builder.AppendLine($"- Special Considerations: {profile.SpecialConsiderations}");
            builder.AppendLine($"- Additional Info: {profile.AdditionalConsiderations}");
            builder.AppendLine();
            builder.AppendLine("Update the course plan based on the above adjustment request and provided user details.");
            builder.AppendLine("Respond strictly in the same JSON format as the original course plan.");
            builder.AppendLine("If the adjustment request is unclear or invalid, return a JSON warning message instead.");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the prompt for generating a course plan.
        /// </summary>
        private string BuildPrompt(CoursePersonalizationInput input, StudentProfile profile)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Create a structured course on the topic: {input.Topic}");
            builder.AppendLine($"Experience Level: {input.ExperienceLevel}");
            if (!string.IsNullOrWhiteSpace(input.AdditionalContext))
            {
                builder.AppendLine($"Additional User Context: {input.AdditionalContext}");
            }
            builder.AppendLine();
            builder.AppendLine("Consider the following user information regarding learning style, education level, special needs, and preferences:");
            builder.AppendLine($"- Education Level: {profile.EducationLevel}");
            builder.AppendLine($"- Preferred Content Types: {profile.PreferredContentTypes}");
            builder.AppendLine($"- Special Considerations: {profile.SpecialConsiderations}");
            builder.AppendLine($"- Additional Info: {profile.AdditionalConsiderations}");
            builder.AppendLine();
            builder.AppendLine("Generate a description for the course to be used in the JSON as well as 'tags' for searching for the course and its lessons.");
            builder.AppendLine("Respond strictly in the following JSON format:");
            builder.AppendLine("{");
            builder.AppendLine("  \"Title\": \"Course Title\",");
            builder.AppendLine("  \"Description\": \"Description\",");
            builder.AppendLine("  \"Tags\": [\"tag1\", \"tag2\", \"etc.\"],");
            builder.AppendLine("  \"Lessons\": [");
            builder.AppendLine("    { \"Title\": \"Lesson 1 Title\", \"Description\": \"Description\", \"Tags\": [\"tag1\", \"tag2\", \"etc.\"] }");
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("If the topic includes any inappropriate, hateful, or offensive language, return a JSON warning message instead.");
            builder.AppendLine("Avoid explicit, harmful, illegal, or discriminatory content. If unsure, return a warning message and do not generate a course plan.");
            builder.AppendLine("If the topic is too broad, ask the user to narrow it down or provide suggestions based on the topic. Respond in a manner consistent with the user's education level and special considerations.");
            return builder.ToString();
        }
    }
}
