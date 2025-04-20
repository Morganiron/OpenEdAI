using OpenEdAI.Client.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace OpenEdAI.Client.Services
{
    public class CoursePersonalizationState
    {
        private const string StorageKey = "course_personalization_input";
        private readonly IJSRuntime _js;
        private readonly ILogger _logger;
        public CoursePersonalizationInput Input { get; set; } = new();

        public CoursePersonalizationState(IJSRuntime js, ILogger logger)
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
                    Input = JsonSerializer.Deserialize<CoursePersonalizationInput>(json) ?? new() ;
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"Error deserializing CoursePersonalizationInput: {ex.Message}");
                    Input = new CoursePersonalizationInput();
                }
            }
        }

        public async Task SaveStateAsync()
        {
            var json = JsonSerializer.Serialize(Input);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        public async Task ClearStateAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            Input = new();
        }
    }
}
