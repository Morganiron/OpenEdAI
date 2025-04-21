using System.Text.Json;
using Microsoft.JSInterop;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class StudentProfileState
    {
        private const string StorageKey = "student_profile_data";
        private readonly IJSRuntime _js;
        private readonly ILogger _logger;

        public StudentProfileDTO ProfileDTO { get; set; } = new();

        public StudentProfileState(IJSRuntime js, ILogger<StudentProfileState> logger)
        {
            _js = js;
            _logger = logger;
        }

        public async Task LoadStateAsync()
        {
            var json = await _js.InvokeAsync<string>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    ProfileDTO = JsonSerializer.Deserialize<StudentProfileDTO>(json) ?? new();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing StudentProfileDTO:");
                    ProfileDTO = new StudentProfileDTO();
                }
            }
        }

        public async Task SaveStateAsync()
        {
            var json = JsonSerializer.Serialize(ProfileDTO);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }

        public async Task ClearStateAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            ProfileDTO = new();
        }

    }
}
