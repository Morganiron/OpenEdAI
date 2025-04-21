using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.JSInterop;
using OpenEdAI.Client.Components;
using OpenEdAI.Client.Models;

namespace OpenEdAI.Client.Services
{
    public class UserStateService
    {
        private const string ProfileStorageKey = "student_profile_data";
        private const string PlanStorageKey = "cached_generated_plan";
        private const string ChatMessageKey = "cached_chat_messages";
        private const string InputStorageKey = "cached_course_input";
        private const int StorageExpriationHours = 1;

        private readonly IJSRuntime _js;
        private readonly ILogger _logger;

        public StudentProfileDTO ProfileDTO { get; private set; } = new();
        public string? Username { get; private set; } = string.Empty;
        public CoursePlanDTO? CoursePlan { get; private set; }
        public List<CoursePlanChat.ChatMessage> ChatMessages { get; private set; } = new();
        public CoursePersonalizationInput? LastInput { get; private set; }

        public UserStateService(IJSRuntime js, ILogger<UserStateService> logger)
        {
            _js = js;
            _logger = logger;
        }

        private class UserSepecificWrapper<T>
        {
            public string Username { get; set; }
            public T Data { get; set; }
            public long Timestamp { get; set; }
        }

        private class SavedProfile
        {
            public string Username { get; set; }
            public StudentProfileDTO Profile { get; set; }
        }

        // === SETTERS ===
        public void SetCoursePlan(CoursePlanDTO plan)
        {
            CoursePlan = plan;
        }

        public void SetChatMessages(List<CoursePlanChat.ChatMessage> messages)
        {
            ChatMessages = messages;
        }

        public void SetLastInput(CoursePersonalizationInput input)
        {
            LastInput = input;
        }

        // === CLEAR ALL ===
        public async void ClearCourseDataAsync()
        {
            await ClearCoursePlanAsync();
            await ClearChatMessagesAsync();
            await ClearCourseInputAsync();
        }

        // === PROFILE METHODS ===
        public async Task LoadProfileStateAsync()
        {
            var json = await _js.InvokeAsync<string>("localStorage.getItem", ProfileStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<SavedProfile>(json);
                    if (saved != null)
                    {
                        Username = saved.Username;
                        ProfileDTO = saved.Profile ?? new();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing StudentProfileDTO:");
                    ProfileDTO = new StudentProfileDTO();
                    Username = null;
                }
            }
        }

        public async Task SaveProfileStateAsync(string username)
        {
            var saved = new SavedProfile
            {
                Username = username,
                Profile = ProfileDTO
            };
            var json = JsonSerializer.Serialize(saved);
            await _js.InvokeVoidAsync("localStorage.setItem", ProfileStorageKey, json);
        }

        public async Task ClearProfileStateAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", ProfileStorageKey);
            ProfileDTO = new();
            Username = null;
        }

        // === COURSE PLAN METHODS ===
        public async Task LoadCoursePlanAsync(string currentUsername)
        {
            CoursePlan = await LoadFromStorageAsync<CoursePlanDTO>(PlanStorageKey, currentUsername);
        }

        public async Task SaveCoursePlanAsync(string username)
        {
            if (CoursePlan != null)
                await SaveToStorageAsync(PlanStorageKey, CoursePlan, username);
        }

        public async Task ClearCoursePlanAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", PlanStorageKey);
            CoursePlan = null;
        }

        // === CHAT MESSAGE METHODS ===
        public async Task LoadChatMessagesAsync(string currentUsername)
        {
            ChatMessages = await LoadFromStorageAsync<List<CoursePlanChat.ChatMessage>>(ChatMessageKey, currentUsername) ?? new();
        }

        public async Task SaveChatMessagesAsync(string username)
        {
            if (ChatMessages != null)
                await SaveToStorageAsync(ChatMessageKey, ChatMessages, username);
        }

        public async Task ClearChatMessagesAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", ChatMessageKey);
            ChatMessages = new();
        }

        // === COURSE INPUT METHODS ===
        public async Task LoadCourseInputAsync(string currentUsername)
        {
            LastInput = await LoadFromStorageAsync<CoursePersonalizationInput>(InputStorageKey, currentUsername);
        }

        public async Task SaveCourseInputAsync(string username)
        {
            if (LastInput != null)
                await SaveToStorageAsync(InputStorageKey, LastInput, username);
        }

        public async Task ClearCourseInputAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", InputStorageKey);
            LastInput = null;
        }

        // === GENERIC HELPER METHODS ===
        private async Task SaveToStorageAsync<T>(string key, T data, string username)
        {
            var wrapper = new UserSepecificWrapper<T>
            {
                Username = username,
                Data = data,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonSerializer.Serialize(wrapper);
            await _js.InvokeVoidAsync("localStorage.setItem", key, json);
        }

        private async Task <T?> LoadFromStorageAsync<T>(string key, string currentUsername)
        {
            var serialized = await _js.InvokeAsync<string>("localStorage.getItem", key);

            if (string.IsNullOrEmpty(serialized))
            {
                try
                {
                    var wrapper = JsonSerializer.Deserialize<UserSepecificWrapper<T>>(serialized);
                    if (wrapper != null)
                    {
                        var ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - wrapper.Timestamp;
                        if (ageMs < StorageExpriationHours * 60 * 60 * 1000)
                        {
                            if(wrapper.Username == currentUsername)
                            {
                                return wrapper.Data;
                            }
                            else
                            {
                                // Different user, clear it
                                await _js.InvokeVoidAsync("localStorage.removeItem", key);
                            }
                        }
                        else
                        {
                            // Expired, clear it
                            await _js.InvokeVoidAsync("localStorage.removeItem", key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading {key} from localStorage");
                }
            }
            return default;
        }
    }
}
