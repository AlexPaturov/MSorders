using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Orders.Infrastructure.Configuration;

public static class DatabaseOptionsExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabasesOptions>()
            .Bind(configuration.GetSection(DatabasesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<SqlDbContext>((sp, options) =>
        {
            var dbs = sp.GetRequiredService<IOptions<DatabasesOptions>>().Value;
            var active = dbs.ActiveDatabase;

            if (string.IsNullOrWhiteSpace(active))
                throw new InvalidOperationException("ActiveDatabase is not configured.");

            if (string.Equals(active, "SqLite", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(active, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var settings = dbs.Sqlite;
                options.UseSqlite(settings.ConnectionString, o => o.CommandTimeout(settings.CommandTimeoutSeconds));
                if (settings.EnableSensitiveDataLogging)
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
                return;
            }

            if (string.Equals(active, "Postgres", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(active, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("PostgreSQL is planned for stage 9");
            }

            throw new InvalidOperationException($"Unknown ActiveDatabase: {active}");
        });

        return services;
    }
}
