using System.IdentityModel.Tokens.Jwt;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenAI;
using OpenEdAI.API.Configuration;
using OpenEdAI.API.Data;
using OpenEdAI.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Load all configuration sources
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Load AWS secrets only if not in Development
if (!builder.Environment.IsDevelopment())
{
    var secrets = await SecretsManagerConfigLoader.LoadSecretsAsync();
    builder.Configuration.AddInMemoryCollection(secrets);
}

// Configure Kestrel differently for Development vs Production
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Retrieve the HTTPS configuration from User Secrets (or other configuration sources)
        var httpsConfig = builder.Configuration.GetSection("Https");
        var certPath = httpsConfig.GetValue<string>("CertificatePath");
        var certPassword = httpsConfig.GetValue<string>("CertificatePassword");

        serverOptions.ListenAnyIP(5070); // HTTP
        serverOptions.ListenAnyIP(7148, listenOptions =>
        {
            listenOptions.UseHttps(certPath, certPassword); // HTTPS only in development
        });
    }
    else
    {
        // HTTP only in production (CloudFront or ALB handles HTTPS)
        serverOptions.ListenAnyIP(80); 
    }
});

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5239",            // Local DEV HTTP
            "https://localhost:7043",           // Local DEV HTTPS
            "https://openedai.morganiron.com"   // Production
            )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Logging forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Load and bind the config objects
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);

// Set cogntio settings 
var cognito = appSettings.AWS?.Cognito;
if (cognito == null || string.IsNullOrEmpty(cognito.UserPoolId) || string.IsNullOrEmpty(cognito.AppClientId))
{
    throw new InvalidOperationException("AWS Cognito settings are missing. Please configure.");
}

// Set the region
var region = appSettings.AWS?.Region;
if (string.IsNullOrEmpty(region))
{
    throw new InvalidOperationException("AWS Region is missing. Please configure.");
}

builder.Services.Configure<AppSettings>(builder.Configuration);

// Get the connection string depending on the environment
var connectionString =
    builder.Configuration["ConnectionStrings:DefaultConnection"] ??        // AppSettings or SecretsManager
    builder.Configuration.GetConnectionString("DefaultConnection") ??      // Named connection string fallback
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"); // Environment variable override

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is missing.");
}

// Register the OpenAI client with the API key from configuration
builder.Services.AddSingleton(sp =>
{
    var openAIKey = appSettings.OpenAI.LearningPathKey
    ?? throw new InvalidOperationException("Missing OpenAi:LearningPathKey");
    return new OpenAIClient(new OpenAIAuthentication(openAIKey));
});

// Register the AI-driven plan and content search services
builder.Services.AddSingleton<AIDrivenSearchPlanService>();
builder.Services.AddSingleton<IContentSearchService, AIDrivenContentSearchService>();

// Backround task and search services (backround queue capacity set to 100)
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100));
builder.Services.AddHostedService<QueuedHostedService>();

// Disable Claims mapping
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Configure AWS options
builder.Services.Configure<AWSOptions>(builder.Configuration.GetSection("AWS"));

// Cognito Authority URL
var cognitoAuthority = $"https://cognito-idp.{region}.amazonaws.com/{cognito.UserPoolId}";

// Register Database Context with MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 41))
    )
);

// Retireve the OpenID Connect configuration from Cognito for token validation
var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
    $"{cognitoAuthority}/.well-known/openid-configuration",
    new OpenIdConnectConfigurationRetriever(),
    new HttpDocumentRetriever() { RequireHttps = true }
    );

var openIdConfig = configurationManager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
var signingKeys = openIdConfig.SigningKeys;

// Configure authentication using JWT Bearer tokens, including custom validation for client_id
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = cognitoAuthority;
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cognitoAuthority,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            NameClaimType = "username",
            RoleClaimType = "cognito:groups"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Retrieve the client_id from the token and compare it with expected value
                var tokenClientId = context.Principal.FindFirst("client_id")?.Value;
                if (string.IsNullOrWhiteSpace(tokenClientId) || tokenClientId.Trim() != cognito.AppClientId.Trim())
                {
                    context.Fail("Invalid client_id.");
                }
                return Task.CompletedTask;
            }
        };
    });

// Model binding logging
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Detail = "See the errors property for more details.",
            Instance = context.HttpContext.Request.Path
        };

        Console.WriteLine("Model binding failed:");
        foreach (var kvp in context.ModelState)
        {
            foreach (var error in kvp.Value.Errors)
            {
                Console.WriteLine($" - {kvp.Key}: {error.ErrorMessage}");
            }
        }

        return new BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var strategy = context.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
        var pending = await context.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            Console.WriteLine($"Pending migrations: {string.Join(", ", pending)}");
            await context.Database.MigrateAsync();
        }
        else
        {
            Console.WriteLine("No pending migrations.");
        }
    });
}

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
LinkVet.Initialize(loggerFactory);

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy")).AllowAnonymous().ShortCircuit();

app.Run();
