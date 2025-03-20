using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.IdentityModel.Tokens.Jwt;
using Amazon.CognitoIdentityProvider;
using OpenEdAI.API.Data;

var builder = WebApplication.CreateBuilder(args);

// Load configurations
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    // Load user secrets only in development
    .AddUserSecrets<Program>();

// Get the connection string for the database
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is missing. Please set 'ConnectionStrings__DefaultConnection' in environment variables.");
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();

// Disable Claims mapping
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Configure AWS options
builder.Services.Configure<AWSOptions>(builder.Configuration.GetSection("AWS"));
var userPoolId = Environment.GetEnvironmentVariable("AWS__UserPoolId")
                 ?? builder.Configuration.GetValue<string>("AWS:UserPoolId");

var appClientId = Environment.GetEnvironmentVariable("AWS__AppClientId")
                  ?? builder.Configuration.GetValue<string>("AWS:AppClientId");

var awsRegion = Environment.GetEnvironmentVariable("AWS__Region")
                ?? builder.Configuration.GetValue<string>("AWS:Region");


if (string.IsNullOrEmpty(userPoolId) || string.IsNullOrEmpty(appClientId))
{
    throw new InvalidOperationException("AWS Cognito settings are missing. Please configure them in user-secrets.");
}

// Cognito Authority
var cognitoAuthority = $"https://cognito-idp.{awsRegion}.amazonaws.com/{userPoolId}";

// Register Database Context with MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 41))
    )
);

// Optimize Token Validation with Caching
var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
    $"{cognitoAuthority}/.well-known/openid-configuration",
    new OpenIdConnectConfigurationRetriever(),
    new HttpDocumentRetriever() { RequireHttps = true }
    );

var openIdConfig = configurationManager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
var signingKeys = openIdConfig.SigningKeys;

// Configure Authentication with Cognito JWT Tokens
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

            //// Get signing keys dynamically from Cognito
            //IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            //{
            //    var config = configurationManager.GetConfigurationAsync(CancellationToken.None);
            //    return config.Result.SigningKeys;
            //},
            // Set the claim types which will be used to map the claims from the JWT token to the ClaimsPrincipal
            NameClaimType = "username",
            RoleClaimType = "cognito:groups"

        };
        // Add custom validation logic to check the client_id
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Retrieve the client_id from the principal
                var tokenClientId = context.Principal.FindFirst("client_id")?.Value;

                if (string.IsNullOrWhiteSpace(tokenClientId) || tokenClientId.Trim() != appClientId.Trim())
                {
                    context.Fail("Invalid client_id.");
                }
                return Task.CompletedTask;
            }
        };

    });


builder.Services.AddAuthorization();

// Register Swagger generator with JWT support
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

    //app.UseHttpsRedirection();

    // Enable authentication & authorization middleware
    app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Allow the app to listen on all network interfaces, but only when running in a container
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
{
    app.Urls.Add("http://+:80");
}

app.Run();
