using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Amazon.Extensions.NETCore.Setup;
using OpenEdAI.Data;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.IdentityModel.Tokens.Jwt;
using Amazon.CognitoIdentityProvider;

var builder = WebApplication.CreateBuilder(args);

// Load configurations
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();

// Disable Claims mapping
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;


// Configure AWS options
builder.Services.Configure<AWSOptions>(builder.Configuration.GetSection("AWS"));
var awsRegion = builder.Configuration.GetValue<string>("AWS:Region");
var userPoolId = builder.Configuration.GetValue<string>("AWS:UserPoolId");
var appClientId = builder.Configuration.GetValue<string>("AWS:AppClientId");

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

            ValidateAudience = false, // Do not validate `aud`. It is not included in access tokens from cognito.

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // Get signing keys dynamically from Cognito
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                var config = configurationManager.GetConfigurationAsync(CancellationToken.None);
                return config.Result.SigningKeys;
            },

            NameClaimType = "username",
            //RoleClaimType = "cognito:groups"

        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                //var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                // Debug - Log all claims from the principal
                //foreach (var claim in context.Principal.Claims)
                //{
                //    logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                //}

                // Retrieve the client_id from the principal
                var tokenClientId = context.Principal.FindFirst("client_id")?.Value;

                // Debug
                //logger.LogInformation("Token client_id: '{TokenClientId}'", tokenClientId);
                //logger.LogInformation("Configured appClientId: '{AppClientId}'", appClientId);

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
        Description = "Enter 'Bearer' [space] and then your JWT token",
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

//app.UseHttpsRedirection(); // Remove, messes up authentication

// Enable authentication & authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
