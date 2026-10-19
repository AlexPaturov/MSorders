using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Orders.Infrastructure.Configuration;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    if (!context.HostingEnvironment.IsEnvironment("Testing"))
    {
        var directory = context.Configuration["FileLogging:Directory"] ?? "logs";
        if (!Path.IsPathRooted(directory))
            directory = Path.Combine(AppContext.BaseDirectory, directory);
        Directory.CreateDirectory(directory);

        configuration.WriteTo.File(
            new CompactJsonFormatter(),
            Path.Combine(directory, "orders-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10_000_000,
            retainedFileCountLimit: 7,
            rollOnFileSizeLimit: true,
            shared: true);
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Orders API",
        Version = "v1",
        Description = "Учебный микросервис заказов: CRUD-эндпоинты и health check."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddScoped<IVault, SqlVault>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SqlDbContext>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.Use((context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
            context.Response.Headers.CacheControl = "no-store";
        return next();
    });

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
