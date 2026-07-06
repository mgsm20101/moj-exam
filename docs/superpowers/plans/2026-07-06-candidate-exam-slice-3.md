# Candidate Exam — Slice 3 Implementation Plan (Reliability & Anti-Cheat)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden the candidate exam with a background auto-submit job for expired attempts, IP-based rate limiting on the public endpoints, and best-effort anti-cheat (copy/paste + right-click lockdown and a tab-switch count).

**Architecture:** Three independent subsystems following the existing Clean Architecture + CQRS patterns. A testable `IExpiredAttemptCloser` driven by a hosted `BackgroundService`; the built-in ASP.NET Core rate limiter with a `candidate` policy; and a `TabSwitchCount` on `ExamAttempt` fed by a token-authenticated endpoint plus player-side listeners.

**Tech Stack:** .NET 8 (`System.Threading.RateLimiting`, `Microsoft.AspNetCore.RateLimiting`, `BackgroundService`), EF Core 8, MediatR, xUnit; Angular 17.

**Spec:** `docs/superpowers/specs/2026-07-06-candidate-exam-slice-3-design.md`
**Depends on:** Slices 1a/1b/2 (done). Reuses `IAttemptGradingService`, `ExamAttempt`, `AttemptToken`, `CandidateAttemptController`, `AttemptPlayerComponent`.

---

## Reference: existing shapes this plan uses

- `ExamAttempt` — `Status (ExamAttemptStatus.InProgress|Submitted|AutoSubmitted|Terminated)`, `ExpiresAtUtc`, `SubmittedAtUtc?`, `Score?`, nav `Questions`→`Options`, `Answers`.
- `IAttemptGradingService.Grade(ExamAttempt attempt, Exam exam)` sets `AttemptAnswer.IsCorrect` and returns a `GradeResult` (Score/TotalPoints/PassMark/Passed).
- `CandidateAttemptController` — `[Authorize(AuthenticationSchemes = "AttemptToken")]`, `Resolve(examId, out attemptId)` reads the token's `attempt_id`/`exam_id` claims (403 on mismatch); actions use `ISender`.
- `CandidateExamController` (`api/exam` — landing/register/start) and `CandidateQueueController` (`api/exam/{id}/queue/status`) are `[AllowAnonymous]`.
- `AttemptPlayerComponent` — `examId`, `state: AttemptState | null`, `ngOnInit`/`ngOnDestroy`; `CandidateAttemptService` has `state/saveAnswer/submit/result` and `apiBaseUrl` is `/api`.
- `Program.cs` pipeline order: `UseHttpsRedirection` → `UseStaticFiles` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers`.

---

## File Structure

**Backend — Application**
- `Common/Interfaces/IExpiredAttemptCloser.cs` (new)
- `Features/CandidateExam/TakeExam/RecordTabSwitchCommand.cs` + `RecordTabSwitchCommandHandler.cs` (new)

**Backend — Infrastructure**
- `BackgroundJobs/ExpiredAttemptCloser.cs` (new)
- `BackgroundJobs/ExpiredAttemptSubmissionService.cs` (new)
- `DependencyInjection.cs` (modify — register the closer)

**Backend — Domain**
- `Attempts/ExamAttempt.cs` (modify — `TabSwitchCount`)

**Backend — API**
- `Program.cs` (modify — hosted service, rate limiter, `UseRateLimiter`)
- `Controllers/CandidateExamController.cs`, `Controllers/CandidateQueueController.cs` (modify — `[EnableRateLimiting]`)
- `Controllers/CandidateAttemptController.cs` (modify — tab-switch action)
- `appsettings.json` (modify — RateLimiting + AutoSubmit defaults)

**Backend — Tests**
- `tests/ExamSystem.Application.UnitTests/BackgroundJobs/ExpiredAttemptCloserTests.cs`
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RecordTabSwitchCommandHandlerTests.cs`
- `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateRateLimitTests.cs`
- `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs` (modify — generous default limit)

**Frontend**
- `core/services/candidate-attempt.service.ts` (modify — `recordTabSwitch`)
- `features/candidate/attempt-player.component.ts` (modify — anti-cheat listeners) + `.spec.ts` (modify)

---

## Task 1: `IExpiredAttemptCloser` (Part A core)

**Files:**
- Create: `src/ExamSystem.Application/Common/Interfaces/IExpiredAttemptCloser.cs`
- Create: `src/ExamSystem.Infrastructure/BackgroundJobs/ExpiredAttemptCloser.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Test: `tests/ExamSystem.Application.UnitTests/BackgroundJobs/ExpiredAttemptCloserTests.cs`

- [ ] **Step 1: Define the interface**

Create `src/ExamSystem.Application/Common/Interfaces/IExpiredAttemptCloser.cs`:

```csharp
namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Grades and closes every InProgress attempt whose timer has expired (FR-2.7 background path).
/// Returns the number of attempts closed. Shares its outcome with the lazy auto-submit in 1b.
/// </summary>
public interface IExpiredAttemptCloser
{
    Task<int> CloseExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/BackgroundJobs/ExpiredAttemptCloserTests.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.BackgroundJobs;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.BackgroundJobs;

public class ExpiredAttemptCloserTests
{
    private static (Infrastructure.Persistence.ApplicationDbContext db, Exam exam) NewDb()
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m };
        db.Exams.Add(exam);
        return (db, exam);
    }

    private static ExamAttempt AnsweredAttempt(Exam exam, DateTime expiresAtUtc)
    {
        var opt = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var q = new AttemptQuestion
        {
            DisplayOrder = 1, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "Q", Options = new List<AttemptQuestionOption> { opt }
        };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-90), ExpiresAtUtc = expiresAtUtc,
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Questions.Add(q);
        attempt.Answers.Add(new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = q.Id, SelectedOptionId = opt.Id });
        return attempt;
    }

    [Fact]
    public async Task CloseExpired_ExpiredInProgress_GradesAndAutoSubmits()
    {
        var (db, exam) = NewDb();
        db.ExamAttempts.Add(AnsweredAttempt(exam, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);

        var closed = await new ExpiredAttemptCloser(db, new AttemptGradingService()).CloseExpiredAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, closed);
        var attempt = db.ExamAttempts.Single();
        Assert.Equal(ExamAttemptStatus.AutoSubmitted, attempt.Status);
        Assert.Equal(2m, attempt.Score);
        Assert.NotNull(attempt.SubmittedAtUtc);
    }

    [Fact]
    public async Task CloseExpired_NotExpiredOrAlreadyClosed_AreUntouched()
    {
        var (db, exam) = NewDb();
        db.ExamAttempts.Add(AnsweredAttempt(exam, DateTime.UtcNow.AddMinutes(30))); // not expired
        var submitted = AnsweredAttempt(exam, DateTime.UtcNow.AddMinutes(-1));
        submitted.Status = ExamAttemptStatus.Submitted;                              // already closed
        db.ExamAttempts.Add(submitted);
        await db.SaveChangesAsync(CancellationToken.None);

        var closed = await new ExpiredAttemptCloser(db, new AttemptGradingService()).CloseExpiredAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, closed);
        Assert.Equal(0, db.ExamAttempts.Count(a => a.Status == ExamAttemptStatus.AutoSubmitted));
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~ExpiredAttemptCloserTests`
Expected: FAIL — `ExpiredAttemptCloser` does not exist.

- [ ] **Step 4: Implement the closer**

Create `src/ExamSystem.Infrastructure/BackgroundJobs/ExpiredAttemptCloser.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.BackgroundJobs;

public class ExpiredAttemptCloser : IExpiredAttemptCloser
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public ExpiredAttemptCloser(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<int> CloseExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var expired = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .Where(a => a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        var examIds = expired.Select(a => a.ExamId).Distinct().ToList();
        var exams = await _db.Exams.Where(e => examIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);

        foreach (var attempt in expired)
        {
            if (!exams.TryGetValue(attempt.ExamId, out var exam))
            {
                continue;
            }
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = ExamAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.ExpiresAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
```

- [ ] **Step 5: Register the closer**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` — add the using:

```csharp
using ExamSystem.Infrastructure.BackgroundJobs;
```

Add before `return services;`:

```csharp
        services.AddScoped<IExpiredAttemptCloser, ExpiredAttemptCloser>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~ExpiredAttemptCloserTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Common/Interfaces/IExpiredAttemptCloser.cs src/ExamSystem.Infrastructure/BackgroundJobs/ExpiredAttemptCloser.cs src/ExamSystem.Infrastructure/DependencyInjection.cs tests/ExamSystem.Application.UnitTests/BackgroundJobs/
git commit -m "feat: expired-attempt closer (grades + auto-submits timed-out attempts)"
```

---

## Task 2: Background service (Part A host)

**Files:**
- Create: `src/ExamSystem.Infrastructure/BackgroundJobs/ExpiredAttemptSubmissionService.cs`
- Modify: `src/ExamSystem.Api/Program.cs`
- Modify: `src/ExamSystem.Api/appsettings.json`

- [ ] **Step 1: Write the hosted service**

Create `src/ExamSystem.Infrastructure/BackgroundJobs/ExpiredAttemptSubmissionService.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExamSystem.Infrastructure.BackgroundJobs;

/// <summary>Periodically closes expired attempts (FR-2.7), supplementing 1b's lazy auto-submit.</summary>
public class ExpiredAttemptSubmissionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredAttemptSubmissionService> _logger;
    private readonly TimeSpan _interval;

    public ExpiredAttemptSubmissionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExpiredAttemptSubmissionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = configuration.GetValue<int?>("AutoSubmit:IntervalSeconds") ?? 60;
        _interval = TimeSpan.FromSeconds(Math.Max(5, seconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var closer = scope.ServiceProvider.GetRequiredService<IExpiredAttemptCloser>();
                var closed = await closer.CloseExpiredAsync(DateTime.UtcNow, stoppingToken);
                if (closed > 0)
                {
                    _logger.LogInformation("Auto-submitted {Count} expired attempt(s).", closed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired-attempt auto-submit tick failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
```

- [ ] **Step 2: Register the hosted service**

Modify `src/ExamSystem.Api/Program.cs` — add the using at the top (with the other usings):

```csharp
using ExamSystem.Infrastructure.BackgroundJobs;
```

Add after `builder.Services.AddInfrastructure(builder.Configuration);` (near the top of the service registrations):

```csharp
builder.Services.AddHostedService<ExpiredAttemptSubmissionService>();
```

- [ ] **Step 3: Add the interval default to config**

Modify `src/ExamSystem.Api/appsettings.json` — add a sibling key (after the `AttemptToken` block):

```json
  "AutoSubmit": {
    "IntervalSeconds": 60
  },
```

- [ ] **Step 4: Verify build**

Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded. (The closer logic is already covered by Task 1's unit tests; the loop itself is a thin timer.)

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Infrastructure/BackgroundJobs/ExpiredAttemptSubmissionService.cs src/ExamSystem.Api/Program.cs src/ExamSystem.Api/appsettings.json
git commit -m "feat(api): background service that auto-submits expired attempts"
```

---

## Task 3: Rate limiting (Part B)

**Files:**
- Modify: `src/ExamSystem.Api/Program.cs`
- Modify: `src/ExamSystem.Api/Controllers/CandidateExamController.cs`
- Modify: `src/ExamSystem.Api/Controllers/CandidateQueueController.cs`
- Modify: `src/ExamSystem.Api/appsettings.json`
- Modify: `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs`
- Test: `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateRateLimitTests.cs`

- [ ] **Step 1: Register the rate limiter**

Modify `src/ExamSystem.Api/Program.cs` — add usings at the top:

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
```

Add after the CORS registration block (before `var app = builder.Build();`):

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("candidate", httpContext =>
    {
        // Partition by client IP; fall back to a constant key when the IP is unavailable
        // (behind some proxies / in the test host) so the limit still applies deterministically.
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "candidate-shared";
        var permitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue<int?>("RateLimiting:Candidate:PermitLimit") ?? 20;
        var windowSeconds = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue<int?>("RateLimiting:Candidate:WindowSeconds") ?? 60;
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });
});
```

Add `app.UseRateLimiter();` in the pipeline, right before `app.MapControllers();`:

```csharp
app.UseRateLimiter();
app.MapControllers();
```

- [ ] **Step 2: Apply the policy to the public candidate controllers**

Modify `src/ExamSystem.Api/Controllers/CandidateExamController.cs` — add the using and the attribute on the class:

```csharp
using Microsoft.AspNetCore.RateLimiting;
```
```csharp
[ApiController]
[Route("api/exam")]
[AllowAnonymous]
[EnableRateLimiting("candidate")]
public class CandidateExamController : ControllerBase
```

Modify `src/ExamSystem.Api/Controllers/CandidateQueueController.cs` the same way (add the using and `[EnableRateLimiting("candidate")]` on the class).

- [ ] **Step 3: Add config defaults + a generous test default**

Modify `src/ExamSystem.Api/appsettings.json` — add (after the `AutoSubmit` block):

```json
  "RateLimiting": {
    "Candidate": {
      "PermitLimit": 20,
      "WindowSeconds": 60
    }
  },
```

Modify `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs` — add to the in-memory config dictionary (so the existing candidate integration tests, which make several register/start calls, are never rate-limited):

```csharp
                ["RateLimiting:Candidate:PermitLimit"] = "1000",
                ["RateLimiting:Candidate:WindowSeconds"] = "60",
```

- [ ] **Step 4: Write the failing rate-limit test**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateRateLimitTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateRateLimitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public CandidateRateLimitTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ExceedingTheLimit_Returns429()
    {
        // Override the (otherwise generous) limit to a small value just for this test's host.
        var strictFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Candidate:PermitLimit"] = "3",
                    ["RateLimiting:Candidate:WindowSeconds"] = "60"
                })));

        var client = strictFactory.CreateClient();
        var body = new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };
        var examId = Guid.NewGuid(); // unknown exam is fine; the limiter runs before the handler

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync($"/api/exam/{examId}/register", body);
            statuses.Add(resp.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
```

- [ ] **Step 5: Run to verify it fails, then passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter FullyQualifiedName~CandidateRateLimitTests`
Expected: after Steps 1-3, PASS (the 4th+ request within the window returns 429).

- [ ] **Step 6: Run the full backend suite**

Run: `dotnet test ExamSystem.sln`
Expected: all pass (the generous test-factory limit keeps the other candidate tests green).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Api/Program.cs src/ExamSystem.Api/Controllers/CandidateExamController.cs src/ExamSystem.Api/Controllers/CandidateQueueController.cs src/ExamSystem.Api/appsettings.json tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateRateLimitTests.cs
git commit -m "feat(api): IP-based rate limiting on public candidate endpoints"
```

---

## Task 4: `TabSwitchCount` field + migration (Part C data)

**Files:**
- Modify: `src/ExamSystem.Domain/Attempts/ExamAttempt.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Domain/QueueEntityDefaultsTests.cs` (extend)

- [ ] **Step 1: Write the failing test**

Add to `tests/ExamSystem.Application.UnitTests/Domain/QueueEntityDefaultsTests.cs` (a new fact in the existing class):

```csharp
    [Fact]
    public void ExamAttempt_Defaults_HaveZeroTabSwitches()
    {
        var attempt = new ExamSystem.Domain.Attempts.ExamAttempt();
        Assert.Equal(0, attempt.TabSwitchCount);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QueueEntityDefaultsTests`
Expected: FAIL — `TabSwitchCount` does not exist.

- [ ] **Step 3: Add the field**

Modify `src/ExamSystem.Domain/Attempts/ExamAttempt.cs` — add after `public int Seed { get; set; }`:

```csharp
    /// <summary>Best-effort integrity signal (Slice 3): times the candidate left the exam tab.</summary>
    public int TabSwitchCount { get; set; }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QueueEntityDefaultsTests`
Expected: PASS.

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddTabSwitchCount \
  --project src/ExamSystem.Infrastructure \
  --startup-project src/ExamSystem.Api \
  --output-dir Migrations
```
Expected: a migration adding the `TabSwitchCount` column to `ExamAttempts`. Open it and confirm.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Domain/Attempts/ExamAttempt.cs src/ExamSystem.Infrastructure/Migrations/ tests/ExamSystem.Application.UnitTests/Domain/QueueEntityDefaultsTests.cs
git commit -m "feat(domain): add ExamAttempt.TabSwitchCount + migration"
```

---

## Task 5: Record-tab-switch command + endpoint (Part C backend)

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/RecordTabSwitchCommand.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/RecordTabSwitchCommandHandler.cs`
- Modify: `src/ExamSystem.Api/Controllers/CandidateAttemptController.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RecordTabSwitchCommandHandlerTests.cs`

- [ ] **Step 1: Write the command**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/RecordTabSwitchCommand.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record RecordTabSwitchCommand(Guid AttemptId) : IRequest<Result<bool>>;
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RecordTabSwitchCommandHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class RecordTabSwitchCommandHandlerTests
{
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt)> SeedAsync(ExamAttemptStatus status)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60 };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            Status = status
        };
        db.Exams.Add(exam);
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt);
    }

    [Fact]
    public async Task Handle_InProgress_IncrementsCount()
    {
        var (db, attempt) = await SeedAsync(ExamAttemptStatus.InProgress);
        var handler = new RecordTabSwitchCommandHandler(db);

        await handler.Handle(new RecordTabSwitchCommand(attempt.Id), CancellationToken.None);
        await handler.Handle(new RecordTabSwitchCommand(attempt.Id), CancellationToken.None);

        Assert.Equal(2, db.ExamAttempts.Single().TabSwitchCount);
    }

    [Fact]
    public async Task Handle_NotInProgress_DoesNotIncrement()
    {
        var (db, attempt) = await SeedAsync(ExamAttemptStatus.Submitted);
        var handler = new RecordTabSwitchCommandHandler(db);

        var result = await handler.Handle(new RecordTabSwitchCommand(attempt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, db.ExamAttempts.Single().TabSwitchCount);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~RecordTabSwitchCommandHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/RecordTabSwitchCommandHandler.cs`:

```csharp
using ExamSystem.Domain.Attempts;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class RecordTabSwitchCommandHandler : IRequestHandler<RecordTabSwitchCommand, Result<bool>>
{
    private readonly IApplicationDbContext _db;

    public RecordTabSwitchCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RecordTabSwitchCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts.FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<bool>.Failure("Attempt not found.");
        }

        if (attempt.Status == ExamAttemptStatus.InProgress)
        {
            attempt.TabSwitchCount += 1;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
```

- [ ] **Step 5: Add the controller action**

Modify `src/ExamSystem.Api/Controllers/CandidateAttemptController.cs` — add the action (after the `Result` action, before the `SaveAnswerRequest` record):

```csharp
    [HttpPost("tab-switch")]
    public async Task<IActionResult> TabSwitch(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        await _sender.Send(new RecordTabSwitchCommand(attemptId), cancellationToken);
        return NoContent();
    }
```

- [ ] **Step 6: Run to verify it passes + build**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~RecordTabSwitchCommandHandlerTests`
Expected: PASS (2 tests).
Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/TakeExam/RecordTabSwitch* src/ExamSystem.Api/Controllers/CandidateAttemptController.cs tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RecordTabSwitchCommandHandlerTests.cs
git commit -m "feat(api): record tab-switch count on the attempt (token-authenticated)"
```

---

## Task 6: Frontend anti-cheat + tab-switch reporting (Part C UI)

**Files:**
- Modify: `frontend/src/app/core/services/candidate-attempt.service.ts`
- Modify: `frontend/src/app/features/candidate/attempt-player.component.ts`
- Modify: `frontend/src/app/features/candidate/attempt-player.component.spec.ts`

- [ ] **Step 1: Add the service method**

In `frontend/src/app/core/services/candidate-attempt.service.ts`, add inside the class (after `result(...)`):

```typescript
  recordTabSwitch(examId: string): Observable<void> {
    return this.http.post<void>(`${this.base(examId)}/tab-switch`, {});
  }
```

- [ ] **Step 2: Add anti-cheat + tab-switch to the player**

In `frontend/src/app/features/candidate/attempt-player.component.ts`, add these bound handlers and wire them in `applyState` (when the attempt is InProgress) and clean them up in `ngOnDestroy`.

Add fields to the class (near the other private fields):

```typescript
  private lastTabSwitchSentAt = 0;
  private readonly onVisibility = () => {
    if (document.hidden && this.state?.status === 'InProgress') {
      const now = Date.now();
      if (now - this.lastTabSwitchSentAt > 3000) {   // throttle a switch storm to one call / 3s
        this.lastTabSwitchSentAt = now;
        this.service.recordTabSwitch(this.examId).subscribe({ error: () => {} });
      }
    }
  };
  private readonly blockEvent = (e: Event) => e.preventDefault();
```

In `applyState(s)`, after `this.startTimer();` (i.e. only for the InProgress branch), register the listeners:

```typescript
    document.addEventListener('visibilitychange', this.onVisibility);
    document.addEventListener('contextmenu', this.blockEvent);
    document.addEventListener('copy', this.blockEvent);
    document.addEventListener('cut', this.blockEvent);
    document.addEventListener('paste', this.blockEvent);
```

In `ngOnDestroy()`, remove them (alongside the existing `clearInterval`):

```typescript
    document.removeEventListener('visibilitychange', this.onVisibility);
    document.removeEventListener('contextmenu', this.blockEvent);
    document.removeEventListener('copy', this.blockEvent);
    document.removeEventListener('cut', this.blockEvent);
    document.removeEventListener('paste', this.blockEvent);
```

- [ ] **Step 3: Update the player spec**

In `frontend/src/app/features/candidate/attempt-player.component.spec.ts`, add `recordTabSwitch` to the `serviceStub` and a test. Change the stub object to include:

```typescript
    recordTabSwitch: jasmine.createSpy('recordTabSwitch').and.returnValue(of(void 0)),
```

Add this test inside the `describe`:

```typescript
  it('reports a tab switch when the document becomes hidden', () => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
    document.dispatchEvent(new Event('visibilitychange'));
    expect(serviceStub.recordTabSwitch).toHaveBeenCalledWith('e1');
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => false });
  });
```

- [ ] **Step 4: Build + run the frontend suite**

Run: `cd frontend && npm run build`
Expected: Build succeeds.
Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: PASS (existing + the new tab-switch test).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/core/services/candidate-attempt.service.ts frontend/src/app/features/candidate/attempt-player.component.ts frontend/src/app/features/candidate/attempt-player.component.spec.ts
git commit -m "feat(candidate-ui): best-effort anti-cheat and tab-switch reporting in the player"
```

---

## Task 7: End-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Run both servers** (the API applies the `AddTabSwitchCount` migration and starts the background service on startup).

- [ ] **Step 2: Background auto-submit.** Create a published exam with a very short duration (e.g., `durationMinutes = 1`) via the admin/API and start an attempt. Do nothing for ~1–2 minutes. Confirm via `preview_logs` on the API that the service logged `Auto-submitted 1 expired attempt(s)`, and that `GET .../attempt/state` (or the result) now shows `AutoSubmitted`.

- [ ] **Step 3: Rate limiting.** With a small configured limit (or by rapidly repeating), issue >limit `POST /api/exam/{id}/register` calls (e.g., via the browser console `fetch` loop) and confirm some return `429`.

- [ ] **Step 4: Anti-cheat.** In the player, right-click → confirm the context menu is suppressed; switch tabs and back → confirm via `preview_network` that `POST .../attempt/tab-switch` fired (`204`).

- [ ] **Step 5: Final full test run.**
  Run: `dotnet test ExamSystem.sln` and `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
  Expected: all green.

---

## Self-Review — Spec Coverage

- Spec §3 Part A (`IExpiredAttemptCloser` + `BackgroundService`, reuse grading, `AutoSubmit:IntervalSeconds`) → Tasks 1, 2. ✅
- Spec §4 Part B (rate limiter, `candidate` policy, IP partition + fallback, config, `[EnableRateLimiting]` on public controllers, 429) → Task 3. ✅
- Spec §5 Part C backend (`TabSwitchCount`, `RecordTabSwitchCommand`, token-auth endpoint, increment only InProgress) → Tasks 4, 5. ✅
- Spec §5 Part C frontend (visibilitychange throttled report, contextmenu/copy/cut/paste block, cleanup on destroy, errors ignored) → Task 6. ✅
- Spec §6 edge cases (tick failure caught, idempotent close, tab-switch after submit no-ops, listeners never throw) → Tasks 2, 1, 5, 6. ✅
- Spec §7 testing (closer unit, rate-limit integration, tab-switch unit, frontend spec) → Tasks 1, 3, 5, 6. ✅

No placeholders remain. Type/name usage is consistent (`IExpiredAttemptCloser.CloseExpiredAsync`, `ExpiredAttemptSubmissionService`, `RecordTabSwitchCommand`, `TabSwitchCount`, `recordTabSwitch`, policy name `"candidate"`, config keys `AutoSubmit:IntervalSeconds` / `RateLimiting:Candidate:PermitLimit`/`WindowSeconds`). **Deferred (per spec §8):** OTP, admin report surfacing the count, daily aggregation job.
