using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Orders.Infrastructure.Configuration;
using Orders.Infrastructure.Logging;
using Serilog;

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
            new PrettyJsonTextFormatter(),
            Path.Combine(directory, "orders-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10_000_000,
            retainedFileCountLimit: 7,
            rollOnFileSizeLimit: true,
            shared: true);
    }
});

#region auth

var authScheme = builder.Configuration["Auth:Scheme"] ?? "Jwt";

builder.Services.AddAuthentication(authScheme)
    .AddJwtBearer(authScheme, options =>
    {
        var jwtSection = builder.Configuration.GetSection("Auth:Jwt");
        var signingKey = jwtSection["SigningKey"];
        ArgumentException.ThrowIfNullOrEmpty(signingKey);

        // Отключаем автоматическое переименование claims
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            // 1. Ограничиваем алгоритмы
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

            // 2. Ужесточаем проверку времени
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            // 3. Требуем наличие срока действия
            RequireExpirationTime = true,

            // 4. Указываем, где искать имя и роли
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

#endregion

builder.Services.AddAuthorization(options =>
{
    // Здесь мы добавляем политики
    options.AddPolicy("orders.write", policy => 
        policy.RequireClaim("scope", "orders.write"));
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
        Description = "Микросервис заказов: CRUD-эндпоинты и health check."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddScoped<IVault, PostgresVault>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PostgresDbContext>();

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


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program
{
}