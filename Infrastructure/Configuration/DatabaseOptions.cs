using System.ComponentModel.DataAnnotations;

namespace Orders.Infrastructure.Configuration;

public class DatabaseConnectionSettings
{
    [Required(ErrorMessage = "Connection string is required")]
    public string ConnectionString { get; set; } = default!;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableSensitiveDataLogging { get; set; } = false;
}

public class DatabasesOptions
{
    public const string SectionName = "Databases";

    [Required]
    public string ActiveDatabase { get; set; } = default!;

    [Required]
    public DatabaseConnectionSettings Sqlite { get; set; } = default!;

    [Required]
    public DatabaseConnectionSettings Postgres { get; set; } = default!;
}
