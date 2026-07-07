using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Application.Features.Exams.PublishExam;
using ExamSystem.Application.Features.Questions.BulkImportQuestions;
using ExamSystem.Domain.Queue;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExamSystem.Infrastructure.Persistence;

public static class DbInitializer
{
    public const string AdminRole = "Admin";

    // Ordered to match the topic sheet in SeedData/questions_ready_for_import.xlsx; DisplayOrder mirrors this order.
    private static readonly string[] SeedTopicNames =
    {
        "أساسيات الحاسب والويندوز",
        "أمن المعلومات والإنترنت",
        "مهارات الإدخال والدقة",
        "مهارات ميكروسوفت إكسيل",
        "مهارات ميكروسوفت وورد"
    };

    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
        }

        var adminUserName = configuration["SeedAdmin:UserName"] ?? "admin";
        var adminPassword = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var existingAdmin = await userManager.FindByNameAsync(adminUserName);
        if (existingAdmin is not null)
        {
            return;
        }

        var adminUser = new ApplicationUser
        {
            UserName = adminUserName,
            Email = configuration["SeedAdmin:Email"] ?? "admin@examsystem.local",
            EmailConfirmed = true,
            FullName = "System Administrator"
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, AdminRole);
        }
        else
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(nameof(DbInitializer));
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            logger.LogError("Failed to seed admin user '{AdminUserName}': {Errors}", adminUserName, errors);
            throw new InvalidOperationException($"Failed to seed admin user '{adminUserName}': {errors}");
        }
    }

    // Re-imports the bundled question bank (docs/question-bank/questions_ready_for_import.xlsx, copied to
    // SeedData/ at build time) through the same bulk-import pipeline the admin UI uses, so a freshly created
    // database isn't left empty after the .mdf/.ldf files are wiped or a new environment is stood up.
    public static async Task SeedQuestionBankAsync(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<IApplicationDbContext>();
        if (await db.Questions.AnyAsync())
        {
            return;
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "questions_ready_for_import.xlsx");
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DbInitializer));

        if (!File.Exists(seedPath))
        {
            logger.LogWarning("Question bank seed file not found at {SeedPath}; skipping question bank seeding.", seedPath);
            return;
        }

        var sender = serviceProvider.GetRequiredService<ISender>();
        await using var stream = File.OpenRead(seedPath);
        var result = await sender.Send(new BulkImportQuestionsCommand(stream));

        if (result.IsSuccess && result.Value is { } report)
        {
            logger.LogInformation(
                "Seeded question bank: {SuccessCount}/{TotalRows} imported ({FailureCount} failed).",
                report.SuccessCount, report.TotalRows, report.FailureCount);
        }
        else
        {
            logger.LogError("Failed to seed question bank: {Errors}", string.Join("; ", result.Errors));
        }
    }

    // Seeds one ready-to-use exam matching the standard composition: 30 questions (25 MCQ + 5 FillBlank,
    // Medium/Hard only) worth 75 marks total (McqPoints=2 x25=50, FillBlankPoints=5 x5=25). The per-topic
    // split respects the bundled bank's inventory (SeedTopicNames[2] has no MCQ questions, so it only
    // contributes its one FillBlank question) and is published immediately so candidates can enter right away.
    public static async Task SeedDefaultExamAsync(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<IApplicationDbContext>();
        if (await db.Exams.AnyAsync())
        {
            return;
        }

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DbInitializer));

        var topics = await db.Topics
            .Where(t => SeedTopicNames.Contains(t.Name))
            .ToListAsync();

        if (topics.Count != SeedTopicNames.Length)
        {
            logger.LogWarning(
                "Expected {Expected} seed topics but found {Actual}; skipping default exam seeding.",
                SeedTopicNames.Length, topics.Count);
            return;
        }

        Guid TopicId(int index) => topics.First(t => t.Name == SeedTopicNames[index]).Id;

        var selections = new List<ExamTopicSelectionInput>
        {
            new(TopicId(0), 1, DifficultyLevel.Medium, QuestionType.Mcq, 3),
            new(TopicId(0), 1, DifficultyLevel.Hard, QuestionType.Mcq, 3),
            new(TopicId(0), 1, DifficultyLevel.Medium, QuestionType.FillBlank, 1),

            new(TopicId(1), 2, DifficultyLevel.Medium, QuestionType.Mcq, 3),
            new(TopicId(1), 2, DifficultyLevel.Hard, QuestionType.Mcq, 3),
            new(TopicId(1), 2, DifficultyLevel.Medium, QuestionType.FillBlank, 1),

            new(TopicId(2), 3, DifficultyLevel.Medium, QuestionType.FillBlank, 1),

            new(TopicId(3), 4, DifficultyLevel.Medium, QuestionType.Mcq, 3),
            new(TopicId(3), 4, DifficultyLevel.Hard, QuestionType.Mcq, 3),
            new(TopicId(3), 4, DifficultyLevel.Medium, QuestionType.FillBlank, 1),

            new(TopicId(4), 5, DifficultyLevel.Medium, QuestionType.Mcq, 4),
            new(TopicId(4), 5, DifficultyLevel.Hard, QuestionType.Mcq, 3),
            new(TopicId(4), 5, DifficultyLevel.Medium, QuestionType.FillBlank, 1)
        };

        var now = DateTime.UtcNow;
        var createCommand = new CreateExamCommand(
            Name: "اختبار قياس المهارات الأساسية",
            Description: "اختبار قياس المهارات الأساسية — 25 سؤال اختيار من متعدد + 5 أسئلة إكمال، من 75 درجة.",
            StartAtUtc: now,
            EndAtUtc: now.AddYears(1),
            DurationMinutes: 45,
            McqPoints: 2m,
            TrueFalsePoints: 1m,
            FillBlankPoints: 5m,
            PassMarkPercentage: 60m,
            MaxAttempts: 1,
            ShuffleAnswers: true,
            ShowResultImmediately: true,
            AllowBackNavigation: true,
            MaxConcurrentAttempts: 20,
            GraceWindowMinutes: 3,
            QueueMode: QueueMode.Auto,
            TopicSelections: selections);

        var sender = serviceProvider.GetRequiredService<ISender>();
        var createResult = await sender.Send(createCommand);

        if (!createResult.IsSuccess)
        {
            logger.LogError("Failed to seed default exam: {Errors}", string.Join("; ", createResult.Errors));
            return;
        }

        var publishResult = await sender.Send(new PublishExamCommand(createResult.Value));
        if (!publishResult.IsSuccess)
        {
            logger.LogError("Failed to publish default exam: {Errors}", string.Join("; ", publishResult.Errors));
        }
        else
        {
            logger.LogInformation("Seeded and published default exam '{Name}'.", createCommand.Name);
        }
    }
}
