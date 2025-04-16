using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using OpenEdAI.API.Data;
using OpenEdAI.API.Services;
using Amazon.Extensions.NETCore.Setup;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.CognitoIdentityProvider;


var builder = WebApplication.CreateBuilder(args);

// Register AWSSecretsManagerService to DI container
builder.Services.AddSingleton<AWSSecretsManagerService>();

// Load configurations from appsettings.json, appsettings.Development.json, user secrets and environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Retrieve the HTTPS configuration from User Secrets (or other configuration sources)
var httpsConfig = builder.Configuration.GetSection("Https");
var certPath = httpsConfig.GetValue<string>("CertificatePath");
var certPassword = httpsConfig.GetValue<string>("CertificatePassword");

// Configure Kestrel to listen on port 80 for HTTP and 443 for HTTPS (using certificate from configuration)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5070); // Always allow HTTP for internal/ALB

    // Listen on 443 with HTTPS using the certificate path and password
    serverOptions.ListenAnyIP(7148, listenOptions =>
    {
        listenOptions.UseHttps(certPath, certPassword);
    });
});

// Add CORS service 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5239", "https://localhost:7043")
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// Conditionally load secrets from AWS Secrets Manager in not in Development. (hosted on AWS)
if (!builder.Environment.IsDevelopment())
{
    Console.WriteLine("Non-development environment detected. Loading secrets from AWS Secrets Manager...");
    var secretsManagerService = builder.Services.BuildServiceProvider().GetRequiredService<AWSSecretsManagerService>();

    // Retrieve app secrets (ClientSecret, DefaultConnection, etc.)
    var appSecrets = await secretsManagerService.GetAppSecretsAsync();
    // Add secrets to configuration
    foreach (var secret in appSecrets)
    {
        builder.Configuration[secret.Key] = secret.Value;
    }
    // Retrieve OpenAI key
var openAiKey = await secretsManagerService.GetOpenAiKeyAsync();
    builder.Configuration["OpenAi_LearningPathKey"] = openAiKey;

}
else
{
    // In development, the configuration values are expected to be present
    // in appsettings.Development.json, user secrets, or environment variables
    Console.WriteLine("Development mode detected - skipping AWS Secrets Manager integration.");
}
// Get the connection string for the database
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is missing.");
}

// Add application services
builder.Services.AddControllers();
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddHttpClient();

// Backround task and search services (backround queue capacity set to 100)
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100));
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IContentSearchService, DummyContentSearchService>();

// Disable Claims mapping
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Configure AWS options
builder.Services.Configure<AWSOptions>(builder.Configuration.GetSection("AWS"));

// Retrieve AWS Cognito settings from environment variables or configuration
var userPoolId = Environment.GetEnvironmentVariable("AWS:Cognito:UserPoolId")
                 ?? builder.Configuration.GetValue<string>("AWS:Cognito:UserPoolId");

var appClientId = Environment.GetEnvironmentVariable("AWS:Cognito:AppClientId")
                  ?? builder.Configuration.GetValue<string>("AWS:Cognito:AppClientId");

var awsRegion = Environment.GetEnvironmentVariable("AWS:Region")
                ?? builder.Configuration.GetValue<string>("AWS:Region");


// Logging for model binding failures
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

// Ensure AWS Cognito settings are provided
if (string.IsNullOrEmpty(userPoolId) || string.IsNullOrEmpty(appClientId))
{
    throw new InvalidOperationException("AWS Cognito settings are missing. Please configure.");
}

// Cognito Authority URL
var cognitoAuthority = $"https://cognito-idp.{awsRegion}.amazonaws.com/{userPoolId}";

// Register Database Context with MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 41))
    )
);

// Retireve teh OpenID Connect configuration from Cognito for token validation
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

                if (string.IsNullOrWhiteSpace(tokenClientId) || tokenClientId.Trim() != appClientId.Trim())
                {
                    context.Fail("Invalid client_id.");
                }
                return Task.CompletedTask;
            }
        };

    });

// Add authorization service
builder.Services.AddAuthorization();

// Register Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OpenEdAI API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter your JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger for all environments
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowFrontendDev");

// Debug middleware to log request paths and authentication state.
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request Path: {context.Request.Path}, Authenticated: {context.User?.Identity?.IsAuthenticated}");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
