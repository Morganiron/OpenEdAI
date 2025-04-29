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
        private const string CreatingStudentKey = "creating_student_flag";
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

        public void SetProfileDTO(StudentProfileDTO profile)
        {
            ProfileDTO = profile;
        }

        public void SetUsername(string username)
        {
            Username = username;
        }

        // === CLEAR ALL ===
        public Task ClearCourseDataAsync()
        {
            return Task.WhenAll(
            ClearCoursePlanAsync(),
            ClearChatMessagesAsync(),
            ClearCourseInputAsync());
        }

        // === CREATING STUDENT METHODS ===
        public async Task MarkCreatingStudentAsync()
        {
            await _js.InvokeVoidAsync("localStorage.setItem", CreatingStudentKey, "true");
        }

        public async Task<bool> IsCreatingStudentAsync()
        {
            var value = await _js.InvokeAsync<string>("localStorage.getItem", CreatingStudentKey);
            return value == "true";
        }

        public async Task ClearCreatingStudentFlagAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", CreatingStudentKey);
        }

        // === PROFILE METHODS ===
        public async Task LoadProfileStateAsync(string currentUsername)
        {
            var profile = await LoadFromStorageAsync<StudentProfileDTO>(ProfileStorageKey, currentUsername);
            if (profile != null)
            {
                ProfileDTO = profile;
                Username = currentUsername;
            }
            else
            {
                // No cached data for this user
                ProfileDTO = new StudentProfileDTO();
                Username = null;
            }
        }

        // Persist the in-memory ProfileDTO to local storage
        public Task SaveProfileStateAsync(string username)
        {
            return SaveToStorageAsync(ProfileStorageKey, ProfileDTO, username);
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
                return default; // No data, safely return
            }

            try
            {
                var wrapper = JsonSerializer.Deserialize<UserSepecificWrapper<T>>(serialized);
                if (wrapper != null)
                {
                    var ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - wrapper.Timestamp;
                    if (ageMs < StorageExpriationHours * 60 * 60 * 1000)
                    {
                        if (wrapper.Username == currentUsername)
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

            return default;
        }
    }
}
