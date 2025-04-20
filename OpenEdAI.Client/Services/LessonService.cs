using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class LessonService
    {
        private readonly HttpClient _http;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly LoadingService _loader;
        private readonly ILogger<LessonService> _logger;

        public LessonService(HttpClient http, AuthenticationStateProvider authStateProvider, LoadingService loader, ILogger<LessonService> logger)
        {
            _http = http;
            _authStateProvider = authStateProvider;
            _loader = loader;
            _logger = logger;
        }

        public async Task<List<LessonDTO>> GetLessonsByCourseIdAsync(int courseId)
        {
            try
            {
                // Show the loading spinner
                _loader.Show();

                // Build the URL using the courseId
                string url = $"api/lessons/course/{courseId}";

                // Use GetFromJsonAsync and ensure we never return null
                var lessons = await _http.GetFromJsonAsync<List<LessonDTO>>(url);
                return lessons ?? new List<LessonDTO>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API error:");
                return new List<LessonDTO>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error:");
                return new List<LessonDTO>();
            }
            finally
            {
                // Hide the loading spinner
                _loader.Hide();
            }
        }
    }
}
