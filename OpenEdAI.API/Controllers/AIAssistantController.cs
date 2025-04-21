using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;
using OpenEdAI.API.Services;

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
                    new Message(Role.System,
                    "You are a thoughtful and inclusive AI assistant. " +
                    "Your role is to generate tailored learning paths for students based on their profile, goals, and needs. " +
                    $"Prioritize the learner's education level, experience, and special considerations such as learning disabilities (e.g., {profile.SpecialConsiderations}). " +
                    "Ensure each lesson is appropriately scoped and structured for their cognitive and developmental level. " +
                    "Split complex topics into micro-lessons if needed. Output strict JSON only, no markdown or commentary. " +
                    $"Titles and tags must reflect the real-world language a student at the {profile.EducationLevel} level would use in search queries. " +
                    "Return a JSON warning if the topic is too broad or violates guidelines."),
                    new Message(Role.User, prompt)
                };

                var chatRequest = new ChatRequest(messages, model: Model.GPT4oMini.Id, temperature: 0.4);
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
                _logger.LogWarning("No output received from OpenAI API in GenerateCourse.");
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
                    string.IsNullOrWhiteSpace(coursePlan.Description) || !coursePlan.Tags.Any() ||
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

            ChatResponse chatResponse;
            try
            {
                // Create messages for the adjustment request:
                var messages = new[]
                {
                    new Message(Role.System,
                    "You are a thoughtful and inclusive AI assistant. " +
                    "Your role is to adjust the tailored learning paths for a student based on their profile, goals, and needs. " +
                    $"Prioritize the learner's education level, experience, and special considerations such as learning disabilities (e.g.,  {profile.SpecialConsiderations} )." +
                    "Ensure each lesson is appropriately scoped and structured for their cognitive and developmental level. " +
                    "Split complex topics into micro-lessons if needed. Output strict JSON only, no markdown or commentary. " +
                    $"Titles and tags must reflect the real-world language a student at the {profile.EducationLevel} level would use in search queries. " +
                    "Return a JSON warning if the topic is too broad or violates guidelines."),
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
            if (plan == null || string.IsNullOrWhiteSpace(plan.Title) || plan.Tags == null || plan.Lessons == null || !plan.Lessons.Any())
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

                    // Get the Student in this scope
                    var studentEntity = await scopedContext.Students.FindAsync([userId], token);

                    // Make sure the student still exists
                    if (studentEntity == null)
                        throw new InvalidOperationException($"Student {userId} vanished mid-job!");

                    // Rebuild the course and lessons in this scope
                    var courseToSave = new Course(
                    plan.Title,
                    plan.Description,
                    plan.Tags,
                    userId,
                    studentEntity.UserName);

                    // Add the student to the course's enrolled students like in CreateCourse
                    courseToSave.EnrolledStudents.Add(studentEntity);

                    // Call the AI to genreate the content links for all lessons
                    var searchPlans = await planSvc.GeneratePlanAsync(
                        userInput,
                        plan,
                        studentProfile,
                        token
                    );

                    // Execute each lesson's queries
                    for (int i = 0; i < searchPlans.Count; i++)
                    {
                        var sp = searchPlans[i];
                        var originalLesson = plan.Lessons[i];


                        var contentLinks = await contentSearchSvc
                        .SearchContentLinksAsync(
                            userInput,
                            plan,
                            sp,
                            studentProfile,
                            token
                        );

                        var lesson = new Lesson(
                            originalLesson.Title,
                            originalLesson.Description,
                            contentLinks,
                            originalLesson.Tags,
                            courseID: 0
                            );

                        courseToSave.Lessons.Add(lesson);
                    }
                    // Save the course and lessons to the database
                    scopedContext.Courses.Add(courseToSave);
                    await scopedContext.SaveChangesAsync(token);

                    // Create the CourseProgress record immediately after saving the course so the course id is generated
                    // then save the course progress to the database
                    var courseProgress = new CourseProgress(userId, studentEntity.UserName, courseToSave.CourseID);
                    scopedContext.CourseProgress.Add(courseProgress);
                    await scopedContext.SaveChangesAsync(token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background job failed for the course.");
                    return;
                }
            });

            return Ok();

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
            sb.AppendLine($"- Education Level: {profile.EducationLevel}");
            sb.AppendLine($"- Preferred Content Types: {profile.PreferredContentTypes ?? "None"}");
            sb.AppendLine($"- Special Needs or Considerations: {profile.SpecialConsiderations ?? "None"}");
            sb.AppendLine($"- Key Interests & Background: {profile.AdditionalConsiderations ?? "None"}");
            sb.AppendLine();
            sb.AppendLine("### PreviousPlanJSON");
            sb.AppendLine(previousPlanJson);
            sb.AppendLine();
            sb.AppendLine("### The user requests adjustments based on:");
            sb.AppendLine(userMessage);
            sb.AppendLine();
            sb.AppendLine("### Requirements");
            sb.AppendLine($"   - Adjust lesson depth and pacing to match a student at the {profile.EducationLevel} level" +
                          (!string.IsNullOrWhiteSpace(profile.SpecialConsiderations)
                              ? $" and account for special considerations: {profile.SpecialConsiderations}."
                              : "."));
            sb.AppendLine("   If the profile information suggests the user may have issues with the topic based on education, special needs, or user request, break dense ideas into 15‑25 minute micro‑lessons.");
            sb.AppendLine("   Pay close attention to any additional context, key interests, background, special needs, and education level when inferring titles, descriptions, and tags.");
            sb.AppendLine("   Tags are 1-3 word phrases similar to hashtags to be used as search keywords.");
            sb.AppendLine("   There should be a set of tags for the course and a set of tags for each lesson.");
            sb.AppendLine("   - Example Tag patterns:");
            sb.AppendLine("     * \"{Topic} for {ExperienceLevel}\"");
            sb.AppendLine("     * \"{Topic} for people with {SpecialConsiderations}\"");
            sb.AppendLine("     * \"{ExperienceLevel} {Topic} for {EducationLevel}\"");
            sb.AppendLine("     * Add any other combinations that tie topic, experience, special needs, and education level.");
            sb.AppendLine("2. Structure & schema must match exactly:");
            sb.AppendLine("   {");
            sb.AppendLine("     \"Title\": string,");
            sb.AppendLine("     \"Description\": string,");
            sb.AppendLine("     \"Tags\": [string],");
            sb.AppendLine("     \"Lessons\": [");
            sb.AppendLine("       { \"Title\": string, \"Description\": string, \"Tags\": [string] }");
            sb.AppendLine("     ]");
            sb.AppendLine("   }");
            sb.AppendLine("3. Optimise Lesson:Title, Lesson:Description, and Lesson:Tags for real‑world search phrases.");
            sb.AppendLine("4. If the request is unclear / violates policy, reply with:");
            sb.AppendLine("   {\"Warning\":\"<explanation>\"}");
            sb.AppendLine();
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
            sb.AppendLine($"- Key Interests & Background: {profile.AdditionalConsiderations ?? "None"}");
            sb.AppendLine();
            sb.AppendLine("### Course Parameters");
            sb.AppendLine($"- Topic: {input.Topic}");
            sb.AppendLine($"- User's experience with the topic: {input.ExperienceLevel}");
            if (!string.IsNullOrWhiteSpace(input.AdditionalContext))
            {
                sb.AppendLine($"- Additional Context: {input.AdditionalContext}");
            }
            sb.AppendLine();
            sb.AppendLine("### Requirements:");
            sb.AppendLine("1. Use the learner profile to personalize EVERYTHING: Titles, Descriptions, and Tags should reflect the user's background, preferences, and goals.");
            sb.AppendLine("   - Include relatable examples or analogies based on profile interests.");
            sb.AppendLine($"   - Adjust lesson depth and pacing to match at student at the {profile.EducationLevel} level" +
                                (!string.IsNullOrWhiteSpace(profile.SpecialConsiderations) ?
                                $" with special considerations: {profile.SpecialConsiderations}." : "."));
            sb.AppendLine("   If the profile information suggests the user may have issues with the topic based on education, special needs, or any additional information, break dense ideas into 15‑25 minute micro‑lessons.");
            sb.AppendLine("2. Infer missing details from the profile (such as the user's age/grade based on education level) to enrich content: select real-world search keywords and scenarios relevant to the learner.");
            sb.AppendLine("   Pay close attention to any additional context, key interests, background, special needs, and education level when inferring titles, descriptions, and tags.");
            sb.AppendLine("   Tags are 1-3 word phrases similar to hashtags to be used as search keywords.");
            sb.AppendLine("   There should be a set of tags for the course and a set of tags for each lesson.");
            sb.AppendLine("   - Example Tag patterns:");
            sb.AppendLine("     * \"{Topic} for {ExperienceLevel}\"");
            sb.AppendLine("     * \"{Topic} for people with {SpecialConsiderations}\"");
            sb.AppendLine("     * \"{ExperienceLevel} {Topic} for {EducationLevel}\"");
            sb.AppendLine("     * Add any other combinations that tie topic, experience, special needs, and education level.");
            sb.AppendLine("3. Structure output EXACTLY as JSON matching schema:");
            sb.AppendLine("   {");
            sb.AppendLine("     \"Title\": string,");
            sb.AppendLine("     \"Description\": string,");
            sb.AppendLine("     \"Tags\": [string],");
            sb.AppendLine("     \"Lessons\": [");
            sb.AppendLine("       { \"Title\": string, \"Description\": string, \"Tags\": [string] }");
            sb.AppendLine("     ]");
            sb.AppendLine("   }");
            sb.AppendLine("4. Optimize every Title and Tag for real-world search intent and learning objectives.");
            sb.AppendLine("5. If unclear or policy-violating, respond with:");
            sb.AppendLine("   {\"Warning\":\"<explanation>\"}");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
