using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class CourseProgressService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly CourseService _courseService;

        public CourseProgressService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, CourseService courseService)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;
            _courseService = courseService;
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
            var resp = await _httpClient.PostAsJsonAsync("api/CourseProgress", createDto);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CourseProgressDTO>()!;
        }

        /// <summary>
        /// Returns onl:
        ///  - progress for currently-enrolled courses, or
        ///  - 100%-complete progress (even if unenrolled)
        /// Each item is enriched with CourseTitle and TotalLessons
        /// </summary>
        public async Task<List<DashboardProgressDTO>> GetDashboardProgressAsync()
        {
            // Load everything
            var allProg = await GetUserProgressAsync();
            var courses = await _courseService.GetEnrolledCoursesAsync();

            var enrolledIds = courses.Select(c => c.CourseID).ToHashSet();

            var filtered = new List<DashboardProgressDTO>();

            foreach (var p in allProg)
            {
                // Skip anything not enrolled AND not 100%-complete
                if (!enrolledIds.Contains(p.CourseID) && p.CompletionPercentage < 100)
                    continue;

                // Find title and total lessons
                var enrolled = courses.FirstOrDefault(c => c.CourseID == p.CourseID);
                string title;
                int totalLessons;

                if (enrolled != null)
                {
                    title = enrolled.Title;
                    totalLessons = enrolled.LessonIds?.Count ?? 0;
                }
                else
                {
                    // Orphaned progress: 100%-complete, but not enrolled
                    var course = await _courseService.GetCourseByIdAsync(p.CourseID);
                    title = course.Title;
                    totalLessons = course.LessonIds?.Count ?? 0;
                }

                filtered.Add(new DashboardProgressDTO
                {
                    CourseID = p.CourseID,
                    CourseTitle = title,
                    LessonsCompleted = p.LessonsCompleted,
                    TotalLessons = totalLessons,
                    CompletionPercentage = p.CompletionPercentage,
                    LastUpdated = p.LastUpdated
                });
            }
            return filtered;
        }

        public async Task MarkLessonCompleteAsync(int progressId, int lessonId)
        {
            var patch = new MarkLessonCompleteDTO { LessonID = lessonId };
            var resp = await _httpClient.PatchAsJsonAsync($"api/CourseProgress/{progressId}", patch);
            resp.EnsureSuccessStatusCode();
        }
    }
}
