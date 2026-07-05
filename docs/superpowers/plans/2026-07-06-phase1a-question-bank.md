# Phase 1a — Question Bank Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Admin full CRUD over Topics and Questions (MCQ + Fill-in-the-Blank, per PRD v1.3 — no True/False in the current question sets but the schema supports it), enforce the FillBlank single-lowercase-word rule (FR-3.2.1), support question images (FR-3.3), and ship a Bulk Excel Import (FR-3.4) that can load the already-prepared `docs/question-bank/questions_ready_for_import.json` dataset (300 MCQ + 52 FillBlank) into a real database.

**Architecture:** Extends the existing Clean Architecture skeleton from Phase 0. Introduces an `IApplicationDbContext` abstraction in `Application` (so CQRS handlers/validators can query EF Core without `Application` referencing `Infrastructure`), two new Domain aggregates (`Topic`, `Question` + `QuestionOption`), CQRS commands/queries per entity, a local-disk image upload service, and a ClosedXML-based bulk importer that returns a per-row validation report instead of failing the whole batch. Frontend adds two new lazy-loaded admin feature areas (`features/admin/topics`, `features/admin/questions`) under the existing `admin` route, using PrimeNG table/dialog components already imported in Task 9 of Phase 0.

**Tech Stack:** .NET 8, EF Core 8 (SQL Server), MediatR, FluentValidation, ClosedXML (bulk import), ASP.NET Core static file serving (question images), xUnit + Moq + SQLite integration tests, Angular 17 standalone + PrimeNG + Reactive Forms.

**Scope note:** This plan covers FR-3 (Question Bank) only. FR-4 (Exam Configuration, Topic×Difficulty publish validation) is deliberately a separate plan (Phase 1b) because it depends on this one being done and merged first — each plan must ship independently testable software per the writing-plans skill's scope-check rule.

---

## Prerequisites (verify before Task 1)

- [ ] **Step 1: Confirm Phase 0 is merged and the solution builds**

Run: `dotnet build` from `D:/os/ExamSystem`
Expected: `Build succeeded.`

- [ ] **Step 2: Confirm the prepared question-bank data exists**

Run: `ls docs/question-bank/questions_ready_for_import.json`
Expected: file exists (352 questions — created in a prior session; see `docs/question-bank/import_validation_report.txt` for known data caveats: no Easy-difficulty questions, one topic with only 1 FillBlank question).

---

## File Structure (target state after this plan)

```
D:/os/ExamSystem/
├─ src/
│  ├─ ExamSystem.Domain/
│  │  ├─ Topics/Topic.cs
│  │  └─ Questions/
│  │     ├─ Question.cs
│  │     ├─ QuestionOption.cs
│  │     ├─ QuestionType.cs
│  │     └─ DifficultyLevel.cs
│  ├─ ExamSystem.Application/
│  │  ├─ ExamSystem.Application.csproj          (Modify: add EF Core package)
│  │  ├─ Common/Interfaces/IApplicationDbContext.cs
│  │  ├─ Common/Interfaces/IImageStorageService.cs
│  │  └─ Features/
│  │     ├─ Topics/
│  │     │  ├─ CreateTopic/{CreateTopicCommand,CreateTopicCommandValidator,CreateTopicCommandHandler}.cs
│  │     │  ├─ UpdateTopic/{UpdateTopicCommand,UpdateTopicCommandValidator,UpdateTopicCommandHandler}.cs
│  │     │  ├─ DeleteTopic/{DeleteTopicCommand,DeleteTopicCommandHandler}.cs
│  │     │  └─ GetTopics/{GetTopicsQuery,TopicDto,GetTopicsQueryHandler}.cs
│  │     └─ Questions/
│  │        ├─ CreateQuestion/{CreateQuestionCommand,CreateQuestionCommandValidator,CreateQuestionCommandHandler}.cs
│  │        ├─ UpdateQuestion/{UpdateQuestionCommand,UpdateQuestionCommandValidator,UpdateQuestionCommandHandler}.cs
│  │        ├─ DeleteQuestion/{DeleteQuestionCommand,DeleteQuestionCommandHandler}.cs
│  │        ├─ GetQuestions/{GetQuestionsQuery,QuestionDto,GetQuestionsQueryHandler}.cs
│  │        └─ BulkImportQuestions/{BulkImportQuestionsCommand,BulkImportRowResult,BulkImportQuestionsCommandHandler}.cs
│  ├─ ExamSystem.Infrastructure/
│  │  ├─ Persistence/
│  │  │  ├─ ApplicationDbContext.cs             (Modify: implement IApplicationDbContext)
│  │  │  └─ Configurations/{TopicConfiguration,QuestionConfiguration,QuestionOptionConfiguration}.cs
│  │  ├─ Files/LocalImageStorageService.cs
│  │  └─ Migrations/*                            (generated)
│  └─ ExamSystem.Api/
│     ├─ Program.cs                              (Modify: static files for question images)
│     └─ Controllers/{TopicsController,QuestionsController}.cs
├─ tests/
│  ├─ ExamSystem.Application.UnitTests/Features/{Topics,Questions}/*Tests.cs
│  └─ ExamSystem.Api.IntegrationTests/Controllers/{TopicsControllerTests,QuestionsControllerTests}.cs
└─ frontend/src/app/
   ├─ core/services/{topic.service.ts,question.service.ts}
   └─ features/admin/
      ├─ topics/topics-list.component.{ts,html,spec.ts}
      └─ questions/
         ├─ questions-list.component.{ts,html,spec.ts}
         ├─ question-form.component.{ts,html,spec.ts}
         └─ bulk-import.component.{ts,html,spec.ts}
```

---

### Task 1: Domain — Topic, Question, QuestionOption

**Files:**
- Create: `src/ExamSystem.Domain/Topics/Topic.cs`
- Create: `src/ExamSystem.Domain/Questions/QuestionType.cs`
- Create: `src/ExamSystem.Domain/Questions/DifficultyLevel.cs`
- Create: `src/ExamSystem.Domain/Questions/Question.cs`
- Create: `src/ExamSystem.Domain/Questions/QuestionOption.cs`
- Modify: `src/ExamSystem.Domain/GlobalUsings.cs`

- [ ] **Step 1: Write `Topic`**

```csharp
namespace ExamSystem.Domain.Topics;

public class Topic : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
```

- [ ] **Step 2: Write `QuestionType` and `DifficultyLevel` enums**

`src/ExamSystem.Domain/Questions/QuestionType.cs`:
```csharp
namespace ExamSystem.Domain.Questions;

public enum QuestionType
{
    Mcq = 0,
    TrueFalse = 1,
    FillBlank = 2
}
```

`src/ExamSystem.Domain/Questions/DifficultyLevel.cs`:
```csharp
namespace ExamSystem.Domain.Questions;

public enum DifficultyLevel
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}
```

- [ ] **Step 3: Write `Question`**

```csharp
using ExamSystem.Domain.Topics;

namespace ExamSystem.Domain.Questions;

public class Question : BaseAuditableEntity
{
    public Guid TopicId { get; set; }
    public Topic? Topic { get; set; }

    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public QuestionType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }

    /// <summary>FillBlank only — always lowercase, single word, matches ^[a-z0-9]+$ (FR-3.2.1).</summary>
    public string? CorrectAnswerText { get; set; }

    public decimal? PointsOverride { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
```

- [ ] **Step 4: Write `QuestionOption`**

```csharp
namespace ExamSystem.Domain.Questions;

public class QuestionOption : BaseEntity
{
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}
```

- [ ] **Step 5: Register new namespaces in `GlobalUsings.cs`**

Modify `src/ExamSystem.Domain/GlobalUsings.cs`:
```csharp
global using ExamSystem.Domain.Common;
global using ExamSystem.Domain.Topics;
global using ExamSystem.Domain.Questions;
```

- [ ] **Step 6: Build the Domain project**

Run: `dotnet build src/ExamSystem.Domain/ExamSystem.Domain.csproj`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Domain
git commit -m "feat(domain): add Topic, Question, and QuestionOption entities"
```

---

### Task 2: Application/Infrastructure — `IApplicationDbContext`, EF Configurations, Migration

**Files:**
- Modify: `src/ExamSystem.Application/ExamSystem.Application.csproj`
- Create: `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `src/ExamSystem.Application/GlobalUsings.cs`
- Modify: `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/TopicConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/QuestionConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/QuestionOptionConfiguration.cs`

- [ ] **Step 1: Add the EF Core abstractions package to Application**

Run:
```bash
dotnet add src/ExamSystem.Application/ExamSystem.Application.csproj package Microsoft.EntityFrameworkCore --version 8.0.10
```
Expected: "Restored ..." with no errors. This adds only the EF Core abstractions (`DbSet<T>`), not a provider — `Application` still has zero dependency on `Infrastructure` or SQL Server.

- [ ] **Step 2: Write `IApplicationDbContext`**

```csharp
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Topic> Topics { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Register the interfaces namespace**

Modify `src/ExamSystem.Application/GlobalUsings.cs`:
```csharp
global using MediatR;
global using FluentValidation;
global using Microsoft.Extensions.DependencyInjection;
global using ExamSystem.Application.Common.Models;
global using ExamSystem.Application.Common.Interfaces;
```

- [ ] **Step 4: Implement `IApplicationDbContext` on `ApplicationDbContext`**

Modify `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`:
```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Write `TopicConfiguration`**

```csharp
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
```

- [ ] **Step 6: Write `QuestionConfiguration`**

```csharp
using ExamSystem.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(q => q.Text).IsRequired();
        builder.Property(q => q.CorrectAnswerText).HasMaxLength(50);
        builder.Property(q => q.PointsOverride).HasColumnType("decimal(5,2)");

        builder.HasOne(q => q.Topic)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.TopicId, q.Difficulty, q.IsActive });
    }
}
```

- [ ] **Step 7: Write `QuestionOptionConfiguration`**

```csharp
using ExamSystem.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.Property(o => o.Text).IsRequired();

        builder.HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 8: Build the solution**

Run: `dotnet build`
Expected: `Build succeeded.` (0 errors — `Application` still has no reference to `Infrastructure` or any EF provider package).

- [ ] **Step 9: Generate the migration**

Run:
```bash
dotnet ef migrations add AddTopicsAndQuestions --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: "Done." and new files under `src/ExamSystem.Infrastructure/Migrations/`.

- [ ] **Step 10: Apply the migration to LocalDB**

Run:
```bash
dotnet ef database update --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: "Done." — `Topics`, `Questions`, `QuestionOptions` tables created on `(localdb)\mssqllocaldb`.

- [ ] **Step 11: Commit**

```bash
git add src/ExamSystem.Application src/ExamSystem.Infrastructure
git commit -m "feat(infrastructure): add IApplicationDbContext abstraction, EF configurations, and Topics/Questions migration"
```

---

### Task 3: Application — Topics CQRS (Create, Update, Delete, GetAll) — TDD

**Files:**
- Create: `src/ExamSystem.Application/Features/Topics/CreateTopic/CreateTopicCommand.cs`
- Create: `src/ExamSystem.Application/Features/Topics/CreateTopic/CreateTopicCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/Topics/CreateTopic/CreateTopicCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Topics/UpdateTopic/UpdateTopicCommand.cs`
- Create: `src/ExamSystem.Application/Features/Topics/UpdateTopic/UpdateTopicCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Topics/DeleteTopic/DeleteTopicCommand.cs`
- Create: `src/ExamSystem.Application/Features/Topics/DeleteTopic/DeleteTopicCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Topics/GetTopics/GetTopicsQuery.cs`
- Create: `src/ExamSystem.Application/Features/Topics/GetTopics/TopicDto.cs`
- Create: `src/ExamSystem.Application/Features/Topics/GetTopics/GetTopicsQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Topics/CreateTopicCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Topics/DeleteTopicCommandHandlerTests.cs`

This task uses EF Core's `Microsoft.EntityFrameworkCore.InMemory` provider in unit tests to back a real `IApplicationDbContext`-shaped fake, because mocking `DbSet<T>` with Moq for LINQ queries is unreliable. Add the package first.

- [ ] **Step 1: Add the InMemory provider to the unit test project**

Run:
```bash
dotnet add tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 8.0.10
dotnet add tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj reference src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj
```
Expected: both succeed. (Referencing `Infrastructure` from the unit test project is acceptable here -- only test code takes the dependency, to reuse the real `ApplicationDbContext`; `Application` production code still never references `Infrastructure`.)

- [ ] **Step 2: Write a shared test helper for an in-memory `ApplicationDbContext`**

Create `tests/ExamSystem.Application.UnitTests/TestDbContextFactory.cs`:
```csharp
using ExamSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.UnitTests;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
```

- [ ] **Step 3: Write the failing test for `CreateTopicCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Topics/CreateTopicCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Topics.CreateTopic;

namespace ExamSystem.Application.UnitTests.Features.Topics;

public class CreateTopicCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidTopic_PersistsAndReturnsId()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new CreateTopicCommandHandler(db);

        var result = await handler.Handle(new CreateTopicCommand("Excel Skills", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.Topics);
        Assert.Equal(result.Value, db.Topics.Single().Id);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        db.Topics.Add(new ExamSystem.Domain.Topics.Topic { Name = "Excel Skills", DisplayOrder = 1 });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateTopicCommandHandler(db);
        var result = await handler.Handle(new CreateTopicCommand("Excel Skills", 2), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Topic name already exists.", result.Errors);
    }
}
```

*(Error message language: Phase 0 established English strings in `Application`-layer `Result<T>.Failure(...)` messages — e.g. `LoginCommandHandler`'s "Invalid username or password." — with Arabic reserved for the Angular UI layer, e.g. `LoginComponent`'s `errorMessage`. This plan keeps that convention: all Application/API messages below are English; Angular components translate them to Arabic for display.)*

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateTopicCommandHandlerTests`
Expected: FAIL to compile -- `CreateTopicCommand`/`CreateTopicCommandHandler` do not exist yet.

- [ ] **Step 5: Implement `CreateTopicCommand`, validator, and handler**

`src/ExamSystem.Application/Features/Topics/CreateTopic/CreateTopicCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.CreateTopic;

public record CreateTopicCommand(string Name, int DisplayOrder) : IRequest<Result<Guid>>;
```

`src/ExamSystem.Application/Features/Topics/CreateTopic/CreateTopicCommandValidator.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.CreateTopic;

public class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Topic name is required.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage("Display order cannot be negative.");
    }
}
```

`src/ExamSystem.Application/Features/Topics/CreateTopic/CreateTopicCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.Features.Topics.CreateTopic;

public class CreateTopicCommandHandler : IRequestHandler<CreateTopicCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateTopicCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await _db.Topics.AnyAsync(t => t.Name == request.Name, cancellationToken);
        if (nameExists)
        {
            return Result<Guid>.Failure("Topic name already exists.");
        }

        var topic = new Topic { Name = request.Name, DisplayOrder = request.DisplayOrder };
        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(topic.Id);
    }
}
```

- [ ] **Step 6: Add the missing global using**

Modify `src/ExamSystem.Application/GlobalUsings.cs` to add:
```csharp
global using Microsoft.EntityFrameworkCore;
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateTopicCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 8: Write `UpdateTopicCommand` + handler (same conventions as Step 5; covered end-to-end by the integration test in Task 4)**

`src/ExamSystem.Application/Features/Topics/UpdateTopic/UpdateTopicCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.UpdateTopic;

public record UpdateTopicCommand(Guid Id, string Name, int DisplayOrder, bool IsActive) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Topics/UpdateTopic/UpdateTopicCommandHandler.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.UpdateTopic;

public class UpdateTopicCommandHandler : IRequestHandler<UpdateTopicCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public UpdateTopicCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return Result<Unit>.Failure("Topic not found.");
        }

        var duplicateName = await _db.Topics.AnyAsync(t => t.Id != request.Id && t.Name == request.Name, cancellationToken);
        if (duplicateName)
        {
            return Result<Unit>.Failure("Topic name already exists.");
        }

        topic.Name = request.Name;
        topic.DisplayOrder = request.DisplayOrder;
        topic.IsActive = request.IsActive;
        topic.ModifiedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 9: Write the failing test for `DeleteTopicCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Topics/DeleteTopicCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Topics.DeleteTopic;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Topics;

public class DeleteTopicCommandHandlerTests
{
    [Fact]
    public async Task Handle_TopicWithNoQuestions_DeletesIt()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Empty Topic", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteTopicCommandHandler(db);
        var result = await handler.Handle(new DeleteTopicCommand(topic.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(db.Topics);
    }

    [Fact]
    public async Task Handle_TopicWithQuestions_ReturnsFailureAndKeepsIt()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Has Questions", DisplayOrder = 1 };
        topic.Questions.Add(new Question { Text = "Q", Type = QuestionType.FillBlank, Difficulty = DifficultyLevel.Medium, CorrectAnswerText = "a" });
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteTopicCommandHandler(db);
        var result = await handler.Handle(new DeleteTopicCommand(topic.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot delete a topic that has questions -- deactivate it instead.", result.Errors);
        Assert.Single(db.Topics);
    }
}
```

- [ ] **Step 10: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter DeleteTopicCommandHandlerTests`
Expected: FAIL to compile -- `DeleteTopicCommand`/`DeleteTopicCommandHandler` do not exist yet.

- [ ] **Step 11: Implement `DeleteTopicCommand` and handler**

`src/ExamSystem.Application/Features/Topics/DeleteTopic/DeleteTopicCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.DeleteTopic;

public record DeleteTopicCommand(Guid Id) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Topics/DeleteTopic/DeleteTopicCommandHandler.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.DeleteTopic;

public class DeleteTopicCommandHandler : IRequestHandler<DeleteTopicCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public DeleteTopicCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(DeleteTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (topic is null)
        {
            return Result<Unit>.Failure("Topic not found.");
        }

        if (topic.Questions.Count > 0)
        {
            return Result<Unit>.Failure("Cannot delete a topic that has questions -- deactivate it instead.");
        }

        _db.Topics.Remove(topic);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 12: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter DeleteTopicCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 13: Write `GetTopicsQuery`, `TopicDto`, and handler**

`src/ExamSystem.Application/Features/Topics/GetTopics/TopicDto.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.GetTopics;

public record TopicDto(Guid Id, string Name, int DisplayOrder, bool IsActive, int QuestionCount);
```

`src/ExamSystem.Application/Features/Topics/GetTopics/GetTopicsQuery.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.GetTopics;

public record GetTopicsQuery : IRequest<Result<List<TopicDto>>>;
```

`src/ExamSystem.Application/Features/Topics/GetTopics/GetTopicsQueryHandler.cs`:
```csharp
namespace ExamSystem.Application.Features.Topics.GetTopics;

public class GetTopicsQueryHandler : IRequestHandler<GetTopicsQuery, Result<List<TopicDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetTopicsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<TopicDto>>> Handle(GetTopicsQuery request, CancellationToken cancellationToken)
    {
        var topics = await _db.Topics
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new TopicDto(t.Id, t.Name, t.DisplayOrder, t.IsActive, t.Questions.Count))
            .ToListAsync(cancellationToken);

        return Result<List<TopicDto>>.Success(topics);
    }
}
```

- [ ] **Step 14: Run the full Application test suite**

Run: `dotnet test tests/ExamSystem.Application.UnitTests`
Expected: all existing + new tests pass, 0 failures.

- [ ] **Step 15: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add Topics CQRS commands and query with tests"
```

---

### Task 4: API — `TopicsController` + Integration Tests

**Files:**
- Create: `src/ExamSystem.Api/Controllers/TopicsController.cs`
- Modify: `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs`
- Create: `tests/ExamSystem.Api.IntegrationTests/Controllers/TopicsControllerTests.cs`

All admin CRUD endpoints require the seeded `Admin` role (FR-7.3). Integration tests need an authenticated `HttpClient` -- add a reusable helper to the factory once here, reused by every later controller test task in this plan.

- [ ] **Step 1: Add an authenticated-client helper to the test factory**

Modify `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs` -- add this method inside the `TestWebApplicationFactory` class (after `DisposeAsync`):

```csharp
    public async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "admin",
            password = SeedAdminPassword
        });
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private record LoginResponseDto(string Token, string UserName, IReadOnlyList<string> Roles);
```

Add the missing usings at the top of the file:
```csharp
using System.Net.Http.Json;
```

- [ ] **Step 2: Write the failing integration test for `TopicsController`**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/TopicsControllerTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class TopicsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TopicsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateThenGet_ReturnsTheCreatedTopic()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/admin/topics", new { name = "Excel Skills", displayOrder = 1 });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/admin/topics");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var topics = await getResponse.Content.ReadFromJsonAsync<List<TopicDto>>();
        Assert.Contains(topics!, t => t.Name == "Excel Skills");
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/topics", new { name = "No Auth", displayOrder = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record TopicDto(Guid Id, string Name, int DisplayOrder, bool IsActive, int QuestionCount);
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter TopicsControllerTests`
Expected: FAIL -- 404 Not Found (no `TopicsController` yet), or compile error if the factory helper isn't in place.

- [ ] **Step 4: Implement `TopicsController`**

```csharp
using ExamSystem.Application.Features.Topics.CreateTopic;
using ExamSystem.Application.Features.Topics.DeleteTopic;
using ExamSystem.Application.Features.Topics.GetTopics;
using ExamSystem.Application.Features.Topics.UpdateTopic;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin CRUD for exam Topics.</summary>
[ApiController]
[Route("api/admin/topics")]
[Authorize(Roles = "Admin")]
public class TopicsController : ControllerBase
{
    private readonly ISender _sender;

    public TopicsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTopicsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTopicCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateTopicCommand(id, request.Name, request.DisplayOrder, request.IsActive), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTopicCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    public record UpdateTopicRequest(string Name, int DisplayOrder, bool IsActive);
}
```

- [ ] **Step 5: Register the CQRS pipeline for the new commands**

No change needed -- `AddApplication()` in `src/ExamSystem.Application/DependencyInjection.cs` already registers MediatR/FluentValidation by scanning the whole `Application` assembly (from Phase 0 Task 4 Step 6), so the new Topics handlers and validator are picked up automatically.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter TopicsControllerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 7: Run the entire backend test suite**

Run: `dotnet test`
Expected: all projects `Passed!`, 0 failures.

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Api tests/ExamSystem.Api.IntegrationTests
git commit -m "feat(api): add TopicsController with role-gated CRUD endpoints and integration tests"
```

---

### Task 5: Application — Questions CQRS (Create, Update, Delete, GetList) — TDD

**Files:**
- Create: `src/ExamSystem.Application/Features/Questions/CreateQuestion/CreateQuestionCommand.cs`
- Create: `src/ExamSystem.Application/Features/Questions/CreateQuestion/QuestionOptionInput.cs`
- Create: `src/ExamSystem.Application/Features/Questions/CreateQuestion/CreateQuestionCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/Questions/CreateQuestion/CreateQuestionCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Questions/UpdateQuestion/UpdateQuestionCommand.cs`
- Create: `src/ExamSystem.Application/Features/Questions/UpdateQuestion/UpdateQuestionCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Questions/DeleteQuestion/DeactivateQuestionCommand.cs`
- Create: `src/ExamSystem.Application/Features/Questions/DeleteQuestion/DeactivateQuestionCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Questions/GetQuestions/GetQuestionsQuery.cs`
- Create: `src/ExamSystem.Application/Features/Questions/GetQuestions/QuestionDto.cs`
- Create: `src/ExamSystem.Application/Features/Questions/GetQuestions/GetQuestionsQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Questions/CreateQuestionCommandValidatorTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Questions/CreateQuestionCommandHandlerTests.cs`

Per FR-3.5, questions are never hard-deleted (a used question must stay intact for historical Snapshots in later phases) -- "delete" here means `IsActive = false`.

- [ ] **Step 1: Write `QuestionOptionInput`**

```csharp
namespace ExamSystem.Application.Features.Questions.CreateQuestion;

public record QuestionOptionInput(string Text, bool IsCorrect);
```

- [ ] **Step 2: Write `CreateQuestionCommand`**

```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.CreateQuestion;

public record CreateQuestionCommand(
    Guid TopicId,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string Text,
    string? ImageUrl,
    List<QuestionOptionInput>? Options,
    string? CorrectAnswerText,
    decimal? PointsOverride) : IRequest<Result<Guid>>;
```

- [ ] **Step 3: Write the failing validator tests**

Create `tests/ExamSystem.Application.UnitTests/Features/Questions/CreateQuestionCommandValidatorTests.cs`:
```csharp
using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class CreateQuestionCommandValidatorTests
{
    private static CreateQuestionCommandValidator CreateValidator(ApplicationDbContextFixtureTopic topic)
    {
        var db = TestDbContextFactory.Create();
        db.Topics.Add(new ExamSystem.Domain.Topics.Topic { Id = topic.Id, Name = topic.Name, DisplayOrder = 1, IsActive = topic.IsActive });
        db.SaveChanges();
        return new CreateQuestionCommandValidator(db);
    }

    private static readonly ApplicationDbContextFixtureTopic ActiveTopic = new(Guid.NewGuid(), "Excel", true);

    [Theory]
    [InlineData("server")]
    [InlineData("counter1")]
    public async Task FillBlank_LowercaseSingleWordAnswer_IsValid(string answer)
    {
        var validator = CreateValidator(ActiveTopic);
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.FillBlank, DifficultyLevel.Medium, "Fill ___", null, null, answer, null);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Data Base")]
    [InlineData("SERVER")]
    [InlineData("mail merge")]
    [InlineData("")]
    public async Task FillBlank_InvalidAnswerFormat_IsRejected(string answer)
    {
        var validator = CreateValidator(ActiveTopic);
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.FillBlank, DifficultyLevel.Medium, "Fill ___", null, null, answer, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Mcq_WithoutExactlyOneCorrectOption_IsRejected()
    {
        var validator = CreateValidator(ActiveTopic);
        var options = new List<QuestionOptionInput>
        {
            new("A", true),
            new("B", true),
            new("C", false)
        };
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.Mcq, DifficultyLevel.Medium, "Pick one", null, options, null, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Mcq_WithExactlyOneCorrectOption_IsValid()
    {
        var validator = CreateValidator(ActiveTopic);
        var options = new List<QuestionOptionInput>
        {
            new("A", false),
            new("B", true),
            new("C", false)
        };
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.Mcq, DifficultyLevel.Medium, "Pick one", null, options, null, null);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task InactiveTopic_IsRejected()
    {
        var inactiveTopic = new ApplicationDbContextFixtureTopic(Guid.NewGuid(), "Retired", false);
        var validator = CreateValidator(inactiveTopic);
        var command = new CreateQuestionCommand(inactiveTopic.Id, QuestionType.FillBlank, DifficultyLevel.Medium, "Fill ___", null, null, "answer", null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}

public record ApplicationDbContextFixtureTopic(Guid Id, string Name, bool IsActive);
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateQuestionCommandValidatorTests`
Expected: FAIL to compile -- `CreateQuestionCommandValidator` does not exist yet.

- [ ] **Step 5: Implement `CreateQuestionCommandValidator`**

```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.CreateQuestion;

public class CreateQuestionCommandValidator : AbstractValidator<CreateQuestionCommand>
{
    private static readonly System.Text.RegularExpressions.Regex FillBlankAnswerPattern = new("^[a-z0-9]+$");

    public CreateQuestionCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Question text is required.");

        RuleFor(x => x.TopicId)
            .MustAsync(async (topicId, ct) => await db.Topics.AnyAsync(t => t.Id == topicId && t.IsActive, ct))
            .WithMessage("Topic not found or inactive.");

        When(x => x.Type == QuestionType.FillBlank, () =>
        {
            RuleFor(x => x.CorrectAnswerText)
                .NotEmpty()
                .Must(answer => FillBlankAnswerPattern.IsMatch(answer ?? string.Empty))
                .WithMessage("FillBlank answer must be a single lowercase word (letters/digits only, no spaces).");

            RuleFor(x => x.Options)
                .Empty()
                .WithMessage("FillBlank questions must not have options.");
        });

        When(x => x.Type != QuestionType.FillBlank, () =>
        {
            RuleFor(x => x.CorrectAnswerText)
                .Empty()
                .WithMessage("CorrectAnswerText only applies to FillBlank questions.");

            RuleFor(x => x.Options)
                .Must(options => options is { Count: >= 2 })
                .WithMessage("At least 2 options are required.");

            RuleFor(x => x.Options)
                .Must(options => options!.Count(o => o.IsCorrect) == 1)
                .When(x => x.Options is { Count: >= 2 })
                .WithMessage("Exactly one option must be marked correct.");
        });
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateQuestionCommandValidatorTests`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 7: Write the failing test for `CreateQuestionCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Questions/CreateQuestionCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class CreateQuestionCommandHandlerTests
{
    [Fact]
    public async Task Handle_McqQuestion_PersistsQuestionAndOptions()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateQuestionCommandHandler(db);
        var options = new List<QuestionOptionInput> { new("A", false), new("B", true) };
        var command = new CreateQuestionCommand(topic.Id, QuestionType.Mcq, DifficultyLevel.Medium, "Pick one", null, options, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.Questions.Include(q => q.Options).Single();
        Assert.Equal(2, saved.Options.Count);
        Assert.Single(saved.Options, o => o.IsCorrect);
    }

    [Fact]
    public async Task Handle_FillBlankQuestion_PersistsWithNoOptions()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateQuestionCommandHandler(db);
        var command = new CreateQuestionCommand(topic.Id, QuestionType.FillBlank, DifficultyLevel.Hard, "Fill ___", null, null, "server", null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.Questions.Include(q => q.Options).Single();
        Assert.Empty(saved.Options);
        Assert.Equal("server", saved.CorrectAnswerText);
    }
}
```

- [ ] **Step 8: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateQuestionCommandHandlerTests`
Expected: FAIL to compile -- `CreateQuestionCommandHandler` does not exist yet.

- [ ] **Step 9: Implement `CreateQuestionCommandHandler`**

```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.CreateQuestion;

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = new Question
        {
            TopicId = request.TopicId,
            Type = request.Type,
            Difficulty = request.Difficulty,
            Text = request.Text,
            ImageUrl = request.ImageUrl,
            CorrectAnswerText = request.CorrectAnswerText,
            PointsOverride = request.PointsOverride
        };

        if (request.Options is not null)
        {
            for (var i = 0; i < request.Options.Count; i++)
            {
                question.Options.Add(new QuestionOption
                {
                    Text = request.Options[i].Text,
                    IsCorrect = request.Options[i].IsCorrect,
                    DisplayOrder = i + 1
                });
            }
        }

        _db.Questions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(question.Id);
    }
}
```

- [ ] **Step 10: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateQuestionCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 11: Write `UpdateQuestionCommand` + handler (mirrors Create; replaces options wholesale)**

`src/ExamSystem.Application/Features/Questions/UpdateQuestion/UpdateQuestionCommand.cs`:
```csharp
using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.UpdateQuestion;

public record UpdateQuestionCommand(
    Guid Id,
    Guid TopicId,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string Text,
    string? ImageUrl,
    List<QuestionOptionInput>? Options,
    string? CorrectAnswerText,
    decimal? PointsOverride,
    bool IsActive) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Questions/UpdateQuestion/UpdateQuestionCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.UpdateQuestion;

public class UpdateQuestionCommandHandler : IRequestHandler<UpdateQuestionCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public UpdateQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (question is null)
        {
            return Result<Unit>.Failure("Question not found.");
        }

        question.TopicId = request.TopicId;
        question.Type = request.Type;
        question.Difficulty = request.Difficulty;
        question.Text = request.Text;
        question.ImageUrl = request.ImageUrl;
        question.CorrectAnswerText = request.CorrectAnswerText;
        question.PointsOverride = request.PointsOverride;
        question.IsActive = request.IsActive;
        question.ModifiedAtUtc = DateTime.UtcNow;

        question.Options.Clear();
        if (request.Options is not null)
        {
            for (var i = 0; i < request.Options.Count; i++)
            {
                question.Options.Add(new QuestionOption
                {
                    Text = request.Options[i].Text,
                    IsCorrect = request.Options[i].IsCorrect,
                    DisplayOrder = i + 1
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

*(Note: register `CreateQuestionCommandValidator`'s rules for Update too by adding an `UpdateQuestionCommandValidator` that duplicates the same When-blocks against `UpdateQuestionCommand` -- omitted here for brevity; copy the body of Step 5's validator, replacing the command type. Do not skip it in the real implementation: shipping Update without validation would let an admin corrupt a FillBlank answer's format silently.)*

- [ ] **Step 12: Write `UpdateQuestionCommandValidator`**

```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.UpdateQuestion;

public class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    private static readonly System.Text.RegularExpressions.Regex FillBlankAnswerPattern = new("^[a-z0-9]+$");

    public UpdateQuestionCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Question text is required.");

        RuleFor(x => x.TopicId)
            .MustAsync(async (topicId, ct) => await db.Topics.AnyAsync(t => t.Id == topicId && t.IsActive, ct))
            .WithMessage("Topic not found or inactive.");

        When(x => x.Type == QuestionType.FillBlank, () =>
        {
            RuleFor(x => x.CorrectAnswerText)
                .NotEmpty()
                .Must(answer => FillBlankAnswerPattern.IsMatch(answer ?? string.Empty))
                .WithMessage("FillBlank answer must be a single lowercase word (letters/digits only, no spaces).");

            RuleFor(x => x.Options).Empty().WithMessage("FillBlank questions must not have options.");
        });

        When(x => x.Type != QuestionType.FillBlank, () =>
        {
            RuleFor(x => x.CorrectAnswerText).Empty().WithMessage("CorrectAnswerText only applies to FillBlank questions.");
            RuleFor(x => x.Options).Must(options => options is { Count: >= 2 }).WithMessage("At least 2 options are required.");
            RuleFor(x => x.Options)
                .Must(options => options!.Count(o => o.IsCorrect) == 1)
                .When(x => x.Options is { Count: >= 2 })
                .WithMessage("Exactly one option must be marked correct.");
        });
    }
}
```

- [ ] **Step 13: Write `DeactivateQuestionCommand` + handler (soft delete per FR-3.5)**

`src/ExamSystem.Application/Features/Questions/DeleteQuestion/DeactivateQuestionCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Questions.DeleteQuestion;

public record DeactivateQuestionCommand(Guid Id) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Questions/DeleteQuestion/DeactivateQuestionCommandHandler.cs`:
```csharp
namespace ExamSystem.Application.Features.Questions.DeleteQuestion;

public class DeactivateQuestionCommandHandler : IRequestHandler<DeactivateQuestionCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public DeactivateQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(DeactivateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);
        if (question is null)
        {
            return Result<Unit>.Failure("Question not found.");
        }

        question.IsActive = false;
        question.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 14: Write `GetQuestionsQuery`, `QuestionDto`, and handler (filterable by Topic/Difficulty, supports FR-3.7's counter)**

`src/ExamSystem.Application/Features/Questions/GetQuestions/QuestionDto.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestions;

public record QuestionOptionDto(Guid Id, string Text, bool IsCorrect, int DisplayOrder);

public record QuestionDto(
    Guid Id,
    Guid TopicId,
    string TopicName,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string Text,
    string? ImageUrl,
    string? CorrectAnswerText,
    decimal? PointsOverride,
    bool IsActive,
    List<QuestionOptionDto> Options);
```

`src/ExamSystem.Application/Features/Questions/GetQuestions/GetQuestionsQuery.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestions;

public record GetQuestionsQuery(Guid? TopicId, DifficultyLevel? Difficulty, bool? IsActive) : IRequest<Result<List<QuestionDto>>>;
```

`src/ExamSystem.Application/Features/Questions/GetQuestions/GetQuestionsQueryHandler.cs`:
```csharp
namespace ExamSystem.Application.Features.Questions.GetQuestions;

public class GetQuestionsQueryHandler : IRequestHandler<GetQuestionsQuery, Result<List<QuestionDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetQuestionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<QuestionDto>>> Handle(GetQuestionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Questions.Include(q => q.Topic).Include(q => q.Options).AsQueryable();

        if (request.TopicId is not null)
        {
            query = query.Where(q => q.TopicId == request.TopicId);
        }
        if (request.Difficulty is not null)
        {
            query = query.Where(q => q.Difficulty == request.Difficulty);
        }
        if (request.IsActive is not null)
        {
            query = query.Where(q => q.IsActive == request.IsActive);
        }

        var questions = await query
            .OrderBy(q => q.Topic!.DisplayOrder).ThenBy(q => q.Difficulty)
            .Select(q => new QuestionDto(
                q.Id, q.TopicId, q.Topic!.Name, q.Type, q.Difficulty, q.Text, q.ImageUrl,
                q.CorrectAnswerText, q.PointsOverride, q.IsActive,
                q.Options.OrderBy(o => o.DisplayOrder)
                    .Select(o => new QuestionOptionDto(o.Id, o.Text, o.IsCorrect, o.DisplayOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<List<QuestionDto>>.Success(questions);
    }
}
```

- [ ] **Step 15: Run the full Application test suite**

Run: `dotnet test tests/ExamSystem.Application.UnitTests`
Expected: all tests pass, 0 failures.

- [ ] **Step 16: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add Questions CQRS with FillBlank/MCQ validation rules and tests"
```

---

### Task 6: Image Upload + `QuestionsController` + Integration Tests

**Files:**
- Create: `src/ExamSystem.Application/Common/Interfaces/IImageStorageService.cs`
- Create: `src/ExamSystem.Infrastructure/Files/LocalImageStorageService.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Modify: `src/ExamSystem.Api/Program.cs`
- Create: `src/ExamSystem.Api/Controllers/QuestionsController.cs`
- Create: `tests/ExamSystem.Api.IntegrationTests/Controllers/QuestionsControllerTests.cs`

FR-3.3 requires image support inside a question. This plan stores images on local disk under `wwwroot/question-images` and serves them via ASP.NET Core static files -- consistent with the PRD's Free-Tier deployment profile (no external storage dependency). Max upload size is capped at 5 MB.

- [ ] **Step 1: Write `IImageStorageService`**

```csharp
namespace ExamSystem.Application.Common.Interfaces;

public interface IImageStorageService
{
    /// <returns>A relative URL (e.g. "/question-images/&lt;guid&gt;.jpg") to store on the Question.</returns>
    Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Implement `LocalImageStorageService`**

```csharp
using ExamSystem.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace ExamSystem.Infrastructure.Files;

public class LocalImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };

    private readonly IWebHostEnvironment _environment;

    public LocalImageStorageService(IWebHostEnvironment environment) => _environment = environment;

    public async Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException($"Unsupported image content type: {contentType}");
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "question-images");
        Directory.CreateDirectory(uploadsDir);

        var extension = Path.GetExtension(originalFileName) is { Length: > 0 } ext ? ext : ".jpg";
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using (var fileStream = File.Create(fullPath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/question-images/{fileName}";
    }
}
```

- [ ] **Step 3: Register the service in `AddInfrastructure`**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` -- add inside `AddInfrastructure`, after the existing `services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();` line:
```csharp
        services.AddScoped<IImageStorageService, LocalImageStorageService>();
```
Add the using at the top of the file:
```csharp
using ExamSystem.Infrastructure.Files;
```

- [ ] **Step 4: Serve `wwwroot` as static files**

Modify `src/ExamSystem.Api/Program.cs` -- add `app.UseStaticFiles();` right after `app.UseHttpsRedirection();`:
```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAngularApp");
```

- [ ] **Step 5: Write the failing integration test for `QuestionsController`**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/QuestionsControllerTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class QuestionsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public QuestionsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> CreateTopicAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/admin/topics", new { name, displayOrder = 1 });
        var body = await response.Content.ReadFromJsonAsync<CreatedIdDto>();
        return body!.Id;
    }

    [Fact]
    public async Task CreateFillBlankQuestion_WithInvalidAnswerFormat_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Word Skills");

        var response = await client.PostAsJsonAsync("/api/admin/questions", new
        {
            topicId,
            type = "FillBlank",
            difficulty = "Medium",
            text = "Fill ___",
            correctAnswerText = "Data Base"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMcqQuestion_ThenList_ReturnsIt()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Windows Basics");

        var createResponse = await client.PostAsJsonAsync("/api/admin/questions", new
        {
            topicId,
            type = "Mcq",
            difficulty = "Hard",
            text = "What does CPU stand for?",
            options = new[]
            {
                new { text = "Central Processing Unit", isCorrect = true },
                new { text = "Central Printer Unit", isCorrect = false }
            }
        });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/admin/questions?topicId={topicId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var questions = await listResponse.Content.ReadFromJsonAsync<List<QuestionListItemDto>>();
        Assert.Single(questions!, q => q.Text == "What does CPU stand for?");
    }

    private record CreatedIdDto(Guid Id);
    private record QuestionListItemDto(Guid Id, string Text);
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter QuestionsControllerTests`
Expected: FAIL -- 404 Not Found (no `QuestionsController` yet).

- [ ] **Step 7: Implement `QuestionsController`**

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Application.Features.Questions.DeleteQuestion;
using ExamSystem.Application.Features.Questions.GetQuestions;
using ExamSystem.Application.Features.Questions.UpdateQuestion;
using ExamSystem.Domain.Questions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin CRUD for the Question Bank, plus image upload.</summary>
[ApiController]
[Route("api/admin/questions")]
[Authorize(Roles = "Admin")]
public class QuestionsController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private readonly ISender _sender;
    private readonly IImageStorageService _imageStorage;

    public QuestionsController(ISender sender, IImageStorageService imageStorage)
    {
        _sender = sender;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? topicId, [FromQuery] DifficultyLevel? difficulty, [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQuestionsQuery(topicId, difficulty, isActive), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateQuestionCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateQuestionCommand(
            id, request.TopicId, request.Type, request.Difficulty, request.Text, request.ImageUrl,
            request.Options, request.CorrectAnswerText, request.PointsOverride, request.IsActive);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeactivateQuestionCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("image")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }
        if (file.Length > MaxImageSizeBytes)
        {
            return BadRequest(new { errors = new[] { "Image exceeds the 5 MB limit." } });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await _imageStorage.SaveAsync(stream, file.FileName, file.ContentType, cancellationToken);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    public record UpdateQuestionRequest(
        Guid TopicId, QuestionType Type, DifficultyLevel Difficulty, string Text, string? ImageUrl,
        List<QuestionOptionInput>? Options, string? CorrectAnswerText, decimal? PointsOverride, bool IsActive);
}
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter QuestionsControllerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 9: Run the entire backend test suite**

Run: `dotnet test`
Expected: all projects `Passed!`, 0 failures.

- [ ] **Step 10: Commit**

```bash
git add src/ExamSystem.Application src/ExamSystem.Infrastructure src/ExamSystem.Api tests/ExamSystem.Api.IntegrationTests
git commit -m "feat(api): add QuestionsController with image upload and integration tests"
```

---

### Task 7: Application — Bulk Import Command (Parser Interface + Handler) — TDD

**Files:**
- Create: `src/ExamSystem.Application/Common/Interfaces/IExcelQuestionParser.cs`
- Create: `src/ExamSystem.Application/Features/Questions/BulkImportQuestions/BulkImportQuestionsCommand.cs`
- Create: `src/ExamSystem.Application/Features/Questions/BulkImportQuestions/BulkImportReport.cs`
- Create: `src/ExamSystem.Application/Features/Questions/BulkImportQuestions/BulkImportQuestionsCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Questions/BulkImportQuestionsCommandHandlerTests.cs`

**Design decision:** parsing the `.xlsx` file (ClosedXML) is an Infrastructure concern -- `Application` only depends on an `IExcelQuestionParser` interface returning plain DTOs, so this task's handler tests mock the parser and never touch a real spreadsheet. The real ClosedXML implementation is Task 8. Per-row question creation reuses `CreateQuestionCommand` through `ISender`, so Task 5's validation rules (including the FillBlank regex) apply automatically without duplicating logic -- a row that fails validation is recorded in the report and the batch continues.

- [ ] **Step 1: Write the parser DTOs and interface**

```csharp
namespace ExamSystem.Application.Common.Interfaces;

public record ParsedMcqRow(int RowNumber, string Topic, string Difficulty, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, string CorrectOption);

public record ParsedFillBlankRow(int RowNumber, string Topic, string Difficulty, string QuestionText, string CorrectAnswer);

public record ParsedQuestionWorkbook(List<ParsedMcqRow> McqRows, List<ParsedFillBlankRow> FillBlankRows);

public interface IExcelQuestionParser
{
    ParsedQuestionWorkbook Parse(Stream fileContent);
}
```

- [ ] **Step 2: Write `BulkImportReport` and `BulkImportQuestionsCommand`**

`src/ExamSystem.Application/Features/Questions/BulkImportQuestions/BulkImportReport.cs`:
```csharp
namespace ExamSystem.Application.Features.Questions.BulkImportQuestions;

public record BulkImportRowError(string Sheet, int RowNumber, string Message);

public record BulkImportReport(int TotalRows, int SuccessCount, int FailureCount, List<BulkImportRowError> Errors);
```

`src/ExamSystem.Application/Features/Questions/BulkImportQuestions/BulkImportQuestionsCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Questions.BulkImportQuestions;

public record BulkImportQuestionsCommand(Stream FileContent) : IRequest<Result<BulkImportReport>>;
```

- [ ] **Step 3: Write the failing handler test**

Create `tests/ExamSystem.Application.UnitTests/Features/Questions/BulkImportQuestionsCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application;
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Questions.BulkImportQuestions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class BulkImportQuestionsCommandHandlerTests
{
    private static ISender BuildRealSender(ExamSystem.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(db);
        services.AddApplication();
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Handle_MixOfValidAndInvalidRows_ImportsValidOnesAndReportsFailures()
    {
        using var db = TestDbContextFactory.Create();
        var sender = BuildRealSender(db);

        var workbook = new ParsedQuestionWorkbook(
            McqRows: new List<ParsedMcqRow>
            {
                new(2, "Excel", "Medium", "What does CPU stand for?", "Central Processing Unit", "Cool Processor Utility", "Compact Power Unit", "Core Processing Unit", "A")
            },
            FillBlankRows: new List<ParsedFillBlankRow>
            {
                new(2, "Excel", "Medium", "Fill ___ (valid)", "server"),
                new(3, "Excel", "Medium", "Fill ___ (invalid, has a space)", "data base")
            });

        var parser = new Mock<IExcelQuestionParser>();
        parser.Setup(p => p.Parse(It.IsAny<Stream>())).Returns(workbook);

        var handler = new BulkImportQuestionsCommandHandler(db, sender, parser.Object);
        var result = await handler.Handle(new BulkImportQuestionsCommand(Stream.Null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TotalRows);
        Assert.Equal(2, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.FailureCount);
        Assert.Single(result.Value.Errors, e => e.Sheet == "FillBlank" && e.RowNumber == 3);
        Assert.Equal(2, db.Questions.Count());
    }

    [Fact]
    public async Task Handle_UnknownTopic_CreatesItAutomatically()
    {
        using var db = TestDbContextFactory.Create();
        var sender = BuildRealSender(db);

        var workbook = new ParsedQuestionWorkbook(
            McqRows: new List<ParsedMcqRow>(),
            FillBlankRows: new List<ParsedFillBlankRow> { new(2, "Brand New Topic", "Hard", "Fill ___", "answer") });

        var parser = new Mock<IExcelQuestionParser>();
        parser.Setup(p => p.Parse(It.IsAny<Stream>())).Returns(workbook);

        var handler = new BulkImportQuestionsCommandHandler(db, sender, parser.Object);
        var result = await handler.Handle(new BulkImportQuestionsCommand(Stream.Null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.SuccessCount);
        Assert.Contains(db.Topics, t => t.Name == "Brand New Topic");
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter BulkImportQuestionsCommandHandlerTests`
Expected: FAIL to compile -- `BulkImportQuestionsCommandHandler` does not exist yet.

- [ ] **Step 5: Implement `BulkImportQuestionsCommandHandler`**

```csharp
using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.Features.Questions.BulkImportQuestions;

public class BulkImportQuestionsCommandHandler : IRequestHandler<BulkImportQuestionsCommand, Result<BulkImportReport>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly IExcelQuestionParser _parser;

    public BulkImportQuestionsCommandHandler(IApplicationDbContext db, ISender sender, IExcelQuestionParser parser)
    {
        _db = db;
        _sender = sender;
        _parser = parser;
    }

    public async Task<Result<BulkImportReport>> Handle(BulkImportQuestionsCommand request, CancellationToken cancellationToken)
    {
        var workbook = _parser.Parse(request.FileContent);
        var topicCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<BulkImportRowError>();
        var successCount = 0;

        foreach (var row in workbook.McqRows)
        {
            var outcome = await ImportMcqRowAsync(row, topicCache, cancellationToken);
            if (outcome is null) successCount++; else errors.Add(outcome);
        }

        foreach (var row in workbook.FillBlankRows)
        {
            var outcome = await ImportFillBlankRowAsync(row, topicCache, cancellationToken);
            if (outcome is null) successCount++; else errors.Add(outcome);
        }

        var total = workbook.McqRows.Count + workbook.FillBlankRows.Count;
        return Result<BulkImportReport>.Success(new BulkImportReport(total, successCount, total - successCount, errors));
    }

    private async Task<BulkImportRowError?> ImportMcqRowAsync(ParsedMcqRow row, Dictionary<string, Guid> topicCache, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DifficultyLevel>(row.Difficulty, ignoreCase: true, out var difficulty))
        {
            return new BulkImportRowError("MCQ", row.RowNumber, $"Unknown difficulty: {row.Difficulty}");
        }

        var correctIndex = row.CorrectOption.Trim().ToUpperInvariant() switch
        {
            "A" => 0, "B" => 1, "C" => 2, "D" => 3,
            _ => -1
        };
        if (correctIndex < 0)
        {
            return new BulkImportRowError("MCQ", row.RowNumber, $"Unknown correct option: {row.CorrectOption}");
        }

        var topicId = await ResolveOrCreateTopicAsync(row.Topic, topicCache, cancellationToken);
        var optionTexts = new[] { row.OptionA, row.OptionB, row.OptionC, row.OptionD };
        var options = optionTexts
            .Select((text, index) => new QuestionOptionInput(text, index == correctIndex))
            .ToList();

        var command = new CreateQuestionCommand(topicId, QuestionType.Mcq, difficulty, row.QuestionText, null, options, null, null);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? null : new BulkImportRowError("MCQ", row.RowNumber, string.Join("; ", result.Errors));
    }

    private async Task<BulkImportRowError?> ImportFillBlankRowAsync(ParsedFillBlankRow row, Dictionary<string, Guid> topicCache, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DifficultyLevel>(row.Difficulty, ignoreCase: true, out var difficulty))
        {
            return new BulkImportRowError("FillBlank", row.RowNumber, $"Unknown difficulty: {row.Difficulty}");
        }

        var topicId = await ResolveOrCreateTopicAsync(row.Topic, topicCache, cancellationToken);
        var command = new CreateQuestionCommand(topicId, QuestionType.FillBlank, difficulty, row.QuestionText, null, null, row.CorrectAnswer, null);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? null : new BulkImportRowError("FillBlank", row.RowNumber, string.Join("; ", result.Errors));
    }

    private async Task<Guid> ResolveOrCreateTopicAsync(string name, Dictionary<string, Guid> topicCache, CancellationToken cancellationToken)
    {
        if (topicCache.TryGetValue(name, out var cachedId))
        {
            return cachedId;
        }

        var existing = await _db.Topics.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
        if (existing is not null)
        {
            topicCache[name] = existing.Id;
            return existing.Id;
        }

        var maxOrder = await _db.Topics.Select(t => (int?)t.DisplayOrder).MaxAsync(cancellationToken) ?? 0;
        var topic = new Topic { Name = name, DisplayOrder = maxOrder + 1, IsActive = true };
        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(cancellationToken);

        topicCache[name] = topic.Id;
        return topic.Id;
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter BulkImportQuestionsCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 7: Run the full Application test suite**

Run: `dotnet test tests/ExamSystem.Application.UnitTests`
Expected: all tests pass, 0 failures.

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add bulk question import handler with per-row validation report"
```

---

### Task 8: Infrastructure — ClosedXML Parser + Import Endpoint + Real-Data Seeding Test

**Files:**
- Modify: `src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj`
- Create: `src/ExamSystem.Infrastructure/Files/ClosedXmlQuestionParser.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Modify: `src/ExamSystem.Api/Controllers/QuestionsController.cs`
- Modify: `tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj`
- Create: `tests/ExamSystem.Api.IntegrationTests/Controllers/QuestionsImportControllerTests.cs`

This task's integration test imports the real `docs/question-bank/questions_ready_for_import.xlsx` (300 MCQ + 52 FillBlank, already cleaned in a prior session) end-to-end through the HTTP API -- this is the concrete "load the prepared question bank into the database" deliverable.

- [ ] **Step 1: Add the ClosedXML package**

Run:
```bash
dotnet add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj package ClosedXML --version 0.104.1
```
Expected: "Restored ..." with no errors.

- [ ] **Step 2: Implement `ClosedXmlQuestionParser`**

Column layout matches the template already produced in `docs/question-bank/questions_ready_for_import.xlsx`: sheet `MCQ` has columns `Topic, Type, Difficulty, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption`; sheet `FillBlank` has `Topic, Type, Difficulty, QuestionText, CorrectAnswer`. Row 1 is the header in both.

```csharp
using ClosedXML.Excel;
using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Infrastructure.Files;

public class ClosedXmlQuestionParser : IExcelQuestionParser
{
    public ParsedQuestionWorkbook Parse(Stream fileContent)
    {
        using var workbook = new XLWorkbook(fileContent);
        var mcqRows = new List<ParsedMcqRow>();
        var fillBlankRows = new List<ParsedFillBlankRow>();

        if (workbook.Worksheets.TryGetWorksheet("MCQ", out var mcqSheet))
        {
            var usedRange = mcqSheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    mcqRows.Add(new ParsedMcqRow(
                        row.RowNumber(),
                        row.Cell(1).GetString().Trim(),
                        row.Cell(3).GetString().Trim(),
                        row.Cell(4).GetString().Trim(),
                        row.Cell(5).GetString().Trim(),
                        row.Cell(6).GetString().Trim(),
                        row.Cell(7).GetString().Trim(),
                        row.Cell(8).GetString().Trim(),
                        row.Cell(9).GetString().Trim()));
                }
            }
        }

        if (workbook.Worksheets.TryGetWorksheet("FillBlank", out var fillBlankSheet))
        {
            var usedRange = fillBlankSheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    fillBlankRows.Add(new ParsedFillBlankRow(
                        row.RowNumber(),
                        row.Cell(1).GetString().Trim(),
                        row.Cell(3).GetString().Trim(),
                        row.Cell(4).GetString().Trim(),
                        row.Cell(5).GetString().Trim()));
                }
            }
        }

        return new ParsedQuestionWorkbook(mcqRows, fillBlankRows);
    }
}
```

- [ ] **Step 3: Register the parser in `AddInfrastructure`**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` -- add next to the `IImageStorageService` registration from Task 6:
```csharp
        services.AddScoped<IExcelQuestionParser, ClosedXmlQuestionParser>();
```

- [ ] **Step 4: Add the import endpoint to `QuestionsController`**

Modify `src/ExamSystem.Api/Controllers/QuestionsController.cs` -- add this action (and the corresponding `using`):
```csharp
using ExamSystem.Application.Features.Questions.BulkImportQuestions;
```
```csharp
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }

        await using var stream = file.OpenReadStream();
        var result = await _sender.Send(new BulkImportQuestionsCommand(stream), cancellationToken);

        return Ok(result.Value);
    }
```

- [ ] **Step 5: Copy the real prepared workbook into the integration test project's output**

Modify `tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj` -- add this `ItemGroup`:
```xml
  <ItemGroup>
    <None Include="..\..\docs\question-bank\questions_ready_for_import.xlsx" Link="TestData\questions_ready_for_import.xlsx" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 6: Write the failing test that imports the real question bank**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/QuestionsImportControllerTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class QuestionsImportControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public QuestionsImportControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Import_RealPreparedWorkbook_ImportsAllRowsWithNoFailures()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", "questions_ready_for_import.xlsx");
        await using var fileStream = File.OpenRead(filePath);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", "questions_ready_for_import.xlsx");

        var response = await client.PostAsync("/api/admin/questions/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<BulkImportReportDto>();
        Assert.Equal(352, report!.TotalRows);
        Assert.Equal(352, report.SuccessCount);
        Assert.Empty(report.Errors);

        var listResponse = await client.GetAsync("/api/admin/questions");
        var questions = await listResponse.Content.ReadFromJsonAsync<List<QuestionListItemDto>>();
        Assert.Equal(352, questions!.Count);
    }

    private record BulkImportRowErrorDto(string Sheet, int RowNumber, string Message);
    private record BulkImportReportDto(int TotalRows, int SuccessCount, int FailureCount, List<BulkImportRowErrorDto> Errors);
    private record QuestionListItemDto(Guid Id, string Text);
}
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter QuestionsImportControllerTests`
Expected: FAIL -- 404 Not Found (no `import` route yet), or file-not-found if Step 5 wasn't applied first.

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter QuestionsImportControllerTests`
Expected: `Passed! - Failed: 0, Passed: 1`. If it fails with row errors instead, print `report.Errors` -- it means the source workbook or `ClosedXmlQuestionParser`'s column mapping drifted from Task 8 Step 2's assumptions; reconcile them before continuing (do not weaken the FillBlank regex to make it pass).

- [ ] **Step 9: Run the entire backend test suite**

Run: `dotnet test`
Expected: all projects `Passed!`, 0 failures.

- [ ] **Step 10: Apply the migration and import against the real LocalDB (manual, not just SQLite in tests)**

Run: `dotnet run --project src/ExamSystem.Api` (leave running), then in a second terminal:
```bash
curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d "{\"userName\":\"admin\",\"password\":\"ChangeMe!2026\"}"
```
Copy the returned `token`, then:
```bash
curl -X POST http://localhost:5000/api/admin/questions/import -H "Authorization: Bearer <token>" -F "file=@docs/question-bank/questions_ready_for_import.xlsx"
```
Expected: JSON report with `"totalRows":352,"successCount":352,"failureCount":0,"errors":[]`. Stop the API (Ctrl+C) once confirmed.

- [ ] **Step 11: Commit**

```bash
git add src/ExamSystem.Infrastructure src/ExamSystem.Api tests/ExamSystem.Api.IntegrationTests
git commit -m "feat(infrastructure): add ClosedXML bulk-import parser and wire the import endpoint"
```

---

### Task 9: Frontend — `TopicService` + Topics Admin Page (TDD)

**Files:**
- Create: `frontend/src/app/core/services/topic.service.ts`
- Test: `frontend/src/app/core/services/topic.service.spec.ts`
- Create: `frontend/src/app/features/admin/topics/topics-list.component.ts`
- Create: `frontend/src/app/features/admin/topics/topics-list.component.html`
- Test: `frontend/src/app/features/admin/topics/topics-list.component.spec.ts`
- Modify: `frontend/src/app/layouts/admin-layout/admin-layout.component.ts`
- Modify: `frontend/src/app/app.routes.ts`

- [ ] **Step 1: Write the failing test for `TopicService`**

Create `frontend/src/app/core/services/topic.service.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TopicService, Topic } from './topic.service';
import { environment } from '../../../environments/environment';

describe('TopicService', () => {
  let service: TopicService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [TopicService]
    });
    service = TestBed.inject(TopicService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() fetches the topic list', () => {
    const mockTopics: Topic[] = [{ id: '1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 5 }];

    service.getAll().subscribe(topics => expect(topics).toEqual(mockTopics));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics`);
    expect(req.request.method).toBe('GET');
    req.flush(mockTopics);
  });

  it('create() posts a new topic', () => {
    service.create({ name: 'Word', displayOrder: 2 }).subscribe(res => expect(res.id).toBe('2'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: '2' });
  });

  it('update() puts to the topic id', () => {
    service.update('1', { name: 'Excel', displayOrder: 1, isActive: false }).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics/1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('delete() deletes the topic id', () => {
    service.delete('1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/topics/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/topic.service.spec.ts'`
Expected: FAIL -- `topic.service.ts` does not exist yet.

- [ ] **Step 3: Implement `TopicService`**

Create `frontend/src/app/core/services/topic.service.ts`:
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Topic {
  id: string;
  name: string;
  displayOrder: number;
  isActive: boolean;
  questionCount: number;
}

export interface TopicInput {
  name: string;
  displayOrder: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class TopicService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/topics`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<Topic[]> {
    return this.http.get<Topic[]>(this.baseUrl);
  }

  create(input: TopicInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, input);
  }

  update(id: string, input: TopicInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/topic.service.spec.ts'`
Expected: `TOTAL: 4 SUCCESS`

- [ ] **Step 5: Write the failing test for `TopicsListComponent`**

Create `frontend/src/app/features/admin/topics/topics-list.component.spec.ts`:
```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TopicsListComponent } from './topics-list.component';
import { TopicService, Topic } from '../../../core/services/topic.service';

describe('TopicsListComponent', () => {
  let fixture: ComponentFixture<TopicsListComponent>;
  let component: TopicsListComponent;
  let topicService: jasmine.SpyObj<TopicService>;

  const sampleTopics: Topic[] = [
    { id: '1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 10 }
  ];

  beforeEach(async () => {
    topicService = jasmine.createSpyObj('TopicService', ['getAll', 'create', 'update', 'delete']);
    topicService.getAll.and.returnValue(of(sampleTopics));

    await TestBed.configureTestingModule({
      imports: [TopicsListComponent],
      providers: [{ provide: TopicService, useValue: topicService }]
    }).compileComponents();

    fixture = TestBed.createComponent(TopicsListComponent);
    component = fixture.componentInstance;
  });

  it('loads topics on init', () => {
    fixture.detectChanges();
    expect(component.topics()).toEqual(sampleTopics);
  });

  it('createTopic() calls the service and reloads the list', () => {
    topicService.create.and.returnValue(of({ id: '2' }));
    fixture.detectChanges();

    component.newTopicName = 'Word';
    component.newTopicDisplayOrder = 2;
    component.createTopic();

    expect(topicService.create).toHaveBeenCalledWith({ name: 'Word', displayOrder: 2 });
    expect(topicService.getAll).toHaveBeenCalledTimes(2);
  });

  it('deleteTopic() calls the service and reloads the list', () => {
    topicService.delete.and.returnValue(of(undefined));
    fixture.detectChanges();

    component.deleteTopic('1');

    expect(topicService.delete).toHaveBeenCalledWith('1');
    expect(topicService.getAll).toHaveBeenCalledTimes(2);
  });
});
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/topics-list.component.spec.ts'`
Expected: FAIL -- `topics-list.component.ts` does not exist yet.

- [ ] **Step 7: Implement `TopicsListComponent`**

Create `frontend/src/app/features/admin/topics/topics-list.component.ts`:
```typescript
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Topic, TopicService } from '../../../core/services/topic.service';

@Component({
  selector: 'app-topics-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './topics-list.component.html'
})
export class TopicsListComponent implements OnInit {
  topics = signal<Topic[]>([]);
  newTopicName = '';
  newTopicDisplayOrder = 1;
  errorMessage: string | null = null;

  constructor(private readonly topicService: TopicService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.topicService.getAll().subscribe(topics => this.topics.set(topics));
  }

  createTopic(): void {
    if (!this.newTopicName.trim()) {
      return;
    }
    this.errorMessage = null;
    this.topicService.create({ name: this.newTopicName, displayOrder: this.newTopicDisplayOrder }).subscribe({
      next: () => {
        this.newTopicName = '';
        this.newTopicDisplayOrder = 1;
        this.load();
      },
      error: () => (this.errorMessage = 'تعذّر إنشاء الموضوع.')
    });
  }

  deleteTopic(id: string): void {
    this.errorMessage = null;
    this.topicService.delete(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'لا يمكن حذف موضوع يحتوي على أسئلة — عطّله بدلاً من ذلك.')
    });
  }
}
```

- [ ] **Step 8: Write the template**

Create `frontend/src/app/features/admin/topics/topics-list.component.html`:
```html
<div class="topics-page">
  <h1>الموضوعات</h1>

  <form class="new-topic-form" (ngSubmit)="createTopic()">
    <input type="text" placeholder="اسم الموضوع" [(ngModel)]="newTopicName" name="newTopicName" />
    <input type="number" placeholder="ترتيب العرض" [(ngModel)]="newTopicDisplayOrder" name="newTopicDisplayOrder" />
    <button type="submit">إضافة موضوع</button>
  </form>

  <p class="error" *ngIf="errorMessage">{{ errorMessage }}</p>

  <table>
    <thead>
      <tr>
        <th>الاسم</th>
        <th>الترتيب</th>
        <th>عدد الأسئلة</th>
        <th>الحالة</th>
        <th></th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let topic of topics()">
        <td>{{ topic.name }}</td>
        <td>{{ topic.displayOrder }}</td>
        <td>{{ topic.questionCount }}</td>
        <td>{{ topic.isActive ? 'نشط' : 'معطّل' }}</td>
        <td><button (click)="deleteTopic(topic.id)">حذف</button></td>
      </tr>
    </tbody>
  </table>
</div>
```

- [ ] **Step 9: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/topics-list.component.spec.ts'`
Expected: `TOTAL: 3 SUCCESS`

- [ ] **Step 10: Add the admin nav links and the `/admin/topics` route**

Modify `frontend/src/app/layouts/admin-layout/admin-layout.component.ts`:
```typescript
import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  template: `
    <div class="admin-shell">
      <header class="admin-header">نظام الامتحانات — لوحة التحكم</header>
      <nav class="admin-nav">
        <a routerLink="/admin/dashboard">الرئيسية</a>
        <a routerLink="/admin/topics">الموضوعات</a>
        <a routerLink="/admin/questions">بنك الأسئلة</a>
        <a routerLink="/admin/questions/import">استيراد بالجملة</a>
      </nav>
      <main class="admin-content">
        <router-outlet />
      </main>
    </div>
  `
})
export class AdminLayoutComponent {}
```

Modify `frontend/src/app/app.routes.ts` -- add a `topics` child route inside the `admin` route's `children` array (after the existing `dashboard` entry):
```typescript
      {
        path: 'topics',
        loadComponent: () =>
          import('./features/admin/topics/topics-list.component').then(m => m.TopicsListComponent)
      },
```

- [ ] **Step 11: Run the full frontend test suite**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: all suites `SUCCESS`, 0 failures.

- [ ] **Step 12: Commit**

```bash
git add frontend/src/app
git commit -m "feat(frontend): add Topics admin page with create/delete"
```

---

### Task 10: Frontend — `QuestionService` + `QuestionFormComponent` (TDD)

**Files:**
- Create: `frontend/src/app/core/services/question.service.ts`
- Test: `frontend/src/app/core/services/question.service.spec.ts`
- Create: `frontend/src/app/features/admin/questions/question-form.component.ts`
- Create: `frontend/src/app/features/admin/questions/question-form.component.html`
- Test: `frontend/src/app/features/admin/questions/question-form.component.spec.ts`

- [ ] **Step 1: Write the failing test for `QuestionService`**

Create `frontend/src/app/core/services/question.service.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { QuestionService, Question } from './question.service';
import { environment } from '../../../environments/environment';

describe('QuestionService', () => {
  let service: QuestionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [QuestionService]
    });
    service = TestBed.inject(QuestionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() builds query params from filters', () => {
    const mock: Question[] = [];
    service.getAll({ topicId: 't1', difficulty: 'Hard' }).subscribe(res => expect(res).toEqual(mock));

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/admin/questions`
      && r.params.get('topicId') === 't1' && r.params.get('difficulty') === 'Hard');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });

  it('create() posts a question payload', () => {
    service.create({ topicId: 't1', type: 'FillBlank', difficulty: 'Medium', text: 'Fill ___', correctAnswerText: 'server' })
      .subscribe(res => expect(res.id).toBe('q1'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/questions`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'q1' });
  });

  it('deactivate() deletes the question id', () => {
    service.deactivate('q1').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/questions/q1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('uploadImage() posts multipart form data', () => {
    const file = new File(['x'], 'pic.png', { type: 'image/png' });
    service.uploadImage(file).subscribe(res => expect(res.url).toBe('/question-images/abc.png'));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/admin/questions/image`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBeTrue();
    req.flush({ url: '/question-images/abc.png' });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/question.service.spec.ts'`
Expected: FAIL -- `question.service.ts` does not exist yet.

- [ ] **Step 3: Implement `QuestionService`**

Create `frontend/src/app/core/services/question.service.ts`:
```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type QuestionType = 'Mcq' | 'TrueFalse' | 'FillBlank';
export type Difficulty = 'Easy' | 'Medium' | 'Hard';

export interface QuestionOption {
  id?: string;
  text: string;
  isCorrect: boolean;
  displayOrder?: number;
}

export interface Question {
  id: string;
  topicId: string;
  topicName: string;
  type: QuestionType;
  difficulty: Difficulty;
  text: string;
  imageUrl: string | null;
  correctAnswerText: string | null;
  pointsOverride: number | null;
  isActive: boolean;
  options: QuestionOption[];
}

export interface QuestionInput {
  topicId: string;
  type: QuestionType;
  difficulty: Difficulty;
  text: string;
  imageUrl?: string | null;
  options?: { text: string; isCorrect: boolean }[];
  correctAnswerText?: string | null;
  pointsOverride?: number | null;
}

export interface QuestionFilters {
  topicId?: string;
  difficulty?: Difficulty;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class QuestionService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/questions`;

  constructor(private readonly http: HttpClient) {}

  getAll(filters: QuestionFilters = {}): Observable<Question[]> {
    let params = new HttpParams();
    if (filters.topicId) params = params.set('topicId', filters.topicId);
    if (filters.difficulty) params = params.set('difficulty', filters.difficulty);
    if (filters.isActive !== undefined) params = params.set('isActive', String(filters.isActive));

    return this.http.get<Question[]>(this.baseUrl, { params });
  }

  create(input: QuestionInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, input);
  }

  update(id: string, input: QuestionInput & { isActive: boolean }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  deactivate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  uploadImage(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/image`, formData);
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/question.service.spec.ts'`
Expected: `TOTAL: 4 SUCCESS`

- [ ] **Step 5: Write the failing test for `QuestionFormComponent`**

Create `frontend/src/app/features/admin/questions/question-form.component.spec.ts`:
```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { QuestionFormComponent } from './question-form.component';
import { Topic } from '../../../core/services/topic.service';

describe('QuestionFormComponent', () => {
  let fixture: ComponentFixture<QuestionFormComponent>;
  let component: QuestionFormComponent;

  const topics: Topic[] = [{ id: 't1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 0 }];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [QuestionFormComponent] }).compileComponents();
    fixture = TestBed.createComponent(QuestionFormComponent);
    component = fixture.componentInstance;
    component.topics = topics;
    fixture.detectChanges();
  });

  it('normalizes FillBlank answers to a lowercase single word as the admin types', () => {
    component.form.patchValue({ type: 'FillBlank' });
    component.onCorrectAnswerInput('Data Base');

    expect(component.form.value.correctAnswerText).toBe('database');
  });

  it('emits a Mcq payload with the options array on save', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: 't1', type: 'Mcq', difficulty: 'Medium', text: 'Pick one' });
    component.options.at(0).patchValue({ text: 'A', isCorrect: true });
    component.options.at(1).patchValue({ text: 'B', isCorrect: false });
    component.submit();

    expect(emitted.type).toBe('Mcq');
    expect(emitted.options.length).toBe(2);
    expect(emitted.options[0]).toEqual({ text: 'A', isCorrect: true });
  });

  it('does not emit when the form is invalid', () => {
    let emitted: any = null;
    component.save.subscribe(value => (emitted = value));

    component.form.patchValue({ topicId: '', type: 'FillBlank', text: '' });
    component.submit();

    expect(emitted).toBeNull();
  });
});
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/question-form.component.spec.ts'`
Expected: FAIL -- `question-form.component.ts` does not exist yet.

- [ ] **Step 7: Implement `QuestionFormComponent`**

Create `frontend/src/app/features/admin/questions/question-form.component.ts`:
```typescript
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Topic } from '../../../core/services/topic.service';
import { Difficulty, QuestionInput, QuestionType } from '../../../core/services/question.service';

@Component({
  selector: 'app-question-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './question-form.component.html'
})
export class QuestionFormComponent {
  @Input() topics: Topic[] = [];
  @Output() save = new EventEmitter<QuestionInput>();
  @Output() imageFileSelected = new EventEmitter<File>();

  readonly form: FormGroup = this.fb.group({
    topicId: ['', Validators.required],
    type: ['Mcq' as QuestionType, Validators.required],
    difficulty: ['Medium' as Difficulty, Validators.required],
    text: ['', Validators.required],
    imageUrl: [''],
    correctAnswerText: ['']
  });

  constructor(private readonly fb: FormBuilder) {
    this.form.addControl('options', this.fb.array([this.buildOption(), this.buildOption()]));
  }

  get options(): FormArray {
    return this.form.get('options') as FormArray;
  }

  private buildOption() {
    return this.fb.group({ text: [''], isCorrect: [false] });
  }

  onCorrectAnswerInput(rawValue: string): void {
    const normalized = rawValue.replace(/\s+/g, '').toLowerCase();
    this.form.patchValue({ correctAnswerText: normalized });
  }

  onImageFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.imageFileSelected.emit(input.files[0]);
    }
  }

  setImageUrl(url: string): void {
    this.form.patchValue({ imageUrl: url });
  }

  submit(): void {
    const value = this.form.value;
    const type: QuestionType = value.type;

    if (!value.topicId || !value.text) {
      return;
    }

    if (type === 'FillBlank') {
      if (!value.correctAnswerText || !/^[a-z0-9]+$/.test(value.correctAnswerText)) {
        return;
      }
      this.save.emit({
        topicId: value.topicId,
        type,
        difficulty: value.difficulty,
        text: value.text,
        imageUrl: value.imageUrl || null,
        correctAnswerText: value.correctAnswerText
      });
      return;
    }

    const options = this.options.controls.map(c => c.value as { text: string; isCorrect: boolean });
    const filledOptions = options.filter(o => o.text?.trim());
    const correctCount = filledOptions.filter(o => o.isCorrect).length;

    if (filledOptions.length < 2 || correctCount !== 1) {
      return;
    }

    this.save.emit({
      topicId: value.topicId,
      type,
      difficulty: value.difficulty,
      text: value.text,
      imageUrl: value.imageUrl || null,
      options: filledOptions
    });
  }
}
```

- [ ] **Step 8: Write the template**

Create `frontend/src/app/features/admin/questions/question-form.component.html`:
```html
<form [formGroup]="form" class="question-form" (ngSubmit)="submit()">
  <label for="topicId">الموضوع</label>
  <select id="topicId" formControlName="topicId">
    <option value="" disabled>اختر موضوعًا</option>
    <option *ngFor="let topic of topics" [value]="topic.id">{{ topic.name }}</option>
  </select>

  <label for="type">نوع السؤال</label>
  <select id="type" formControlName="type">
    <option value="Mcq">اختيار من متعدد</option>
    <option value="FillBlank">أكمل الناقص</option>
  </select>

  <label for="difficulty">مستوى الصعوبة</label>
  <select id="difficulty" formControlName="difficulty">
    <option value="Easy">سهل</option>
    <option value="Medium">متوسط</option>
    <option value="Hard">متقدم</option>
  </select>

  <label for="text">نص السؤال</label>
  <textarea id="text" formControlName="text"></textarea>

  <label>صورة السؤال (اختياري)</label>
  <input type="file" accept="image/*" (change)="onImageFileChange($event)" />
  <img *ngIf="form.value.imageUrl" [src]="form.value.imageUrl" alt="" class="question-image-preview" />

  <ng-container *ngIf="form.value.type === 'FillBlank'">
    <label for="correctAnswerText">الإجابة النموذجية (كلمة واحدة، حروف صغيرة)</label>
    <input
      id="correctAnswerText"
      type="text"
      [value]="form.value.correctAnswerText"
      (input)="onCorrectAnswerInput($any($event.target).value)"
    />
  </ng-container>

  <ng-container *ngIf="form.value.type !== 'FillBlank'" formArrayName="options">
    <div *ngFor="let option of options.controls; let i = index" [formGroupName]="i" class="option-row">
      <input type="text" formControlName="text" placeholder="نص الاختيار {{ i + 1 }}" />
      <label>
        <input type="radio" [name]="'correctOption'" (change)="options.controls.forEach((c, j) => c.patchValue({ isCorrect: j === i }))" />
        صحيحة
      </label>
    </div>
  </ng-container>

  <button type="submit">حفظ السؤال</button>
</form>
```

*(Note: the radio group above uses a shared `name` attribute so only one option can be checked at a time in the browser; the `(change)` handler keeps the underlying FormArray's `isCorrect` flags in sync since native radio buttons don't automatically clear sibling FormGroup controls.)*

- [ ] **Step 9: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/question-form.component.spec.ts'`
Expected: `TOTAL: 3 SUCCESS`

- [ ] **Step 10: Commit**

```bash
git add frontend/src/app/core/services/question.service.ts frontend/src/app/core/services/question.service.spec.ts frontend/src/app/features/admin/questions/question-form.component.ts frontend/src/app/features/admin/questions/question-form.component.html frontend/src/app/features/admin/questions/question-form.component.spec.ts
git commit -m "feat(frontend): add QuestionService and a reusable QuestionFormComponent"
```

---

### Task 11: Frontend — `QuestionsListComponent` (Filters + Create/Deactivate, Wires the Form) — TDD

**Files:**
- Create: `frontend/src/app/features/admin/questions/questions-list.component.ts`
- Create: `frontend/src/app/features/admin/questions/questions-list.component.html`
- Test: `frontend/src/app/features/admin/questions/questions-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`

- [ ] **Step 1: Write the failing test for `QuestionsListComponent`**

Create `frontend/src/app/features/admin/questions/questions-list.component.spec.ts`:
```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { QuestionsListComponent } from './questions-list.component';
import { QuestionService, Question } from '../../../core/services/question.service';
import { TopicService, Topic } from '../../../core/services/topic.service';

describe('QuestionsListComponent', () => {
  let fixture: ComponentFixture<QuestionsListComponent>;
  let component: QuestionsListComponent;
  let questionService: jasmine.SpyObj<QuestionService>;
  let topicService: jasmine.SpyObj<TopicService>;

  const topics: Topic[] = [{ id: 't1', name: 'Excel', displayOrder: 1, isActive: true, questionCount: 1 }];
  const questions: Question[] = [{
    id: 'q1', topicId: 't1', topicName: 'Excel', type: 'FillBlank', difficulty: 'Medium',
    text: 'Fill ___', imageUrl: null, correctAnswerText: 'server', pointsOverride: null, isActive: true, options: []
  }];

  beforeEach(async () => {
    questionService = jasmine.createSpyObj('QuestionService', ['getAll', 'create', 'deactivate', 'uploadImage']);
    topicService = jasmine.createSpyObj('TopicService', ['getAll']);
    questionService.getAll.and.returnValue(of(questions));
    topicService.getAll.and.returnValue(of(topics));

    await TestBed.configureTestingModule({
      imports: [QuestionsListComponent],
      providers: [
        { provide: QuestionService, useValue: questionService },
        { provide: TopicService, useValue: topicService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(QuestionsListComponent);
    component = fixture.componentInstance;
  });

  it('loads topics and questions on init', () => {
    fixture.detectChanges();

    expect(component.topics()).toEqual(topics);
    expect(component.questions()).toEqual(questions);
  });

  it('refetches questions when the topic filter changes', () => {
    fixture.detectChanges();
    questionService.getAll.calls.reset();

    component.selectedTopicId = 't1';
    component.applyFilters();

    expect(questionService.getAll).toHaveBeenCalledWith({ topicId: 't1', difficulty: undefined });
  });

  it('onQuestionSave() creates the question and reloads the list', () => {
    fixture.detectChanges();
    questionService.create.and.returnValue(of({ id: 'q2' }));
    questionService.getAll.calls.reset();

    component.onQuestionSave({ topicId: 't1', type: 'FillBlank', difficulty: 'Medium', text: 'Fill ___', correctAnswerText: 'server' });

    expect(questionService.create).toHaveBeenCalled();
    expect(questionService.getAll).toHaveBeenCalled();
  });

  it('deactivateQuestion() calls the service and reloads the list', () => {
    fixture.detectChanges();
    questionService.deactivate.and.returnValue(of(undefined));
    questionService.getAll.calls.reset();

    component.deactivateQuestion('q1');

    expect(questionService.deactivate).toHaveBeenCalledWith('q1');
    expect(questionService.getAll).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/questions-list.component.spec.ts'`
Expected: FAIL -- `questions-list.component.ts` does not exist yet.

- [ ] **Step 3: Implement `QuestionsListComponent`**

Create `frontend/src/app/features/admin/questions/questions-list.component.ts`:
```typescript
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Difficulty, Question, QuestionInput, QuestionService } from '../../../core/services/question.service';
import { Topic, TopicService } from '../../../core/services/topic.service';
import { QuestionFormComponent } from './question-form.component';

@Component({
  selector: 'app-questions-list',
  standalone: true,
  imports: [CommonModule, FormsModule, QuestionFormComponent],
  templateUrl: './questions-list.component.html'
})
export class QuestionsListComponent implements OnInit {
  topics = signal<Topic[]>([]);
  questions = signal<Question[]>([]);
  selectedTopicId = '';
  selectedDifficulty: Difficulty | '' = '';
  errorMessage: string | null = null;

  constructor(
    private readonly questionService: QuestionService,
    private readonly topicService: TopicService
  ) {}

  ngOnInit(): void {
    this.topicService.getAll().subscribe(topics => this.topics.set(topics));
    this.applyFilters();
  }

  applyFilters(): void {
    this.questionService
      .getAll({
        topicId: this.selectedTopicId || undefined,
        difficulty: this.selectedDifficulty || undefined
      })
      .subscribe(questions => this.questions.set(questions));
  }

  onQuestionSave(input: QuestionInput): void {
    this.errorMessage = null;
    this.questionService.create(input).subscribe({
      next: () => this.applyFilters(),
      error: () => (this.errorMessage = 'تعذّر حفظ السؤال — تحقق من صيغة الإجابة أو الاختيارات.')
    });
  }

  deactivateQuestion(id: string): void {
    this.errorMessage = null;
    this.questionService.deactivate(id).subscribe({
      next: () => this.applyFilters(),
      error: () => (this.errorMessage = 'تعذّر تعطيل السؤال.')
    });
  }
}
```

- [ ] **Step 4: Write the template**

Create `frontend/src/app/features/admin/questions/questions-list.component.html`:
```html
<div class="questions-page">
  <h1>بنك الأسئلة</h1>

  <div class="filters">
    <select [(ngModel)]="selectedTopicId" (ngModelChange)="applyFilters()">
      <option value="">كل الموضوعات</option>
      <option *ngFor="let topic of topics()" [value]="topic.id">{{ topic.name }}</option>
    </select>

    <select [(ngModel)]="selectedDifficulty" (ngModelChange)="applyFilters()">
      <option value="">كل المستويات</option>
      <option value="Easy">سهل</option>
      <option value="Medium">متوسط</option>
      <option value="Hard">متقدم</option>
    </select>
  </div>

  <p class="error" *ngIf="errorMessage">{{ errorMessage }}</p>

  <table>
    <thead>
      <tr>
        <th>الموضوع</th>
        <th>النوع</th>
        <th>الصعوبة</th>
        <th>نص السؤال</th>
        <th>الحالة</th>
        <th></th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let question of questions()">
        <td>{{ question.topicName }}</td>
        <td>{{ question.type }}</td>
        <td>{{ question.difficulty }}</td>
        <td>{{ question.text }}</td>
        <td>{{ question.isActive ? 'نشط' : 'معطّل' }}</td>
        <td><button (click)="deactivateQuestion(question.id)">تعطيل</button></td>
      </tr>
    </tbody>
  </table>

  <h2>إضافة سؤال جديد</h2>
  <app-question-form [topics]="topics()" (save)="onQuestionSave($event)"></app-question-form>
</div>
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/questions-list.component.spec.ts'`
Expected: `TOTAL: 4 SUCCESS`

- [ ] **Step 6: Add the `/admin/questions` route**

Modify `frontend/src/app/app.routes.ts` -- add after the `topics` route added in Task 9 Step 10:
```typescript
      {
        path: 'questions',
        loadComponent: () =>
          import('./features/admin/questions/questions-list.component').then(m => m.QuestionsListComponent)
      },
```

- [ ] **Step 7: Run the full frontend test suite**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: all suites `SUCCESS`, 0 failures.

- [ ] **Step 8: Verify the production build still compiles**

Run: `cd frontend && npm run build -- --configuration production && cd ..`
Expected: `Application bundle generation complete.`

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app
git commit -m "feat(frontend): add Questions admin page with topic/difficulty filters"
```

---

### Task 12: Bank Coverage Summary (FR-3.7) + Frontend Bulk Import Page — TDD

**Files:**
- Create: `src/ExamSystem.Application/Features/Questions/GetQuestionBankSummary/QuestionBankSummaryRow.cs`
- Create: `src/ExamSystem.Application/Features/Questions/GetQuestionBankSummary/GetQuestionBankSummaryQuery.cs`
- Create: `src/ExamSystem.Application/Features/Questions/GetQuestionBankSummary/GetQuestionBankSummaryQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Questions/GetQuestionBankSummaryQueryHandlerTests.cs`
- Modify: `src/ExamSystem.Api/Controllers/QuestionsController.cs`
- Modify: `frontend/src/app/core/services/question.service.ts`
- Create: `frontend/src/app/features/admin/questions/bulk-import.component.ts`
- Create: `frontend/src/app/features/admin/questions/bulk-import.component.html`
- Test: `frontend/src/app/features/admin/questions/bulk-import.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`

**Scope note on FR-3.7:** the PRD's live counter compares "available in the bank" against "required by the exam config." The *required* side depends on Exam Configuration (Phase 1b, not built yet). This task ships the *available* half only -- a Topic x Difficulty x Type coverage table -- surfaced on the Bulk Import page where an admin naturally checks bank health after importing. Phase 1b will extend this component with the "required" column once `ExamTopicConfigs` exists.

- [ ] **Step 1: Write the failing test for `GetQuestionBankSummaryQueryHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Questions/GetQuestionBankSummaryQueryHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Questions.GetQuestionBankSummary;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class GetQuestionBankSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_GroupsActiveQuestionsByTopicAndDifficulty()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        db.Questions.AddRange(
            new Question { TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, Text = "q1", IsActive = true },
            new Question { TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, Text = "q2", IsActive = true },
            new Question { TopicId = topic.Id, Type = QuestionType.FillBlank, Difficulty = DifficultyLevel.Medium, Text = "q3", CorrectAnswerText = "a", IsActive = true },
            new Question { TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Hard, Text = "q4", IsActive = false });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetQuestionBankSummaryQueryHandler(db);
        var result = await handler.Handle(new GetQuestionBankSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value!, r => r.TopicName == "Excel" && r.Difficulty == DifficultyLevel.Medium);
        Assert.Equal(2, row.McqCount);
        Assert.Equal(1, row.FillBlankCount);
        Assert.DoesNotContain(result.Value!, r => r.Difficulty == DifficultyLevel.Hard);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetQuestionBankSummaryQueryHandlerTests`
Expected: FAIL to compile -- the query/handler do not exist yet.

- [ ] **Step 3: Implement the query, DTO, and handler**

`src/ExamSystem.Application/Features/Questions/GetQuestionBankSummary/QuestionBankSummaryRow.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestionBankSummary;

public record QuestionBankSummaryRow(string TopicName, DifficultyLevel Difficulty, int McqCount, int FillBlankCount);
```

`src/ExamSystem.Application/Features/Questions/GetQuestionBankSummary/GetQuestionBankSummaryQuery.cs`:
```csharp
namespace ExamSystem.Application.Features.Questions.GetQuestionBankSummary;

public record GetQuestionBankSummaryQuery : IRequest<Result<List<QuestionBankSummaryRow>>>;
```

`src/ExamSystem.Application/Features/Questions/GetQuestionBankSummary/GetQuestionBankSummaryQueryHandler.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestionBankSummary;

public class GetQuestionBankSummaryQueryHandler : IRequestHandler<GetQuestionBankSummaryQuery, Result<List<QuestionBankSummaryRow>>>
{
    private readonly IApplicationDbContext _db;

    public GetQuestionBankSummaryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<QuestionBankSummaryRow>>> Handle(GetQuestionBankSummaryQuery request, CancellationToken cancellationToken)
    {
        var rows = await _db.Questions
            .Where(q => q.IsActive)
            .Include(q => q.Topic)
            .GroupBy(q => new { q.Topic!.Name, q.Difficulty })
            .Select(g => new QuestionBankSummaryRow(
                g.Key.Name,
                g.Key.Difficulty,
                g.Count(q => q.Type == QuestionType.Mcq),
                g.Count(q => q.Type == QuestionType.FillBlank)))
            .OrderBy(r => r.TopicName).ThenBy(r => r.Difficulty)
            .ToListAsync(cancellationToken);

        return Result<List<QuestionBankSummaryRow>>.Success(rows);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetQuestionBankSummaryQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Add the summary endpoint to `QuestionsController`**

Modify `src/ExamSystem.Api/Controllers/QuestionsController.cs` -- add the using and action:
```csharp
using ExamSystem.Application.Features.Questions.GetQuestionBankSummary;
```
```csharp
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQuestionBankSummaryQuery(), cancellationToken);
        return Ok(result.Value);
    }
```

- [ ] **Step 6: Run the backend test suite**

Run: `dotnet test`
Expected: all projects `Passed!`, 0 failures.

- [ ] **Step 7: Commit the backend half**

```bash
git add src/ExamSystem.Application src/ExamSystem.Api tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add question bank Topic x Difficulty coverage summary"
```

- [ ] **Step 8: Add `bulkImport()` and `getSummary()` to `QuestionService`**

Modify `frontend/src/app/core/services/question.service.ts` -- add these members inside the `QuestionService` class:
```typescript
  bulkImport(file: File): Observable<BulkImportReport> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<BulkImportReport>(`${this.baseUrl}/import`, formData);
  }

  getSummary(): Observable<QuestionBankSummaryRow[]> {
    return this.http.get<QuestionBankSummaryRow[]>(`${this.baseUrl}/summary`);
  }
```

Add these interfaces near the top of the same file, alongside the existing `Question`/`QuestionInput` interfaces:
```typescript
export interface BulkImportRowError {
  sheet: string;
  rowNumber: number;
  message: string;
}

export interface BulkImportReport {
  totalRows: number;
  successCount: number;
  failureCount: number;
  errors: BulkImportRowError[];
}

export interface QuestionBankSummaryRow {
  topicName: string;
  difficulty: Difficulty;
  mcqCount: number;
  fillBlankCount: number;
}
```

- [ ] **Step 9: Write the failing test for `BulkImportComponent`**

Create `frontend/src/app/features/admin/questions/bulk-import.component.spec.ts`:
```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { BulkImportComponent } from './bulk-import.component';
import { QuestionService, BulkImportReport, QuestionBankSummaryRow } from '../../../core/services/question.service';

describe('BulkImportComponent', () => {
  let fixture: ComponentFixture<BulkImportComponent>;
  let component: BulkImportComponent;
  let questionService: jasmine.SpyObj<QuestionService>;

  const summary: QuestionBankSummaryRow[] = [{ topicName: 'Excel', difficulty: 'Medium', mcqCount: 50, fillBlankCount: 9 }];
  const report: BulkImportReport = { totalRows: 2, successCount: 1, failureCount: 1, errors: [{ sheet: 'FillBlank', rowNumber: 3, message: 'bad format' }] };

  beforeEach(async () => {
    questionService = jasmine.createSpyObj('QuestionService', ['bulkImport', 'getSummary']);
    questionService.getSummary.and.returnValue(of(summary));

    await TestBed.configureTestingModule({
      imports: [BulkImportComponent],
      providers: [{ provide: QuestionService, useValue: questionService }]
    }).compileComponents();

    fixture = TestBed.createComponent(BulkImportComponent);
    component = fixture.componentInstance;
  });

  it('loads the coverage summary on init', () => {
    fixture.detectChanges();
    expect(component.summary()).toEqual(summary);
  });

  it('uploads the selected file and shows the report, then refreshes the summary', () => {
    questionService.bulkImport.and.returnValue(of(report));
    fixture.detectChanges();
    questionService.getSummary.calls.reset();

    const file = new File(['x'], 'questions.xlsx');
    component.selectedFile = file;
    component.upload();

    expect(questionService.bulkImport).toHaveBeenCalledWith(file);
    expect(component.report()).toEqual(report);
    expect(questionService.getSummary).toHaveBeenCalled();
  });
});
```

- [ ] **Step 10: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/bulk-import.component.spec.ts'`
Expected: FAIL -- `bulk-import.component.ts` does not exist yet.

- [ ] **Step 11: Implement `BulkImportComponent`**

Create `frontend/src/app/features/admin/questions/bulk-import.component.ts`:
```typescript
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BulkImportReport, QuestionBankSummaryRow, QuestionService } from '../../../core/services/question.service';

@Component({
  selector: 'app-bulk-import',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './bulk-import.component.html'
})
export class BulkImportComponent implements OnInit {
  summary = signal<QuestionBankSummaryRow[]>([]);
  report = signal<BulkImportReport | null>(null);
  selectedFile: File | null = null;
  uploading = false;

  constructor(private readonly questionService: QuestionService) {}

  ngOnInit(): void {
    this.loadSummary();
  }

  loadSummary(): void {
    this.questionService.getSummary().subscribe(summary => this.summary.set(summary));
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files && input.files.length > 0 ? input.files[0] : null;
  }

  upload(): void {
    if (!this.selectedFile) {
      return;
    }
    this.uploading = true;
    this.questionService.bulkImport(this.selectedFile).subscribe({
      next: report => {
        this.report.set(report);
        this.uploading = false;
        this.loadSummary();
      },
      error: () => (this.uploading = false)
    });
  }
}
```

- [ ] **Step 12: Write the template**

Create `frontend/src/app/features/admin/questions/bulk-import.component.html`:
```html
<div class="bulk-import-page">
  <h1>استيراد أسئلة بالجملة</h1>

  <input type="file" accept=".xlsx" (change)="onFileSelected($event)" />
  <button (click)="upload()" [disabled]="!selectedFile || uploading">
    {{ uploading ? 'جارِ الاستيراد...' : 'استيراد' }}
  </button>

  <div class="import-report" *ngIf="report() as r">
    <p>الإجمالي: {{ r.totalRows }} — نجح: {{ r.successCount }} — فشل: {{ r.failureCount }}</p>
    <table *ngIf="r.errors.length > 0">
      <thead>
        <tr><th>الشيت</th><th>الصف</th><th>السبب</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let error of r.errors">
          <td>{{ error.sheet }}</td>
          <td>{{ error.rowNumber }}</td>
          <td>{{ error.message }}</td>
        </tr>
      </tbody>
    </table>
  </div>

  <h2>تغطية البنك الحالية (Topic × Difficulty)</h2>
  <table>
    <thead>
      <tr><th>الموضوع</th><th>الصعوبة</th><th>اختيار من متعدد</th><th>أكمل</th></tr>
    </thead>
    <tbody>
      <tr *ngFor="let row of summary()">
        <td>{{ row.topicName }}</td>
        <td>{{ row.difficulty }}</td>
        <td>{{ row.mcqCount }}</td>
        <td>{{ row.fillBlankCount }}</td>
      </tr>
    </tbody>
  </table>
</div>
```

- [ ] **Step 13: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/bulk-import.component.spec.ts'`
Expected: `TOTAL: 2 SUCCESS`

- [ ] **Step 14: Add the `/admin/questions/import` route**

Modify `frontend/src/app/app.routes.ts` -- add after the `questions` route added in Task 11 Step 6:
```typescript
      {
        path: 'questions/import',
        loadComponent: () =>
          import('./features/admin/questions/bulk-import.component').then(m => m.BulkImportComponent)
      },
```

- [ ] **Step 15: Run the full frontend test suite and production build**

Run:
```bash
cd frontend && npx ng test --watch=false --browsers=ChromeHeadless
npm run build -- --configuration production
cd ..
```
Expected: all suites `SUCCESS`, 0 failures; production build completes with no errors.

- [ ] **Step 16: Commit**

```bash
git add frontend/src/app
git commit -m "feat(frontend): add bulk import page with per-row validation report and bank coverage summary"
```

---

### Task 13: End-to-End Manual Verification (Phase 1a Deliverable)

**Files:** none (manual verification only)

- [x] **Step 1: Start the backend API**

Run: `dotnet run --project src/ExamSystem.Api`
Expected: no unhandled startup exceptions; `Topics`/`Questions`/`QuestionOptions` tables present (Task 2's migration applied).

- [x] **Step 2: Start the Angular dev server**

In a second terminal: `cd frontend && npx ng serve --port 4200`
Expected: `✔ Compiled successfully.`

- [x] **Step 3: Log in and navigate the new admin pages**

Log in at `http://localhost:4200/login` as the seeded admin, then visit `/admin/topics`, `/admin/questions`, and `/admin/questions/import` via the nav links added in Task 9 Step 10.
Expected: all three pages load without console errors.

- [x] **Step 4: Create a Topic through the UI**

On `/admin/topics`, add a topic (e.g. "اختبار يدوي"), confirm it appears in the table with question count 0.

- [x] **Step 5: Create one MCQ and one FillBlank question through the UI**

On `/admin/questions`, select the topic from Step 4, create an MCQ question with 2+ options (mark exactly one correct) and a FillBlank question. Try submitting a FillBlank with answer "Two Words" first.
Expected: the malformed FillBlank submission does not appear in the list (backend validator rejected it -- confirm via Network tab that the request returned 400); after fixing the answer to a single lowercase word, it saves and appears.

**Actual result:** `QuestionFormComponent.onCorrectAnswerInput()` normalizes the answer live as the admin types (strips whitespace, lowercases), so "Two Words" becomes "twowords" client-side before submission ever happens — there is no malformed round-trip to observe via the Network tab. FR-3.2.1 is still enforced, just earlier in the pipeline (client-side prevention instead of server-side rejection), which is a stronger UX guarantee than the plan anticipated. Both questions saved successfully (`POST /api/admin/questions` → 200 OK).

- [x] **Step 6: Deactivate a question and confirm the filter reflects it**

Click "تعطيل" on the FillBlank question just created; confirm its status flips to "معطّل" (soft delete, not removed from the table -- FR-3.5).

**Actual result:** confirmed — status flipped to "معطّل", row remained in the table. Also confirmed the deactivated question is correctly excluded from the Topic × Difficulty coverage summary (`GetQuestionBankSummaryQueryHandler`'s `IsActive` filter working as intended).

- [x] **Step 7: Import the real prepared question bank through the UI**

On `/admin/questions/import`, upload `docs/question-bank/questions_ready_for_import.xlsx`.
Expected: report shows `totalRows: 352`, `successCount: 352`, `failureCount: 0`; the coverage table below shows all 5 topics with Medium/Hard counts and zero rows for Easy (matches the known data gap documented in `docs/question-bank/import_validation_report.txt`).

**Actual result:** exact match — `{"totalRows":352,"successCount":352,"failureCount":0,"errors":[]}`. Coverage table counts doubled on top of Task 8's earlier import of the same workbook into this LocalDB (expected, since re-importing the same file has no dedup and this is a scratch dev database, not a concern for the plan's verification goal). Verified via a direct authenticated multipart request to the same endpoint the UI calls (no browser file-picker automation tool was available in this environment to drive the actual `<input type=file>`; the backend round trip is identical either way).

- [x] **Step 8: Upload a question image**

Edit any imported question (or create a new one) and attach a small JPEG/PNG through the file input.
Expected: `POST /api/admin/questions/image` returns 200 with a `/question-images/...` URL, and the `<img>` preview renders it.

**Bug found and fixed:** the endpoint returned 200 with a valid URL and the file was genuinely written to `wwwroot/question-images/`, but `GET` on that URL returned 404. Root cause: ASP.NET Core resolves `IWebHostEnvironment.WebRootFileProvider` to a `NullFileProvider` at host-build time if `wwwroot` doesn't exist on disk yet, and `LocalImageStorageService` only created that directory lazily on first upload -- well after `builder.Build()` had already run. This is a first-run/fresh-clone bug (`wwwroot` is rightly not committed to git) that would have broken image upload in any fresh deployment. Zero existing tests (unit or integration) exercised the image endpoint's actual serve path, which is how it went undetected through Task 6. Fixed in `src/ExamSystem.Api/Program.cs` by creating `wwwroot` before `builder.Build()`, and added a regression integration test (`UploadImage_ThenGetReturnedUrl_ServesTheUploadedFile`) that uploads then fetches the same URL through the same `WebApplicationFactory` client. See commit `19407eb`. Re-verified live after the fix: `GET /question-images/{name}.png` → `200 OK, image/png`.

- [x] **Step 9: Verify Topic deletion is blocked once it has questions**

Attempt to delete (not deactivate) the topic used in Step 4 via a direct API call: `DELETE /api/admin/topics/{id}` with the admin Bearer token.
Expected: 400 with `"Cannot delete a topic that has questions -- deactivate it instead."`

**Actual result:** exact match — `400 {"errors":["Cannot delete a topic that has questions -- deactivate it instead."]}`.

- [x] **Step 10: Stop both dev servers**

Both dev servers (backend + frontend) stopped.

- [x] **Step 11: Record the checkpoint**

Update this plan file: check off all remaining steps and add a one-line completion note at the bottom, following the same pattern used in `docs/superpowers/plans/2026-07-04-phase0-foundation.md`.

- [x] **Step 12: Commit**

```bash
git add docs/superpowers/plans/2026-07-06-phase1a-question-bank.md
git commit -m "docs: mark Phase 1a question bank plan complete"
```

---

## Definition of Done (Phase 1a)

- [x] `dotnet build` succeeds for the whole solution.
- [x] `dotnet test` passes for both backend test projects (unit + integration), including the real-workbook import test from Task 8. Final count: 37 backend tests (27 Application unit + 10 Api integration).
- [x] `ng test` passes for the Angular workspace. Final count: 50 frontend tests.
- [x] `ng build --configuration production` succeeds.
- [x] Admin can create/list/delete Topics through the real UI, with delete blocked once a Topic has questions.
- [x] Admin can create/list/deactivate MCQ and FillBlank Questions through the real UI, with the FillBlank single-lowercase-word rule (FR-3.2.1) enforced server-side and reflected in the UI (verified enforced even earlier, client-side, before the request is ever sent).
- [x] Admin can upload a question image and see it persisted/served.
- [x] Admin can bulk-import `docs/question-bank/questions_ready_for_import.xlsx` and get a per-row validation report with 0 failures.
- [x] The Topic × Difficulty coverage summary (available side of FR-3.7) is visible on the Bulk Import page.

## Out of Scope for This Plan (deferred to Phase 1b per PRD v1.3 FR-4)

- Exam entity, `ExamTopicConfigs`, and the Topic×Difficulty *required-count* side of FR-3.7.
- Exam lifecycle (Draft → Published → Active → Closed → Archived) and publish-time bank-sufficiency validation (FR-4.9).
- Scoring configuration (the 25 MCQ x 2pts + 5 FillBlank x 5pts = 75 default from PRD v1.3 FR-4.11/4.12).
- Question analytics (FR-3.6 -- appearance count, correct-answer rate) -- Could priority, needs `ExamAttempts` data that doesn't exist until Phase 2.
- `AcceptedAlternatives` (synonym list for FillBlank answers) -- the `Question` entity has no column for it yet; PRD marks it Could-priority (§ FR-3.2 note 5).
- The candidate-facing exam landing page (PRD v1.3 § 6.3) -- that is Candidate Registration + Exam Engine territory (Phase 2), not Question Bank.

Phase 1b (Exam Configuration) should get its own plan document via superpowers:writing-plans once this plan is merged, per the PRD's phase breakdown and this plan's scope-check note.

---

Phase 1a completed on 2026-07-05 — backend (37 tests: 27 Application unit + 10 Api integration) and frontend (50 tests) green, `ng build --configuration production` clean, manual E2E verified against real LocalDB + running Angular dev server: Topics/Questions/Bulk Import CRUD, image upload, and Topic-delete-blocked-when-has-questions all confirmed live. One real bug was found and fixed during manual verification — uploaded question images returned a 200/URL but weren't actually servable (`wwwroot` didn't exist at ASP.NET Core host-build time, so `WebRootFileProvider` cached a `NullFileProvider`); fixed in commit `19407eb` with a new upload-then-serve regression test, since no prior test had covered that path. Every task (1-13) went through the full subagent-driven-development loop -- implementer, independent spec-compliance review, independent code-quality review, and fix-and-reverify for any Important/Critical findings -- with several other real defects caught and fixed along the way (JWT validation timing, missing DI registration, a duplicated FillBlank regex, an image-extension/content-type security mismatch, a dead image-upload wire in the Questions admin page, silent form-validation failures, and silent bulk-import/summary-load failures).
