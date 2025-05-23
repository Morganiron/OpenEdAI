﻿using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class CourseService
    {
        private readonly HttpClient _http;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly LoadingService _loader;
        private readonly ILogger<CourseService> _logger;

        public CourseService(HttpClient http, AuthenticationStateProvider authStateProvider, LoadingService loader, ILogger<CourseService> logger)
        {
            _http = http;
            _authStateProvider = authStateProvider;
            _loader = loader;
            _logger = logger;
        }

        public async Task<List<CourseDTO>> GetEnrolledCoursesAsync()
        {
            try
            {
                // Show the loading spinner
                _loader.Show();

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
                _logger.LogError($"API error: {ex.Message}");
                return new List<CourseDTO>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
                return new List<CourseDTO>();
            }
            finally
            {
                // Hide the loading spinner
                _loader.Hide();
            }
        }

        public async Task<CourseDTO> GetCourseByIdAsync(int id)
        {
            _loader.Show();
            try
            {
                return await _http.GetFromJsonAsync<CourseDTO>($"api/courses/{id}");
            }
            finally
            {
                _loader.Hide();
            }
        }

        public async Task<bool> UnenrollFromCourseAsync(int courseId)
        {
            // Get the student from the authentication state
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var student = authState.User;
            var studentId = student.FindFirst("sub")?.Value;

            try
            {
                var response = await _http.DeleteAsync($"api/courses/{courseId}/UnenrollStudent/{studentId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unenrolling from course {courseId}: {ex.Message}");
                return false;
            }
        }
    }
}
