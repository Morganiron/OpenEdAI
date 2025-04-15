using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenEdAI.API.Models;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using System.Text.Json;
using System.Text;
using Microsoft.Identity.Client;
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

        public AIAssistantController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;

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
                return Unauthorized("User ID not found in token.");
            }

            // Retrieve the user's profile to use in the AI prompt
            var profile = await _context.StudentProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return BadRequest("User profile not found.");
            }

            // Build the prompt using the user's input and profile
            var prompt = BuildPrompt(input, profile);
            Console.WriteLine("Generated propmt:");
            Console.WriteLine(prompt);

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
                // Create a new ChatRequest using the specified messages, telling the system which model to use and set the randomness of the generated output
                // 0.7 is a balanced output between a deterministic and creative resonse.
                var chatRequest = new ChatRequest(messages, model: Model.GPT4oMini.Id, temperature: 0.7);
                // Call the ChatEndpoint asynchronously
                chatResponse = await _openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
                return StatusCode(500, ex.Message);
            }

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return StatusCode(500, "No output received from OpenAI API.");
            }

            string coursePlanJson = chatResponse.Choices[0].Message.Content;
            Console.WriteLine("Raw AI-generated output:");
            Console.WriteLine(coursePlanJson);

            CoursePlanDTO coursePlan;
            try
            {
                coursePlan = JsonSerializer.Deserialize<CoursePlanDTO>(
                    coursePlanJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                // Validate that deserialization produced required fields
                if (coursePlan == null || string.IsNullOrWhiteSpace(coursePlan.Title) ||
                    coursePlan.Lessons == null || !coursePlan.Lessons.Any())
                {
                    throw new JsonException("Missing required fields in CoursePlanDTO.");
                }
                return Ok(coursePlan);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Deserialization error: {ex.Message}");

                // Create a fallback warning CoursePlanDTO so that the front-end chat always has something to display
                coursePlan = new CoursePlanDTO
                {
                    Title = "Warning: Course Plan Issue",
                    Description = $"The AI response did not generate the expected JSON format. Response received:\n{coursePlanJson}",
                    Lessons = new List<LessonDTO>()
                };
            }

            return Ok(coursePlan);

            // Temporarily mock resonse
            //var mockPlan = new CoursePlanDTO
            //{
            //    Title = input.Topic,
            //    Description = "AI generated course description",
            //    Lessons = new List<LessonDTO>
            //    {
            //        new LessonDTO
            //        {
            //            Title = "Introduction",
            //            Description = "Overview of the Course"
            //        },
            //        new LessonDTO
            //        {
            //            Title = "Lesson 1",
            //            Description = "Basics of the topic."
            //        }
            //    }
            //};

            //return Ok(coursePlanJson);
        }

        // POST: ai/AdjustCourse
        [HttpPost("adjust-course")]
        public async Task<IActionResult> AdjustCourse([FromBody] JsonElement payload)
        {
            var userId = GetUserIdFromToken();

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token.");
            }

            // Log received payload properties for debugging
            Console.WriteLine("Received payload:");
            foreach (var prop in payload.EnumerateObject())
            {
                Console.WriteLine($"Property: {prop.Name}");
            }

            // Validate required fields exist
            if (!payload.TryGetProperty("userMessage", out JsonElement userMessageElement) ||
                !payload.TryGetProperty("previousPlan", out JsonElement previousPlanElement))
            {
                return BadRequest("Missing required fields: UserMessage and/or PreviousPlan.");
            }

            string userMessage = userMessageElement.GetString();
            string previousPlanJson = previousPlanElement.GetString();

            if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(previousPlanJson))
            {
                return BadRequest("UserMessage and PreviousPlan must not be empty.");
            }

            // Deserialize previous course plan
            CoursePlanDTO previousPlan;
            try
            {
                previousPlan = JsonSerializer.Deserialize<CoursePlanDTO>(previousPlanJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                return BadRequest("Failed to deserialize previous plan: " + ex.Message);
            }
            
            // Retrieve the user's profile
            var profile = await _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return BadRequest("User profile not found.");
            }

            // Build the adjustment prompt
            var prompt = BuildAdjustmentPrompt(previousPlanJson, userMessage, profile);
            Console.WriteLine("Adjustment prompt:");
            Console.WriteLine(prompt);

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
                Console.WriteLine($"Error calling OpenAI API for adjustment: {ex.Message}");
                return StatusCode(500, ex.Message);
            }

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return StatusCode(500, "No output received from OpenAI API.");
            }

            // Extract the raw AI-generated output
            string adjustedPlanJson = chatResponse.Choices[0].Message.Content;
            Console.WriteLine("Raw AI-generated adjusted output:");
            Console.WriteLine(adjustedPlanJson);


            CoursePlanDTO adjustedPlan;
            try
            {
                adjustedPlan = JsonSerializer.Deserialize<CoursePlanDTO>(
                    adjustedPlanJson,
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
                Console.WriteLine($"Deserialization error: {ex.Message}");

                // Create a fallback warning CoursePlanDTO so that the front-end chat always has something to display
                adjustedPlan = new CoursePlanDTO
                {
                    Title = "Warning: Course Plan Issue",
                    Description = $"The AI response did not generate the expected JSON format. Response received:\n{adjustedPlanJson}",
                    Lessons = new List<LessonDTO>()
                };
            }

            return Ok(adjustedPlan);

            // Mocked adjusted plan
            //var adjustedPlan = new CoursePlanDTO
            //{
            //    Title = "Adjusted: " + previousPlan?.Title,
            //    Description = "AI Adjusted Generated Course Description",
            //    Lessons = new List<LessonDTO>
            //    {
            //        new LessonDTO { Title = "Refined Introduction", Description = "Updated overview with new focus." },
            //        new LessonDTO { Title = "Lesson 1 (Revised)", Description = "Enhanced content for better clarity." },
            //        new LessonDTO { Title = "Lesson 2", Description = "Additional lesson based on feedback." }
            //    }
            //};

            //return Ok(adjustedPlan);
        }

        // POST: ai/SubmitCoursePlan
        [HttpPost("submit-course")]
        public async Task<IActionResult> SubmitCoursePlan([FromBody] SubmittedCourseDTO plan)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token.");
            }

            // Retrieve the student to update HasCompletedSetup
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserID == userId);
            if (student == null)
            {
                return NotFound("Student not found.");
            }

            // If this is the student's first time setting up their profile and creating a course,
            // update their status
            if (!student.HasCompletedSetup)
            {
                student.MarkSetupComplete();
            }
            
            // Validate the course plan
            if (plan == null || string.IsNullOrWhiteSpace(plan.Title) || plan.Lessons == null || !plan.Lessons.Any())
            {
                return BadRequest("Invalid course plan.");
            }

            // TODO: Send the course plan to the search APIs to generate content links

            await _context.SaveChangesAsync();
            return Ok(new {message = "Course finalized successfully."});
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
            builder.AppendLine("  \"title\": \"Course Title\",");
            builder.AppendLine("  \"description\": \"Description\",");
            builder.AppendLine("  \"tags\": [\"tag1\", \"tag2\", \"etc.\"],");
            builder.AppendLine("  \"lessons\": [");
            builder.AppendLine("    { \"title\": \"Lesson 1 Title\", \"description\": \"Description\", \"tags\": [\"tag1\", \"tag2\", \"etc.\"] }");
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
