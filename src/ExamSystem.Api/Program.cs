using System.Text;
using System.Text.Json.Serialization;
using ExamSystem.Application;
using ExamSystem.Infrastructure;
using ExamSystem.Infrastructure.Identity;
using ExamSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
    .AddJwtBearer();

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

// ASP.NET Core resolves IWebHostEnvironment.WebRootFileProvider once, at Build() time. If wwwroot
// doesn't exist on disk yet (e.g. fresh clone/fresh migration, since wwwroot is runtime-generated
// content and not committed to git), it caches a NullFileProvider and static files are never served
// afterwards -- even if the folder is created later (e.g. lazily on first image upload).
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

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
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow }))
   .AllowAnonymous();

app.Run();

public partial class Program { }
