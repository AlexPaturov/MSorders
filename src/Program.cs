using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
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
        
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var sub = context.Principal?.FindFirstValue("sub");

                if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
                {
                    context.Fail("The 'sub' claim is missing or is not a valid GUID.");
                }

                return Task.CompletedTask;
            }
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Добавляем описание схемы безопасности (кнопку Authorize)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",              // Имя заголовка
        Type = SecuritySchemeType.Http,      // Тип: HTTP
        Scheme = "bearer",                   // Схема: Bearer (Swagger сам добавит слово "Bearer" перед токеном)
        BearerFormat = "JWT",                // Просто для информации в UI
        In = ParameterLocation.Header,       // Где передается токен (в заголовке)
        Description = "Введите JWT токен (без слова 'Bearer')."
    });

    // Говорим Swagger применять эту схему ко всем эндпоинтам по умолчанию
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>() // Список скоупов (если нужны)
        }
    });
    
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
    app.MapPost("/dev/token", (DevTokenRequest request) =>
        {
            if (request.UserId == Guid.Empty)
                return Results.BadRequest("userId must be a non-empty GUID.");

            var jwt = app.Configuration.GetSection("Auth:Jwt");
            var signingKey = jwt["SigningKey"];

            if (string.IsNullOrEmpty(signingKey))
                return Results.Problem(
                    "Auth:Jwt:SigningKey is not configured.",
                    statusCode: StatusCodes.Status500InternalServerError);

            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", request.UserId.ToString()),
                new Claim("name", request.Name ?? "Dev User"),
                new Claim("scope", "orders.write"),
                new Claim("jti", Guid.NewGuid().ToString())
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateJwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                subject: new ClaimsIdentity(claims),
                notBefore: now.AddMinutes(-1),
                expires: now.AddMinutes(60),
                issuedAt: now,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    SecurityAlgorithms.HmacSha256));

            return Results.Ok(new
            {
                access_token = handler.WriteToken(token),
                token_type = "Bearer",
                expires_in = 3600
            });
        })
        .AllowAnonymous();
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

public sealed record DevTokenRequest(Guid UserId, string? Name = null);

public partial class Program
{
}