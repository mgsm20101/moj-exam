using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ExamSystem.Application;
using ExamSystem.Infrastructure;
using ExamSystem.Infrastructure.BackgroundJobs;
using ExamSystem.Infrastructure.Identity;
using ExamSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ExpiredAttemptSubmissionService>();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer()
    .AddJwtBearer("AttemptToken", options =>
    {
        var settings = builder.Configuration.GetSection(AttemptTokenSettings.SectionName).Get<AttemptTokenSettings>()
                       ?? new AttemptTokenSettings();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(settings.Key) ? new string('0', 32) : settings.Key))
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((bearerOptions, jwtSettingsOptions) =>
    {
        var jwtSettings = jwtSettingsOptions.Value;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',') ?? Array.Empty<string>();
        // Empty/misconfigured AllowedOrigins results in an empty array here, which CORS middleware
        // correctly treats as "no origin allowed" (fail closed) rather than falling back to AllowAnyOrigin().
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("candidate", httpContext =>
    {
        // Partition by client IP; fall back to a constant key when the IP is unavailable
        // (behind some proxies / in the test host) so the limit still applies deterministically.
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "candidate-shared";
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var permitLimit = config.GetValue<int?>("RateLimiting:Candidate:PermitLimit") ?? 20;
        var windowSeconds = config.GetValue<int?>("RateLimiting:Candidate:WindowSeconds") ?? 60;
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// ASP.NET Core resolves IWebHostEnvironment.WebRootFileProvider from whether wwwroot exists on disk
// at hosting-environment initialization -- which happens no later than WebApplicationBuilder
// construction, before any of our own code runs. If wwwroot doesn't exist yet (fresh clone/fresh
// migration -- it's runtime-generated content and not committed to git), a NullFileProvider gets
// cached and static files are never served afterwards, even after the folder is created later (e.g.
// lazily on first image upload). Creating the directory here is too late to affect the cached
// provider, so we explicitly rebuild it against the now-guaranteed-to-exist directory.
var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
app.Environment.WebRootFileProvider = new PhysicalFileProvider(webRootPath);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Runs in every environment, not just Development: shared hosting (e.g. MonsterASP.NET) gives no
// SSH/console access, so the app applying its own pending migrations on startup is the only
// available path to a schema update short of a separate CI migration job. Safe to run on every
// restart -- MigrateAsync only applies pending migrations (no-op otherwise) and both seed steps
// are idempotent (guarded by AnyAsync checks), so this can't duplicate data or reapply anything.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // SQL Server uses real migrations; non-SQL-Server providers (e.g. SQLite in integration tests, Task 8) use EnsureCreatedAsync instead.
    if (db.Database.IsSqlServer())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
    await DbInitializer.SeedAdminAsync(scope.ServiceProvider);
    await DbInitializer.SeedQuestionBankAsync(scope.ServiceProvider);
    await DbInitializer.SeedDefaultExamAsync(scope.ServiceProvider);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow }))
   .AllowAnonymous();

app.Run();

public partial class Program { }
