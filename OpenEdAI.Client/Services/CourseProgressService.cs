using System.Net.Http.Json;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class CourseProgressService
    {
        private readonly HttpClient _httpClient;
        public CourseProgressService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        
        public async Task<List<CourseProgressDTO>> GetUserCourseProgressAsync()
        {
            var progress = await _httpClient.GetFromJsonAsync<List<CourseProgressDTO>>("api/CourseProgress/user");
            return progress ?? new List<CourseProgressDTO>();
        }
    }
}
