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
using OpenEdAI.API.Services;
using Newtonsoft.Json.Linq;

namespace OpenEdAI.API.Controllers
{
    [Route("ai")]
    [ApiController]
    public class AIAssistantController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly OpenAIClient _openAiClient;
        private readonly ILogger<AIAssistantController> _logger;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IContentSearchService _contentSearchService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public AIAssistantController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AIAssistantController> logger,
            IBackgroundTaskQueue backgroundTaskQueue,
            IContentSearchService contentSearchService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _logger = logger;
            _backgroundTaskQueue = backgroundTaskQueue;
            _contentSearchService = contentSearchService;
            _serviceScopeFactory = serviceScopeFactory;

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
                    new Message(Role.System, "You are a helpful, inclusive AI course planner. "
                                              + "Always prioritise the learner profile (education level & special considerations). "
                                              + "If the profile hints at limited attention (e.g., ADHD) break dense topics into short micro‑lessons. "
                                              + "Return CLEAN, valid JSON only — no commentary or code‑fences. "
                                              + "Optimise titles & tags for the search terms this learner would use. "
                                              + "If the topic is offensive, inappropriate, or too broad, return a JSON warning."),
                    new Message(Role.User, prompt)
                };

                // Temperature of 0.2 seems to respond with very good results
                var chatRequest = new ChatRequest(messages, model: Model.GPT4oMini.Id, temperature: 0.3);
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
                    new Message(Role.System, "You are a helpful, inclusive AI course planner. "
                                            + "Your job is to adjust an existing plan based on feedback. "
                                            + "Always prioritise the learner profile; create micro‑lessons when attention issues exist. "
                                            + "Return CLEAN, valid JSON only — no commentary or code‑fences. "
                                            + "If the adjustment request is unclear, inappropriate, or violates policy, return a JSON warning."),
                    new Message(Role.User, prompt)
                };
                // Setting the temperature slightly higher to provide more creative results for changes
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
        public async Task<IActionResult> SubmitCoursePlan([FromBody] SubmitCourseRequest request)
        {
            // Get the course plan and user input from the request
            var userInput = request.UserInput;
            var plan = request.Plan;

            // Validate the user
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

            // Enqueue a background task for generating content links
            _backgroundTaskQueue.EnqueueBackgroundWorkItem(async token =>
            {
                try
                {
                    // Create a new scope so we get a fresh DbContext and services
                    using var scope = _serviceScopeFactory.CreateScope();
                    var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var planSvc = scope.ServiceProvider.GetRequiredService<AIDrivenSearchPlanService>();
                    var contentSearchSvc = scope.ServiceProvider.GetRequiredService<IContentSearchService>();

                    // Retrieve the student's profile
                    var profileEntity = await scopedContext.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                    if (profileEntity == null)
                    {
                        _logger.LogWarning("Student profile not found for user {UserId} during background processing.", userId);
                    }
                    // Store the profile in a DTO
                    var studentProfile = new StudentProfileDTO
                    {
                        EducationLevel = profileEntity.EducationLevel,
                        PreferredContentTypes = profileEntity.PreferredContentTypes,
                        SpecialConsiderations = profileEntity.SpecialConsiderations,
                        AdditionalConsiderations = profileEntity.AdditionalConsiderations
                    };

                    // Rebuild the course and lessons in this scope
                    var courseToSave = new Course(
                    plan.Title,
                    plan.Description,
                    plan.Tags,
                    userId,
                    student.UserName);

                    // Call the AI to genreate the content links for all lessons
                    var searchPlans = await planSvc.GeneratePlanAsync(
                        userInput,
                        plan,
                        studentProfile,
                        token
                    );

                    // Execute each lesson's queries
                    foreach (var lessonPlan in searchPlans)
                    {
                        var contentLinks = await contentSearchSvc
                        .SearchContentLinksAsync(
                            userInput,
                            plan,
                            lessonPlan,
                            studentProfile,
                            token
                        );

                        var lesson = new Lesson(
                            lessonPlan.LessonTitle,
                            plan.Lessons.First(l => l.Title == lessonPlan.LessonTitle).Description,
                            contentLinks,
                            plan.Lessons.First(l => l.Title == lessonPlan.LessonTitle).Tags,
                            /* courseId placeholder */ 0
                            );

                        courseToSave.Lessons.Add(lesson);
                    }

                    // Save the new Course and lessons to the database
                    // (Not saving changes for testing purposes)
                    //scopedContext.Courses.Add(courseToSave);
                    //await scopedContext.SaveChangesAsync(token);

                    _logger.LogInformation("Completed background processing for course '{CourseTitle}'.", plan.Title);

                    // ---- Output all of the data to the console --- //
                    var logPayload = new
                    {
                        Course = new
                        {
                            ID = courseToSave.CourseID,
                            Title = courseToSave.Title,
                            Description = courseToSave.Description,
                            Tags = courseToSave.Tags
                        },
                        Lessons = courseToSave.Lessons.Select(l => new
                        {
                            ID = l.LessonID,
                            Title = l.Title,
                            Description = l.Description,
                            Tags = l.Tags,
                            ContentLinks = l.ContentLinks
                        })
                    };

                    // Serialize to JSON
                    var json = JsonSerializer.Serialize(
                        logPayload,
                        new JsonSerializerOptions { WriteIndented = true }
                    );

                    // Emit as a single log line
                    _logger.LogInformation("Enriched course data: {CourseJson}", json);

                    // --------------------------------------------------

                    
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background job failed for the course.");
                    return;
                }

            });

            _logger.LogInformation("Course finalized successfully for user {UserId}.", userId);
            return Ok(new { message = "Course finalized successfully." });
        
        }

        /// <summary>
        /// Builds a prompt for adjusting an existing course plan.
        /// </summary>
        private string BuildAdjustmentPrompt(string previousPlanJson, string userMessage, StudentProfile profile)
        {
            var sb = new StringBuilder();

            sb.AppendLine("## COURSE‑PLAN ADJUSTMENT — RETURN STRICT JSON ONLY ##");
            sb.AppendLine();
            sb.AppendLine("### Learner Profile");
            sb.AppendLine($"EducationLevel: {profile.EducationLevel}");
            sb.AppendLine($"SpecialConsiderations: {profile.SpecialConsiderations ?? "None"}");
            sb.AppendLine($"PreferredContentTypes: {profile.PreferredContentTypes ?? "None"}");
            sb.AppendLine($"AdditionalLearnerInfo: {profile.AdditionalConsiderations ?? "None"}");
            sb.AppendLine();
            sb.AppendLine("### CurrentPlanJSON");
            sb.AppendLine(previousPlanJson);
            sb.AppendLine();
            sb.AppendLine("### UserRequest");
            sb.AppendLine(userMessage);
            sb.AppendLine();
            sb.AppendLine("### Requirements");
            sb.AppendLine("1. Update the plan **respecting ALL learner profile info**.");
            sb.AppendLine("   •  If attention‑span issues (e.g., ADHD) → split large ideas into micro‑lessons.");
            sb.AppendLine("2. Keep a single‑level `Lessons[]` array (no chapters).");
            sb.AppendLine("3. Structure & schema must match exactly:");
            sb.AppendLine("   {");
            sb.AppendLine("     \"Title\": string,");
            sb.AppendLine("     \"Description\": string,");
            sb.AppendLine("     \"Tags\": [string],");
            sb.AppendLine("     \"Lessons\": [");
            sb.AppendLine("       { \"Title\": string, \"Description\": string, \"Tags\": [string] }");
            sb.AppendLine("     ]");
            sb.AppendLine("   }");
            sb.AppendLine("4. Optimise `Tags` and `Lesson.Title` for real‑world search phrases.");
            sb.AppendLine("5. If the request is unclear / violates policy, reply with:");
            sb.AppendLine("   {\"Warning\":\"<explanation>\"}");
            sb.AppendLine();
            sb.AppendLine("### Produce the JSON now.");
            return sb.ToString();
        }

        /// <summary>
        /// Builds the prompt for generating a course plan.
        /// </summary>
        private string BuildPrompt(CoursePersonalizationInput input, StudentProfile profile)
        {
            var sb = new StringBuilder();

            sb.AppendLine("### COURSE GENERATION REQUEST - RETURN STRICT JSON ONLY ###");
            sb.AppendLine();
            sb.AppendLine("### Learner Profile");
            sb.AppendLine($"- Education Level: {profile.EducationLevel}");
            sb.AppendLine($"- Preferred Content Types: {profile.PreferredContentTypes ?? "None"}");
            sb.AppendLine($"- Special Needs or Considerations: {profile.SpecialConsiderations ?? "None"}");
            sb.AppendLine($"- Additional Learner Info: {profile.AdditionalConsiderations ?? "None"}");
            sb.AppendLine();
            sb.AppendLine("### Course Parameters");
            sb.AppendLine($"Topic: {input.Topic}");
            sb.AppendLine($"Learner Experience Level: {input.ExperienceLevel}");
            if (!string.IsNullOrWhiteSpace(input.AdditionalContext))
            {
                sb.AppendLine($"Additional User Context: {input.AdditionalContext}");
            }
            sb.AppendLine();
            sb.AppendLine("### Requirements");
            sb.AppendLine("1. **Prioritise learner profile** — lesson length, depth, and ordering must suit the stated education level **and** any special considerations.");
            sb.AppendLine("   •  If profile hints at limited attention (e.g., ADHD), break dense ideas into 15‑25 minute micro‑lessons.");
            sb.AppendLine("2. Single‑level structure (no chapters) ⇒ `Lessons[]` is a flat array.");
            sb.AppendLine("3. Use plain, valid JSON *only* (no markdown, no commentary, no code‑fences).");
            sb.AppendLine("4. Schema to follow exactly:");
            sb.AppendLine("   {");
            sb.AppendLine("     \"Title\": string,");
            sb.AppendLine("     \"Description\": string,");
            sb.AppendLine("     \"Tags\": [string],");
            sb.AppendLine("     \"Lessons\": [");
            sb.AppendLine("       { \"Title\": string, \"Description\": string, \"Tags\": [string] }");
            sb.AppendLine("     ]");
            sb.AppendLine("   }");
            sb.AppendLine("5. Optimise all `Tags` and `Lesson.Title` fields as SEO phrases the learner would actually search.");
            sb.AppendLine("6. If the topic is too broad or violates policy, reply with:");
            sb.AppendLine("   {\"Warning\":\"<explanation>\"}");
            sb.AppendLine();
            sb.AppendLine("### Produce the JSON now."); 
            return sb.ToString();
        }
    }
}
