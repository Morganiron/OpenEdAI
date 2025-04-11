using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Models;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using System.Text.Json;
using System.Text;
using Microsoft.Identity.Client;

namespace OpenEdAI.API.Controllers
{
    [Route("ai")]
    [ApiController]
    
    public class AIAssistantController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AIAssistantController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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
            // Commented out to return Mocked response until AI integration is complete
            //var prompt = BuildPrompt(input, profile);

            //var client = _httpClientFactory.CreateClient();
            //client.BaseAddress = new Uri("https://api.openai.com/v1/");
            //client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration["OpenAI:ApiKey"]}");

            //var requestBody = new
            //{
            //    model = "gpt-4",
            //    messages = new[]
            //    {
            //        new {role = "system", content = "You are a helpful course planner."},
            //        new {role = "user", content = prompt }
            //    }
            //};

            //var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            //var response = await client.PostAsync("chat/completions", content);

            //if (!response.IsSuccessStatusCode)
            //{
            //    var error = await response.Content.ReadAsStringAsync();
            //    return StatusCode((int)response.StatusCode, error);
            //}

            //var json = await response.Content.ReadAsStringAsync();
            // TODO: Parse the response and construct a CoursePlanDTO from it

            // Temporarily mock resonse
            var mockPlan = new CoursePlanDTO
            {
                Title = input.Topic,
                Lessons = new List<LessonDTO>
                {
                    new LessonDTO
                    {
                        Title = "Introduction",
                        Description = "Overview of the Course"
                    },
                    new LessonDTO
                    {
                        Title = "Lesson 1",
                        Description = "Basics of the topic."
                    }
                }
            };

            return Ok(mockPlan);
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

            Console.WriteLine("Received payload:");
            foreach (var prop in payload.EnumerateObject())
            {
                Console.WriteLine($"Property: {prop.Name}");
            }

            // Gracefully handle missing properties
            if (!payload.TryGetProperty("userMessage", out JsonElement userMessageElement) ||
                !payload.TryGetProperty("previousPlan", out JsonElement previousPlanElement))
            {
                return BadRequest("Missing required fields: UserMessage and/or PreviousPlan.");
            }

            var userMessage = userMessageElement.GetString();
            var previousPlanJson = previousPlanElement.GetString();

            if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(previousPlanJson))
            {
                return BadRequest("UserMessage and PreviousPlan must not be empty.");
            }

            // Deserialize previous plan
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

            var profile = await _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return BadRequest("User profile not found.");
            }
            
            // Commented out to return Mocked response until AI integration is complete
            //var prompt = BuildAdjustmentPrompt(previousPlanJson, userMessage, profile);

            //var client = _httpClientFactory.CreateClient();
            //client.BaseAddress = new Uri("https://api.openai.com/v1/");
            //client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration["OpenAI:ApiKey"]}");

            //var requestBody = new
            //{
            //    model = "gpt-4",
            //    messages = new[]
            //    {
            //        new { role = "system", content = "You are a helpful course planner. The user has an existing course plan and would like to make adjustments." },
            //        new { role = "user", content = prompt }
            //    }
            //};

            //var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            //var response = await client.PostAsync("chat/completions", content);

            //if (!response.IsSuccessStatusCode)
            //{
            //    var error = await response.Content.ReadAsStringAsync();
            //    return StatusCode((int)response.StatusCode, error);
            //}

            //var json = await response.Content.ReadAsStringAsync();
            //var result = JsonDocument.Parse(json);
            //var courseContent = result.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            //try
            //{
            //    var coursePlan = JsonSerializer.Deserialize<CoursePlanDTO>(courseContent, new JsonSerializerOptions
            //    {
            //        PropertyNameCaseInsensitive = true
            //    });

            //    return Ok(coursePlan);
            //}
            //catch (JsonException)
            //{
            //    return StatusCode(500, "Failed to parse adjusted course plan from AI.");
            //}

            // Mocked adjusted plan
            var adjustedPlan = new CoursePlanDTO
            {
                Title = "Adjusted: " + previousPlan?.Title,
                Lessons = new List<LessonDTO>
                {
                    new LessonDTO { Title = "Refined Introduction", Description = "Updated overview with new focus." },
                    new LessonDTO { Title = "Lesson 1 (Revised)", Description = "Enhanced content for better clarity." },
                    new LessonDTO { Title = "Lesson 2", Description = "Additional lesson based on feedback." }
                }
            };

            return Ok(adjustedPlan);
        }

        private string BuildAdjustmentPrompt(string previousPlanJson, string userMessage, StudentProfile profile)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("Here is the current course plan in JSON format::");
            prompt.AppendLine(previousPlanJson);
            prompt.AppendLine("The user has provided the following message to adjust the course plan:");
            prompt.AppendLine(userMessage);

            prompt.AppendLine("Consider the following user information in regards to learning style, education level, special needs, and preferences:");
            prompt.AppendLine("- Education Level: " + profile.EducationLevel);
            prompt.AppendLine("- Preferred Content Types: " + profile.PreferredContentTypes);
            prompt.AppendLine("- Special Considerations: " + profile.SpecialConsiderations);
            prompt.AppendLine("- Additional Info: " + profile.AdditionalConsiderations);

            prompt.AppendLine("\nIf the topic includes any inappropriate, hateful, or offensive language, respond with a warning message and do not generate a course plan.");
            prompt.AppendLine("Avoid any explicit, harmful, illegal, or discriminatory content. If unsure, respond with a warning message and decline to generate a course plan.");
            prompt.AppendLine("If the topic is too broad, ask the user to narrow it down, provide suggestions based on the topic they want to learn. Respond in a manner consistent with their Education Level and Special Considerations.");

            prompt.AppendLine("\nUpdate the course plan based on the user's message and the provided information. Respond strictly in the same JSON format as the original.");

            return prompt.ToString();
        }

        private string BuildPrompt(CoursePersonalizationInput input, StudentProfile profile)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Create a structured course on the topic: {input.Topic}");
            builder.AppendLine($"Experience Level: {input.ExperienceLevel}");

            if (!string.IsNullOrWhiteSpace(input.AdditionalContext))
            {
                builder.AppendLine($"Additional User Context: {input.AdditionalContext}");
            }

            builder.AppendLine("Consider the following user information in regards to learning style, education level, special needs, and preferences:");
            builder.AppendLine("- Education Level: " + profile.EducationLevel);
            builder.AppendLine("- Preferred Content Types: " + profile.PreferredContentTypes);
            builder.AppendLine("- Special Considerations: " + profile.SpecialConsiderations);
            builder.AppendLine("- Additional Info: " + profile.AdditionalConsiderations);

            builder.AppendLine();
            builder.AppendLine("Respond strictly in the following JSON format:");
            builder.AppendLine("{ \"title\": \"Course Title\", \"tags\": [\"tag1\", \"tag2\", \"etc.\"], \"lessons\": [ { \"title\": \"Lesson 1 Title\", \"description\": \"Description\", \"tags\": [\"tag1\", \"tag2\", \"etc.\"] }, ... ] }");
            builder.AppendLine();
            builder.AppendLine("If the topic includes any inappropriate, hateful, or offensive language, respond with a warning message and do not generate a course plan.");
            builder.AppendLine("Avoid any explicit, harmful, illegal, or discriminatory content. If unsure, respond with a warning message and decline to generate a course plan.");
            builder.AppendLine("If the topic is too broad, ask the user to narrow it down, provide suggestions based on the topic they want to learn. Respond in a manner consistent with their Education Level and Special Considerations.");

            return builder.ToString();
        }

    }
}
