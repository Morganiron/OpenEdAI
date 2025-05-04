using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenEdAI.API.Configuration;

namespace OpenEdAI.API.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Determine the environment: default to Develpoment if not set
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE__ENVIRONMENT") ?? "Development";

            // Start with basic config sources
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables();

            // Build intermediate config to detect env and optionally pull AWS secrets
            var interimConfig = configBuilder.Build();

            if (!environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                var secrets = SecretsManagerConfigLoader.LoadSecretsAsync().GetAwaiter().GetResult();
                configBuilder.AddInMemoryCollection(secrets);
            }

            var config = configBuilder.Build();

            var connectionString =
                config["ConnectionStrings:DefaultConnection"] ??
                config.GetConnectionString("DefaultConnection") ??
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is missing.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
