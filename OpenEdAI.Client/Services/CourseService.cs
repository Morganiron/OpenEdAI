using System.Net.Http.Json;
using OpenEdAI.Client.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace OpenEdAI.Client.Services
{
    public class CourseService
    {
        private readonly HttpClient _http;
        private readonly AuthenticationStateProvider _authStateProvider;

        public CourseService(HttpClient http, AuthenticationStateProvider authStateProvider)
        {
            _http = http;
            _authStateProvider = authStateProvider;

        }

        public async Task<List<CourseDTO>> GetEnrolledCoursesAsync()
        {
            try
            {
                // Retrieve the current user's authentication state
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                // Extract the userId from the "sub" claim
                string userId = user.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new Exception("User is not authenticated or user id not found.");
                }

                // Build the URL using the userId
                string url = $"api/students/{userId}/EnrolledCourses";

                // Use GetFromJsonAsync and ensure we never return null
                var courses = await _http.GetFromJsonAsync<List<CourseDTO>>(url);
                return courses ?? new List<CourseDTO>();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API error: {ex.Message}");
                return new List<CourseDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return new List<CourseDTO>();
            }
        }
    }
}
