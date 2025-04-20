using System.Net.Http.Json;
using OpenEdAI.Client.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace OpenEdAI.Client.Services
{
    public class CourseProgressService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationStateProvider _authStateProvider;

        public CourseProgressService(HttpClient httpClient, AuthenticationStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;
        }
        // Gets all progress records for the user
        public async Task<List<CourseProgressDTO>> GetUserProgressAsync()
            => await _httpClient.GetFromJsonAsync<List<CourseProgressDTO>>("api/CourseProgress/user")
            ?? new List<CourseProgressDTO>();

        // Get or create a progress record for this user and a specific course
        public async Task<CourseProgressDTO?> GetCourseProgressAsync(int courseId)
        {
            var list = await GetUserProgressAsync();
            return list.FirstOrDefault(p => p.CourseID == courseId);
        }

        public async Task<CourseProgressDTO> CreateProgressAsync(int courseId)
        {
            var state = await _authStateProvider.GetAuthenticationStateAsync();
            var user = state.User;

            var createDto = new CreateCourseProgressDTO
            {
                UserID = user.FindFirst("sub")?.Value!,
                UserName = user.FindFirst("username")?.Value!,
                CourseID = courseId,
            };
            Console.WriteLine($"Data being sent to API: {createDto.UserID}, {createDto.UserName}, {createDto.CourseID}");
            var resp = await _httpClient.PostAsJsonAsync("api/CourseProgress", createDto);
            Console.WriteLine($"Response from API: {resp.Content}");
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CourseProgressDTO>()!;
        }

        public async Task MarkLessonCompleteAsync(int progressId, int lessonId)
        {
            var patch = new MarkLessonCompleteDTO { LessonID = lessonId };
            var resp = await _httpClient.PatchAsJsonAsync($"api/CourseProgress/{progressId}", patch);
            resp.EnsureSuccessStatusCode();
        }
    }
}
