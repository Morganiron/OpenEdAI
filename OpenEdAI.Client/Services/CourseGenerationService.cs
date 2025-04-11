using System.Net.Http.Json;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class CourseGenerationService
    {
        private readonly HttpClient _http;

        public CourseGenerationService(HttpClient http) => _http = http;

        public async Task<CoursePlanDTO> GenerateCoursePlanAsync(CoursePersonalizationInput input)
        {
            var response = await _http.PostAsJsonAsync("ai/generate-course", input);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CoursePlanDTO>();
        }

        public async Task<CoursePlanDTO> AdjustCoursePlanAsync(string userMessage)
        {
            var response = await _http.PostAsJsonAsync("ai/adjust-course", new { message = userMessage });
            return await response.Content.ReadFromJsonAsync<CoursePlanDTO>();
        }
    }
}
