using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OpenEdAI.Client;
using OpenEdAI.Client.Models;
using OpenEdAI.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Call AddOptions() so that .Configure<AuthConfig>() works
builder.Services.AddOptions();

// Detect the environment
var env = builder.HostEnvironment.Environment;

// Load additional config file manually if in Production
if (env == "Production")
{
    using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var jsonStream = await http.GetStreamAsync("appsettings.Production.json");
    builder.Configuration.AddJsonStream(jsonStream);
}

// Load API URL from config
var apiUrl = builder.Configuration["ApiBaseUrl"];

// Register services
builder.Services.AddAuthorizationCore();

// Register the custom JWT authentication state provider
builder.Services.AddScoped<AuthenticationStateProvider, CustomJwtAuthenticationStateProvider>();

// Register the API Authorization Message Handler
builder.Services.AddTransient<ApiAuthorizationMessageHandler>();

// Configure the HttpClient to use the authorization message handler
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<ApiAuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(apiUrl)
    };
});

// Register custom services
builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<TokenManager>();
builder.Services.AddScoped<LoadingService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<CourseGenerationService>();
builder.Services.AddScoped<LogoutService>();
builder.Services.AddScoped<CoursePersonalizationState>();
builder.Services.AddScoped<UserStateService>();
builder.Services.AddScoped<CourseProgressService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddSingleton<NotificationService>();

// Bind the AuthConfig section from appsettings.json and register it as a singleton
var authConfig = builder.Configuration.GetSection("AuthConfig").Get<AuthConfig>();
builder.Services.AddSingleton(authConfig);


var host = builder.Build();

// Retrieve the token manager and initialize it
var tokenManager = host.Services.GetRequiredService<TokenManager>();
await tokenManager.InitializeAsync();
await host.RunAsync();
