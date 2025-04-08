using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class StudentService
    {
        private readonly HttpClient _http;
        private readonly AuthenticationStateProvider _authStateProvider;

        public StudentService(HttpClient http, AuthenticationStateProvider authStateProvider)
        {
            _http = http;
            _authStateProvider = authStateProvider;
        }

        public async Task<StudentDTO> GetCurrentStudentAsync()
        {
            // Retrieve the current user's authentication state
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var userId = user.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                throw new Exception("User is not authenticated or user id not found.");
            }

            // Call the bakckend endpoint GET api/students/{userId}
            return await _http.GetFromJsonAsync<StudentDTO>($"api/students/{userId}");
        }

        public async Task<StudentProfileDTO> GetStudentProfileAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var userId = user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new Exception("User is not authenticated or user id not found.");
            }

            // Call the backend endpoint GET api/students/{userId}
            var studentDTO = await _http.GetFromJsonAsync<StudentDTO>($"api/students/{userId}");
            return studentDTO?.Profile;
        }

        public async Task UpdateStudentProfileAsync(StudentProfileDTO profile)
        {
            // Retrieve the current user's authentication state
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var userId = user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new Exception("User is not authenticated or user id not found.");
            }
            // Call the backend endpoint PUT api/students/{userId}
            var response = await _http.PutAsJsonAsync($"api/students/{userId}", profile);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error updating student profile: {response.ReasonPhrase}");
            }
        }
    }
}
