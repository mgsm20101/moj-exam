# Phase 0 — Foundation (Skeleton + Admin Login) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a working, tested skeleton of the Online Examination System: a .NET 8 Clean Architecture backend (Domain/Application/Infrastructure/API) with CQRS (MediatR + FluentValidation), JWT-based admin authentication backed by ASP.NET Identity, and an Angular 17 standalone frontend with RTL/PrimeNG theming — such that a seeded Admin user can log in end-to-end through the UI and receive a valid JWT.

**Architecture:** Backend is Clean Architecture: `Domain` (no dependencies) ← `Application` (CQRS commands/handlers, validation pipeline, interfaces) ← `Infrastructure` (EF Core, ASP.NET Identity, JWT generation) ← `Api` (composition root, controllers). Frontend is an Angular 17 standalone-components workspace organized into `core/` (singleton services, guards, interceptors), `shared/`, `features/` (lazy-loaded), and `layouts/`, using PrimeNG with a right-to-left (RTL) theme and design tokens in `variables.scss`. This plan targets **.NET 8** and **Free-Tier Cloud deployment posture** (EF Core kept provider-agnostic; no feature here is SQL-Server-specific) per the PRD decisions, and supports multiple parallel exams in later phases (no single-active-exam assumption is baked in here).

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8 (SQL Server provider for now), ASP.NET Identity, MediatR, FluentValidation, JWT Bearer auth, xUnit + Moq, Angular 17 (standalone), PrimeNG, Karma/Jasmine.

---

## Prerequisites (verify before Task 1)

- [ ] **Step 1: Verify .NET 8 SDK is installed**

Run: `dotnet --list-sdks`
Expected: output includes a line starting with `8.` (e.g. `8.0.4xx [C:\Program Files\dotnet\sdk]`)

If missing, install it first: `winget install --id Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements`, then re-run the check.

- [ ] **Step 2: Verify Angular CLI 17 is usable**

Run: `npx -y @angular/cli@17 version`
Expected: prints `Angular CLI: 17.3.x`

- [ ] **Step 3: Confirm working directory and git**

Run: `cd D:/os/ExamSystem && git status`
Expected: `On branch master` (or `main`), repository already initialized, only `PRD-Exam-System_2.md` and `docs/` tracked/untracked so far.

---

## File Structure (target state after this plan)

```
D:/os/ExamSystem/
├─ ExamSystem.sln
├─ .gitignore
├─ src/
│  ├─ ExamSystem.Domain/
│  │  ├─ ExamSystem.Domain.csproj
│  │  ├─ GlobalUsings.cs
│  │  └─ Common/
│  │     ├─ BaseEntity.cs
│  │     └─ BaseAuditableEntity.cs
│  ├─ ExamSystem.Application/
│  │  ├─ ExamSystem.Application.csproj
│  │  ├─ GlobalUsings.cs
│  │  ├─ DependencyInjection.cs
│  │  ├─ Common/
│  │  │  ├─ Models/Result.cs
│  │  │  ├─ Interfaces/IIdentityService.cs
│  │  │  ├─ Interfaces/IJwtTokenGenerator.cs
│  │  │  └─ Behaviors/ValidationBehavior.cs
│  │  └─ Features/Auth/Login/
│  │     ├─ LoginCommand.cs
│  │     ├─ LoginCommandValidator.cs
│  │     └─ LoginCommandHandler.cs
│  ├─ ExamSystem.Infrastructure/
│  │  ├─ ExamSystem.Infrastructure.csproj
│  │  ├─ GlobalUsings.cs
│  │  ├─ DependencyInjection.cs
│  │  ├─ Identity/
│  │  │  ├─ ApplicationUser.cs
│  │  │  ├─ IdentityService.cs
│  │  │  ├─ JwtSettings.cs
│  │  │  └─ JwtTokenGenerator.cs
│  │  └─ Persistence/
│  │     ├─ ApplicationDbContext.cs
│  │     └─ DbInitializer.cs
│  └─ ExamSystem.Api/
│     ├─ ExamSystem.Api.csproj
│     ├─ Program.cs
│     ├─ appsettings.json
│     ├─ appsettings.Development.json
│     └─ Controllers/AuthController.cs
├─ tests/
│  ├─ ExamSystem.Application.UnitTests/
│  │  ├─ ExamSystem.Application.UnitTests.csproj
│  │  ├─ Behaviors/ValidationBehaviorTests.cs
│  │  └─ Features/Auth/LoginCommandHandlerTests.cs
│  └─ ExamSystem.Api.IntegrationTests/
│     ├─ ExamSystem.Api.IntegrationTests.csproj
│     ├─ TestWebApplicationFactory.cs
│     └─ Controllers/AuthControllerTests.cs
└─ frontend/                          (Angular 17 standalone workspace)
   ├─ src/styles/variables.scss
   ├─ src/index.html                  (dir="rtl" lang="ar")
   └─ src/app/
      ├─ app.config.ts / app.routes.ts
      ├─ core/
      │  ├─ services/auth.service.ts
      │  ├─ services/auth.service.spec.ts
      │  ├─ interceptors/auth.interceptor.ts
      │  ├─ interceptors/auth.interceptor.spec.ts
      │  └─ guards/auth.guard.ts
      │  └─ guards/auth.guard.spec.ts
      ├─ layouts/admin-layout/admin-layout.component.ts
      └─ features/auth/login/
         ├─ login.component.ts
         ├─ login.component.html
         └─ login.component.spec.ts
```

---

### Task 1: Solution & Project Scaffolding

**Files:**
- Create: `ExamSystem.sln`
- Create: `.gitignore`
- Create: `src/ExamSystem.Domain/ExamSystem.Domain.csproj`
- Create: `src/ExamSystem.Application/ExamSystem.Application.csproj`
- Create: `src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj`
- Create: `src/ExamSystem.Api/ExamSystem.Api.csproj`
- Create: `tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj`
- Create: `tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj`

- [ ] **Step 1: Create the solution and all projects**

Run (from `D:/os/ExamSystem`):

```bash
dotnet new sln -n ExamSystem

dotnet new classlib -n ExamSystem.Domain -o src/ExamSystem.Domain
dotnet new classlib -n ExamSystem.Application -o src/ExamSystem.Application
dotnet new classlib -n ExamSystem.Infrastructure -o src/ExamSystem.Infrastructure
dotnet new webapi -n ExamSystem.Api -o src/ExamSystem.Api --use-controllers

dotnet new xunit -n ExamSystem.Application.UnitTests -o tests/ExamSystem.Application.UnitTests
dotnet new xunit -n ExamSystem.Api.IntegrationTests -o tests/ExamSystem.Api.IntegrationTests

dotnet sln add src/ExamSystem.Domain/ExamSystem.Domain.csproj
dotnet sln add src/ExamSystem.Application/ExamSystem.Application.csproj
dotnet sln add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj
dotnet sln add src/ExamSystem.Api/ExamSystem.Api.csproj
dotnet sln add tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj
dotnet sln add tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj
```

Expected: each `dotnet new` prints "The template ... was created successfully", each `dotnet sln add` prints "Project ... added to the solution."

- [ ] **Step 2: Delete template placeholder files**

Run:
```bash
rm src/ExamSystem.Domain/Class1.cs
rm src/ExamSystem.Application/Class1.cs
rm src/ExamSystem.Infrastructure/Class1.cs
rm src/ExamSystem.Api/WeatherForecast.cs
rm src/ExamSystem.Api/Controllers/WeatherForecastController.cs
```

Expected: no error output.

- [ ] **Step 3: Wire project references**

Run:
```bash
dotnet add src/ExamSystem.Application/ExamSystem.Application.csproj reference src/ExamSystem.Domain/ExamSystem.Domain.csproj
dotnet add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj reference src/ExamSystem.Application/ExamSystem.Application.csproj
dotnet add src/ExamSystem.Api/ExamSystem.Api.csproj reference src/ExamSystem.Application/ExamSystem.Application.csproj
dotnet add src/ExamSystem.Api/ExamSystem.Api.csproj reference src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj
dotnet add tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj reference src/ExamSystem.Application/ExamSystem.Application.csproj
dotnet add tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj reference src/ExamSystem.Api/ExamSystem.Api.csproj
```

Expected: each prints "Reference ... added to the project."

- [ ] **Step 4: Add NuGet packages**

Run:
```bash
dotnet add src/ExamSystem.Application/ExamSystem.Application.csproj package MediatR --version 12.4.1
dotnet add src/ExamSystem.Application/ExamSystem.Application.csproj package FluentValidation.DependencyInjectionExtensions --version 11.10.0

dotnet add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.10
dotnet add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.10
dotnet add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.10
dotnet add src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj package System.IdentityModel.Tokens.Jwt --version 8.1.2

dotnet add src/ExamSystem.Api/ExamSystem.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.10
dotnet add src/ExamSystem.Api/ExamSystem.Api.csproj package Swashbuckle.AspNetCore --version 6.9.0

dotnet add tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj package Moq --version 4.20.72
dotnet add tests/ExamSystem.Application.UnitTests/ExamSystem.Application.UnitTests.csproj package FluentValidation --version 11.10.0

dotnet add tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 8.0.10
dotnet add tests/ExamSystem.Api.IntegrationTests/ExamSystem.Api.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.10
```

Expected: each prints "Restored ... " / no restore errors.

- [ ] **Step 5: Enable nullable reference types + implicit usings in every csproj**

Open each of the 6 `.csproj` files and ensure the `<PropertyGroup>` contains:
```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```
(The `webapi` and `xunit` templates already set these; `classlib` templates already set `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` too in .NET 8 — verify, don't duplicate if present.)

Expected: no build errors after verifying.

- [ ] **Step 6: Add root `.gitignore`**

Create `D:/os/ExamSystem/.gitignore`:
```gitignore
## .NET
bin/
obj/
*.user

## Angular / Node
frontend/node_modules/
frontend/dist/
frontend/.angular/

## IDE
.vs/
.vscode/

## Secrets
*.env
appsettings.*.local.json
```

- [ ] **Step 7: Build the solution**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors (warnings about unused usings are acceptable at this stage).

- [ ] **Step 8: Commit**

```bash
git add ExamSystem.sln .gitignore src tests
git commit -m "chore: scaffold Clean Architecture solution and test projects"
```

---

### Task 2: Domain Layer — Base Entities

**Files:**
- Create: `src/ExamSystem.Domain/Common/BaseEntity.cs`
- Create: `src/ExamSystem.Domain/Common/BaseAuditableEntity.cs`
- Create: `src/ExamSystem.Domain/GlobalUsings.cs`

- [ ] **Step 1: Write `BaseEntity`**

```csharp
namespace ExamSystem.Domain.Common;

/// <summary>Base type for all Domain entities; Id is client-generated (Guid) so it is available before persistence.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
```

- [ ] **Step 2: Write `BaseAuditableEntity`**

```csharp
namespace ExamSystem.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
}
```

- [ ] **Step 3: Write `GlobalUsings.cs`**

```csharp
global using ExamSystem.Domain.Common;
```

- [ ] **Step 4: Build the Domain project**

Run: `dotnet build src/ExamSystem.Domain/ExamSystem.Domain.csproj`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Domain
git commit -m "feat(domain): add base entity abstractions"
```

---

### Task 3: Application Layer — Result Model + Validation Pipeline Behavior (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Common/Models/Result.cs`
- Create: `src/ExamSystem.Application/Common/Behaviors/ValidationBehavior.cs`
- Create: `src/ExamSystem.Application/GlobalUsings.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Behaviors/ValidationBehaviorTests.cs`

- [ ] **Step 1: Write `Result<T>` (needed by the test below)**

```csharp
namespace ExamSystem.Application.Common.Models;

/// <summary>Represents the outcome of an operation without relying on exceptions for expected/business failures.</summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? value, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());

    public static Result<T> Failure(params string[] errors) => new(false, default, errors);

    public static Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToList());
}
```

- [ ] **Step 2: Write the failing tests for `ValidationBehavior`**

Create `tests/ExamSystem.Application.UnitTests/Behaviors/ValidationBehaviorTests.cs`:

```csharp
using ExamSystem.Application.Common.Behaviors;
using ExamSystem.Application.Common.Models;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using Xunit;

namespace ExamSystem.Application.UnitTests.Behaviors;

public record SampleRequest(string Name) : IRequest<Result<string>>;

public class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>(Enumerable.Empty<IValidator<SampleRequest>>());
        var nextCalled = false;

        var response = await behavior.Handle(new SampleRequest("x"), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task Handle_ValidationFails_ReturnsFailureResultWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>(new[] { new SampleRequestValidator() });
        var nextCalled = false;

        var response = await behavior.Handle(new SampleRequest(""), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("should not happen"));
        }, CancellationToken.None);

        Assert.False(nextCalled);
        Assert.False(response.IsSuccess);
        Assert.Contains("Name is required.", response.Errors);
    }

    [Fact]
    public async Task Handle_ValidationPasses_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>(new[] { new SampleRequestValidator() });

        var response = await behavior.Handle(new SampleRequest("Ali"), () => Task.FromResult(Result<string>.Success("ok")), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("ok", response.Value);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter ValidationBehaviorTests`
Expected: FAIL to compile — `ValidationBehavior<,>` does not exist yet.

- [ ] **Step 4: Implement `ValidationBehavior`**

```csharp
using FluentValidation.Results;

namespace ExamSystem.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => f.ErrorMessage)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failureMethod = responseType.GetMethod(
                nameof(Result<object>.Failure),
                new[] { typeof(IEnumerable<string>) });

            return (TResponse)failureMethod!.Invoke(null, new object[] { failures })!;
        }

        throw new ValidationException(failures.Select(f => new ValidationFailure(string.Empty, f)));
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter ValidationBehaviorTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 6: Write `GlobalUsings.cs`**

```csharp
global using MediatR;
global using FluentValidation;
global using Microsoft.Extensions.DependencyInjection;
global using ExamSystem.Application.Common.Models;
```

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add Result<T> model and validation pipeline behavior with tests"
```

---

### Task 4: Application Layer — Auth Interfaces + Login Command (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Common/Interfaces/IIdentityService.cs`
- Create: `src/ExamSystem.Application/Common/Interfaces/IJwtTokenGenerator.cs`
- Create: `src/ExamSystem.Application/Features/Auth/Login/LoginCommand.cs`
- Create: `src/ExamSystem.Application/Features/Auth/Login/LoginCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/Auth/Login/LoginCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Auth/LoginCommandHandlerTests.cs`

- [ ] **Step 1: Write the interfaces**

`src/ExamSystem.Application/Common/Interfaces/IIdentityService.cs`:
```csharp
namespace ExamSystem.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<IdentityValidationResult> ValidateCredentialsAsync(string userName, string password);
}

public record IdentityValidationResult(bool Succeeded, string? UserId, string? UserName, IReadOnlyList<string> Roles)
{
    public static IdentityValidationResult Failure() => new(false, null, null, Array.Empty<string>());
}
```

`src/ExamSystem.Application/Common/Interfaces/IJwtTokenGenerator.cs`:
```csharp
namespace ExamSystem.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string userName, IReadOnlyList<string> roles);
}
```

- [ ] **Step 2: Write the failing test for `LoginCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Auth/LoginCommandHandlerTests.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Auth.Login;
using Moq;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.Auth;

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithToken()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(s => s.ValidateCredentialsAsync("admin", "P@ssw0rd!"))
            .ReturnsAsync(new IdentityValidationResult(true, "user-1", "admin", new List<string> { "Admin" }));

        var tokenGenerator = new Mock<IJwtTokenGenerator>();
        tokenGenerator
            .Setup(g => g.GenerateToken("user-1", "admin", It.Is<IReadOnlyList<string>>(r => r.Contains("Admin"))))
            .Returns("fake-jwt-token");

        var handler = new LoginCommandHandler(identityService.Object, tokenGenerator.Object);

        var result = await handler.Handle(new LoginCommand("admin", "P@ssw0rd!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fake-jwt-token", result.Value!.Token);
        Assert.Equal("admin", result.Value.UserName);
    }

    [Fact]
    public async Task Handle_InvalidCredentials_ReturnsFailure()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityValidationResult.Failure());

        var tokenGenerator = new Mock<IJwtTokenGenerator>();

        var handler = new LoginCommandHandler(identityService.Object, tokenGenerator.Object);

        var result = await handler.Handle(new LoginCommand("admin", "wrong"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid username or password.", result.Errors);
        tokenGenerator.Verify(g => g.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter LoginCommandHandlerTests`
Expected: FAIL to compile — `LoginCommand`, `LoginCommandHandler` do not exist yet.

- [ ] **Step 4: Implement `LoginCommand`, validator, and handler**

`src/ExamSystem.Application/Features/Auth/Login/LoginCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Auth.Login;

public record LoginCommand(string UserName, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(string Token, string UserName, IReadOnlyList<string> Roles);
```

`src/ExamSystem.Application/Features/Auth/Login/LoginCommandValidator.cs`:
```csharp
namespace ExamSystem.Application.Features.Auth.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Username is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}
```

`src/ExamSystem.Application/Features/Auth/Login/LoginCommandHandler.cs`:
```csharp
using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginCommandHandler(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _identityService.ValidateCredentialsAsync(request.UserName, request.Password);

        if (!validation.Succeeded || validation.UserId is null || validation.UserName is null)
        {
            return Result<LoginResponse>.Failure("Invalid username or password.");
        }

        var token = _jwtTokenGenerator.GenerateToken(validation.UserId, validation.UserName, validation.Roles);
        return Result<LoginResponse>.Success(new LoginResponse(token, validation.UserName, validation.Roles));
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter LoginCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 6: Write `DependencyInjection.cs` for the Application layer**

```csharp
namespace ExamSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Common.Behaviors.ValidationBehavior<,>));
        return services;
    }
}
```

- [ ] **Step 7: Run the full Application test suite**

Run: `dotnet test tests/ExamSystem.Application.UnitTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add Login CQRS command with handler tests"
```

---

### Task 5: Infrastructure Layer — Identity, DbContext, JWT Generator, Seeder

**Files:**
- Create: `src/ExamSystem.Infrastructure/Identity/ApplicationUser.cs`
- Create: `src/ExamSystem.Infrastructure/Identity/JwtSettings.cs`
- Create: `src/ExamSystem.Infrastructure/Identity/JwtTokenGenerator.cs`
- Create: `src/ExamSystem.Infrastructure/Identity/IdentityService.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/DbInitializer.cs`
- Create: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Create: `src/ExamSystem.Infrastructure/GlobalUsings.cs`

- [ ] **Step 1: Write `ApplicationUser`**

```csharp
namespace ExamSystem.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Write `JwtSettings`**

```csharp
namespace ExamSystem.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
```

- [ ] **Step 3: Write `JwtTokenGenerator`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExamSystem.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExamSystem.Infrastructure.Identity;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public string GenerateToken(string userId, string userName, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 4: Write `IdentityService`**

```csharp
using ExamSystem.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ExamSystem.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IdentityService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<IdentityValidationResult> ValidateCredentialsAsync(string userName, string password)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return IdentityValidationResult.Failure();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return IdentityValidationResult.Failure();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new IdentityValidationResult(true, user.Id, user.UserName, roles.ToList());
    }
}
```

- [ ] **Step 5: Write `ApplicationDbContext`**

```csharp
using ExamSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
}
```

- [ ] **Step 6: Write `DbInitializer`**

```csharp
using ExamSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamSystem.Infrastructure.Persistence;

public static class DbInitializer
{
    public const string AdminRole = "Admin";

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
    }
}
```

- [ ] **Step 7: Write `DependencyInjection.cs`**

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Infrastructure.Identity;
using ExamSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
```

- [ ] **Step 8: Write `GlobalUsings.cs`**

```csharp
global using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 9: Build the Infrastructure project**

Run: `dotnet build src/ExamSystem.Infrastructure/ExamSystem.Infrastructure.csproj`
Expected: `Build succeeded.`

- [ ] **Step 10: Commit**

```bash
git add src/ExamSystem.Infrastructure
git commit -m "feat(infrastructure): add Identity, EF Core DbContext, JWT generator, admin seeder"
```

---

### Task 6: API Layer — Composition Root, Auth Endpoint, Health Check

**Files:**
- Modify: `src/ExamSystem.Api/Program.cs`
- Create: `src/ExamSystem.Api/Controllers/AuthController.cs`
- Modify: `src/ExamSystem.Api/appsettings.json`
- Modify: `src/ExamSystem.Api/appsettings.Development.json`

- [ ] **Step 1: Replace `Program.cs`**

```csharp
using System.Text;
using ExamSystem.Application;
using ExamSystem.Infrastructure;
using ExamSystem.Infrastructure.Identity;
using ExamSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
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
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAdminAsync(scope.ServiceProvider);
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow }))
   .AllowAnonymous();

app.Run();

public partial class Program { }
```

- [ ] **Step 2: Write `AuthController`**

```csharp
using ExamSystem.Application.Features.Auth.Login;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Handles Admin authentication.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Authenticates an Admin user and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
```

- [ ] **Step 3: Replace `appsettings.json` (no secrets committed)**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedOrigins": "http://localhost:4200",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "Issuer": "ExamSystem",
    "Audience": "ExamSystemClients",
    "ExpiryMinutes": 60
  },
  "SeedAdmin": {
    "UserName": "admin",
    "Email": "admin@examsystem.local"
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 4: Leave `appsettings.Development.json` empty of secrets**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

- [ ] **Step 5: Configure local secrets (never commit these)**

Run:
```bash
dotnet user-secrets init --project src/ExamSystem.Api/ExamSystem.Api.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=ExamSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true" --project src/ExamSystem.Api/ExamSystem.Api.csproj
dotnet user-secrets set "Jwt:Key" "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS" --project src/ExamSystem.Api/ExamSystem.Api.csproj
dotnet user-secrets set "SeedAdmin:Password" "ChangeMe!2026" --project src/ExamSystem.Api/ExamSystem.Api.csproj
```
Expected: each prints "Successfully saved ... to the secret store."

- [ ] **Step 6: Build the API project**

Run: `dotnet build src/ExamSystem.Api/ExamSystem.Api.csproj`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Api
git commit -m "feat(api): wire composition root, JWT auth, auth controller, health endpoint"
```

*(Note: `dotnet user-secrets` writes to a per-user file outside the repo — nothing from Step 5 is committed.)*

---

### Task 7: EF Core Migration + Local DB Verification

**Files:**
- Create: `src/ExamSystem.Infrastructure/Migrations/*` (generated)

- [ ] **Step 1: Install/verify the `dotnet-ef` tool**

Run: `dotnet tool install -g dotnet-ef` (if already installed, run `dotnet tool update -g dotnet-ef` instead)
Expected: prints the installed/updated version.

- [ ] **Step 2: Generate the initial migration**

Run (from repo root):
```bash
dotnet ef migrations add InitialIdentitySchema --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: "Done." and new files under `src/ExamSystem.Infrastructure/Migrations/`.

- [ ] **Step 3: Apply the migration to LocalDB**

Run:
```bash
dotnet ef database update --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: "Done." — `ExamSystemDb` created on `(localdb)\mssqllocaldb`.

- [ ] **Step 4: Run the API and verify seeding + health check**

Run: `dotnet run --project src/ExamSystem.Api` (leave running)
In a second terminal: `curl http://localhost:5000/health` (adjust port to whatever `dotnet run` printed)
Expected: `{"status":"healthy","timestampUtc":"..."}`

Stop the API (Ctrl+C) once confirmed.

- [ ] **Step 5: Commit the migration**

```bash
git add src/ExamSystem.Infrastructure/Migrations
git commit -m "chore(db): add initial Identity schema migration"
```

---

### Task 8: API Integration Test — Login Round Trip (TDD, SQLite in-memory)

**Files:**
- Create: `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs`
- Create: `tests/ExamSystem.Api.IntegrationTests/Controllers/AuthControllerTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/AuthControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using ExamSystem.Application.Features.Auth.Login;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsOkWithToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", TestWebApplicationFactory.SeedAdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Contains("Admin", body.Roles);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", "totally-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Write `TestWebApplicationFactory`**

Create `tests/ExamSystem.Api.IntegrationTests/TestWebApplicationFactory.cs`:

```csharp
using ExamSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExamSystem.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SeedAdminPassword = "Test-P@ssw0rd!1";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-signing-key-please-ignore-32chars",
                ["Jwt:Issuer"] = "ExamSystem.Tests",
                ["Jwt:Audience"] = "ExamSystem.Tests.Clients",
                ["SeedAdmin:UserName"] = "admin",
                ["SeedAdmin:Password"] = SeedAdminPassword,
                ["SeedAdmin:Email"] = "admin@examsystem.local"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

- [ ] **Step 3: Ensure the test host creates the SQLite schema on startup**

Modify `src/ExamSystem.Api/Program.cs` migrate/seed block so it also works against SQLite in tests — replace:
```csharp
    await db.Database.MigrateAsync();
```
with:
```csharp
    if (db.Database.IsSqlServer())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
```
(SQLite is used only in the integration-test host; production/dev continues to run real migrations against SQL Server.)

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests`
Expected: FAIL — either compile error (before Step 2/3 land) or a runtime failure if the SQLite branch isn't wired yet. Confirm the specific failure is about missing schema/DB wiring, not an unrelated error.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 6: Run the entire backend test suite together**

Run: `dotnet test`
Expected: all projects report `Passed!`, 0 failures.

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Api/Program.cs tests/ExamSystem.Api.IntegrationTests
git commit -m "test(api): add SQLite-backed integration tests for the login endpoint"
```

---

### Task 9: Angular Workspace Scaffolding + RTL/PrimeNG Theme

**Files:**
- Create: `frontend/` (full Angular workspace)
- Create: `frontend/src/styles/variables.scss`
- Modify: `frontend/src/index.html`
- Modify: `frontend/src/styles.scss`

- [ ] **Step 1: Generate the Angular workspace**

Run (from `D:/os/ExamSystem`):
```bash
npx -y @angular/cli@17 new frontend --directory frontend --standalone --routing --style=scss --skip-git --package-manager=npm
```
Expected: "✔ Packages installed successfully." and a `frontend/` folder created.

- [ ] **Step 2: Install PrimeNG + CDK**

Run:
```bash
cd frontend
npm install primeng@17 primeicons @angular/cdk@17
cd ..
```
Expected: `added N packages` with no vulnerabilities marked `critical`.

- [ ] **Step 3: Create design tokens file**

Create `frontend/src/styles/variables.scss`:
```scss
:root {
  --primary: #1a56db;
  --surface: #f9fafb;
  --card: #ffffff;
  --border: #e5e7eb;
  --text-main: #111827;
  --text-muted: #6b7280;
  --error: #ef4444;
  --success: #10b981;
  --warning: #f59e0b;

  --space-xs: 4px;
  --space-sm: 8px;
  --space-md: 16px;
  --space-lg: 24px;
  --space-xl: 32px;
  --space-2xl: 48px;
}
```

- [ ] **Step 4: Wire global styles (RTL-ready) and PrimeNG theme**

Replace `frontend/src/styles.scss`:
```scss
@import "styles/variables";
@import "primeng/resources/themes/lara-light-blue/theme.css";
@import "primeng/resources/primeng.css";
@import "primeicons/primeicons.css";

html, body {
  height: 100%;
  margin: 0;
  font-family: "Segoe UI", Tahoma, Arial, sans-serif;
  background: var(--surface);
  color: var(--text-main);
}
```

- [ ] **Step 5: Set the document direction to RTL**

Modify `frontend/src/index.html` `<html>` tag:
```html
<html lang="ar" dir="rtl">
```

- [ ] **Step 6: Verify the workspace builds**

Run: `cd frontend && npm run build -- --configuration development && cd ..`
Expected: `Application bundle generation complete.` with no errors.

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "chore(frontend): scaffold Angular 17 standalone workspace with RTL + PrimeNG theme"
```

---

> **Carried forward from Task 9's code review — read before starting Task 10/11:**
> 1. PrimeNG 17's `lara-light-blue` theme ships **zero RTL CSS** (`grep -c '\[dir=' node_modules/primeng/resources/themes/lara-light-blue/theme.css` → `0`). Components with icons/overlays anchored via `left`/`right` (dropdowns, calendars, input groups, menus) will visually misalign under `dir="rtl"` unless an RTL override stylesheet is added. Budget for this when building the login form/admin layout — don't assume `dir="rtl"` alone is sufficient.
> 2. Production initial bundle is already at 406.63 kB against `angular.json`'s 500kb warning budget (dev-mode build is 1.45MB, over the 1mb *error* budget, but budgets only apply to the production config by default). Watch this as more PrimeNG components get imported in Tasks 10-11 — there's limited headroom left.
> 3. `styles.scss`'s font stack (`"Segoe UI", Tahoma, Arial, sans-serif`) has no Arabic web-font fallback (e.g. Noto Sans Arabic/Cairo) and no `@font-face`/Google Fonts import — non-Windows clients will fall back to generic sans-serif for Arabic glyphs. Worth picking a proper Arabic font pairing before the login page (first user-visible screen) ships.

### Task 10: Angular Core — Auth Service, Interceptor, Guard (TDD)

**Files:**
- Create: `frontend/src/app/core/services/auth.service.ts`
- Test: `frontend/src/app/core/services/auth.service.spec.ts`
- Create: `frontend/src/app/core/interceptors/auth.interceptor.ts`
- Test: `frontend/src/app/core/interceptors/auth.interceptor.spec.ts`
- Create: `frontend/src/app/core/guards/auth.guard.ts`
- Test: `frontend/src/app/core/guards/auth.guard.spec.ts`
- Modify: `frontend/src/environments/environment.ts` (create if template didn't generate it)

- [ ] **Step 1: Add environment config**

Create `frontend/src/environments/environment.ts`:
```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000/api'
};
```

- [ ] **Step 2: Write the failing test for `AuthService`**

Create `frontend/src/app/core/services/auth.service.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService, LoginResponse } from './auth.service';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('logs in, stores the token, and reports authenticated state', () => {
    const mockResponse: LoginResponse = { token: 'jwt-token', userName: 'admin', roles: ['Admin'] };

    service.login('admin', 'secret').subscribe(response => {
      expect(response.token).toBe('jwt-token');
      expect(service.isAuthenticated()).toBeTrue();
      expect(localStorage.getItem('auth_token')).toBe('jwt-token');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('reports not authenticated when no token is stored', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('logout clears the stored token', () => {
    localStorage.setItem('auth_token', 'jwt-token');
    service.logout();
    expect(localStorage.getItem('auth_token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });
});
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/auth.service.spec.ts'`
Expected: FAIL — `auth.service.ts` does not exist yet.

- [ ] **Step 4: Implement `AuthService`**

Create `frontend/src/app/core/services/auth.service.ts`:
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  token: string;
  userName: string;
  roles: string[];
}

const TOKEN_KEY = 'auth_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly http: HttpClient) {}

  login(userName: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiBaseUrl}/auth/login`, { userName, password })
      .pipe(tap(response => localStorage.setItem(TOKEN_KEY, response.token)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/auth.service.spec.ts'`
Expected: `TOTAL: 3 SUCCESS`

- [ ] **Step 6: Write the failing test for the auth interceptor**

Create `frontend/src/app/core/interceptors/auth.interceptor.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('adds an Authorization header when a token exists', () => {
    spyOn(authService, 'getToken').and.returnValue('jwt-token');

    httpClient.get('/api/anything').subscribe();

    const req = httpMock.expectOne('/api/anything');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush({});
  });

  it('does not add an Authorization header when no token exists', () => {
    spyOn(authService, 'getToken').and.returnValue(null);

    httpClient.get('/api/anything').subscribe();

    const req = httpMock.expectOne('/api/anything');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/auth.interceptor.spec.ts'`
Expected: FAIL — `auth.interceptor.ts` does not exist yet.

- [ ] **Step 8: Implement `authInterceptor`**

Create `frontend/src/app/core/interceptors/auth.interceptor.ts`:
```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  if (!token) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
```

- [ ] **Step 9: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/auth.interceptor.spec.ts'`
Expected: `TOTAL: 2 SUCCESS`

- [ ] **Step 10: Write the failing test for `authGuard`**

Create `frontend/src/app/core/guards/auth.guard.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    authService = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    router = jasmine.createSpyObj('Router', ['createUrlTree']);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('allows navigation when authenticated', () => {
    authService.isAuthenticated.and.returnValue(true);

    const result = TestBed.runInInjectionContext(() => authGuard());

    expect(result).toBeTrue();
  });

  it('redirects to login when not authenticated', () => {
    authService.isAuthenticated.and.returnValue(false);
    const fakeTree = {} as any;
    router.createUrlTree.and.returnValue(fakeTree);

    const result = TestBed.runInInjectionContext(() => authGuard());

    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    expect(result).toBe(fakeTree);
  });
});
```

- [ ] **Step 11: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/auth.guard.spec.ts'`
Expected: FAIL — `auth.guard.ts` does not exist yet.

- [ ] **Step 12: Implement `authGuard`**

Create `frontend/src/app/core/guards/auth.guard.ts`:
```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};
```

- [ ] **Step 13: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/auth.guard.spec.ts'`
Expected: `TOTAL: 2 SUCCESS`

- [ ] **Step 14: Commit**

```bash
git add frontend/src/app/core frontend/src/environments
git commit -m "feat(frontend): add auth service, interceptor, and guard with tests"
```

---

### Task 11: Angular Login Page + Admin Layout + Routing

**Files:**
- Create: `frontend/src/app/features/auth/login/login.component.ts`
- Create: `frontend/src/app/features/auth/login/login.component.html`
- Test: `frontend/src/app/features/auth/login/login.component.spec.ts`
- Create: `frontend/src/app/layouts/admin-layout/admin-layout.component.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.config.ts`

- [ ] **Step 1: Write the failing test for `LoginComponent`**

Create `frontend/src/app/features/auth/login/login.component.spec.ts`:
```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService, LoginResponse } from '../../../core/services/auth.service';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj('AuthService', ['login']);
    router = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [LoginComponent, ReactiveFormsModule],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('navigates to /admin on successful login', () => {
    const response: LoginResponse = { token: 't', userName: 'admin', roles: ['Admin'] };
    authService.login.and.returnValue(of(response));
    component.form.setValue({ userName: 'admin', password: 'secret' });

    component.submit();

    expect(authService.login).toHaveBeenCalledWith('admin', 'secret');
    expect(router.navigate).toHaveBeenCalledWith(['/admin']);
    expect(component.errorMessage).toBeNull();
  });

  it('shows an error message on failed login', () => {
    authService.login.and.returnValue(throwError(() => ({ status: 401 })));
    component.form.setValue({ userName: 'admin', password: 'wrong' });

    component.submit();

    expect(component.errorMessage).toBe('اسم المستخدم أو كلمة المرور غير صحيحة.');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/login.component.spec.ts'`
Expected: FAIL — `login.component.ts` does not exist yet.

- [ ] **Step 3: Implement `LoginComponent`**

Create `frontend/src/app/features/auth/login/login.component.ts`:
```typescript
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  errorMessage: string | null = null;
  loading = false;

  readonly form = this.fb.group({
    userName: ['', Validators.required],
    password: ['', Validators.required]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.errorMessage = null;
    this.loading = true;
    const { userName, password } = this.form.getRawValue();

    this.authService.login(userName!, password!).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/admin']);
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'اسم المستخدم أو كلمة المرور غير صحيحة.';
      }
    });
  }
}
```

Create `frontend/src/app/features/auth/login/login.component.html`:
```html
<div class="login-page">
  <form [formGroup]="form" (ngSubmit)="submit()" class="login-card">
    <h1>تسجيل دخول الأدمن</h1>

    <label for="userName">اسم المستخدم</label>
    <input id="userName" type="text" formControlName="userName" />

    <label for="password">كلمة المرور</label>
    <input id="password" type="password" formControlName="password" />

    <p class="error" *ngIf="errorMessage">{{ errorMessage }}</p>

    <button type="submit" [disabled]="form.invalid || loading">
      {{ loading ? 'جاري الدخول...' : 'دخول' }}
    </button>
  </form>
</div>
```

*(Note: `*ngIf` requires `CommonModule` in the standalone `imports` array — add it.)*

- [ ] **Step 4: Add `CommonModule` to the component imports**

Modify `frontend/src/app/features/auth/login/login.component.ts` imports array:
```typescript
import { CommonModule } from '@angular/common';
// ...
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  templateUrl: './login.component.html'
})
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/login.component.spec.ts'`
Expected: `TOTAL: 2 SUCCESS`

- [ ] **Step 6: Create a minimal Admin layout shell**

Create `frontend/src/app/layouts/admin-layout/admin-layout.component.ts`:
```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="admin-shell">
      <header class="admin-header">نظام الامتحانات — لوحة التحكم</header>
      <main class="admin-content">
        <router-outlet />
      </main>
    </div>
  `
})
export class AdminLayoutComponent {}
```

- [ ] **Step 7: Wire routes**

Replace `frontend/src/app/app.routes.ts`:
```typescript
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'admin',
    canActivate: [authGuard],
    loadComponent: () => import('./layouts/admin-layout/admin-layout.component').then(m => m.AdminLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/admin/dashboard/dashboard-placeholder.component').then(m => m.DashboardPlaceholderComponent)
      }
    ]
  }
];
```

- [ ] **Step 8: Add the dashboard placeholder referenced above**

Create `frontend/src/app/features/admin/dashboard/dashboard-placeholder.component.ts`:
```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-dashboard-placeholder',
  standalone: true,
  template: `<p>تم تسجيل الدخول بنجاح — لوحة التحكم الكاملة تُبنى في Phase 1/3.</p>`
})
export class DashboardPlaceholderComponent {}
```

- [ ] **Step 9: Register the HTTP client with the auth interceptor**

Modify `frontend/src/app/app.config.ts` to include:
```typescript
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor]))
  ]
};
```

- [ ] **Step 10: Run the full frontend test suite**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: all suites `SUCCESS`, 0 failures.

- [ ] **Step 11: Verify the production build still compiles**

Run: `cd frontend && npm run build -- --configuration production && cd ..`
Expected: `Application bundle generation complete.`

- [ ] **Step 12: Commit**

```bash
git add frontend/src/app
git commit -m "feat(frontend): add login page, admin layout shell, and routing"
```

---

### Task 12: End-to-End Manual Verification (Phase 0 Deliverable)

**Files:** none (manual verification only)

- [ ] **Step 1: Start the backend API**

Run: `dotnet run --project src/ExamSystem.Api`
Expected: log line `Now listening on: http://localhost:5000` (or similar) with no unhandled startup exceptions; admin user seeded on first run (verify via Step 3 login).

- [ ] **Step 2: Start the Angular dev server**

In a second terminal: `cd frontend && npx ng serve --port 4200`
Expected: `✔ Compiled successfully.`

- [ ] **Step 3: Log in as the seeded Admin through the browser**

Open `http://localhost:4200/login`, enter username `admin` and the password set in Task 6 Step 5 (`ChangeMe!2026` if unchanged), submit.
Expected: redirect to `/admin/dashboard` showing "تم تسجيل الدخول بنجاح..."; `localStorage` contains a Bearer JWT under key `auth_token` (verify via browser DevTools → Application → Local Storage).

- [ ] **Step 4: Verify a bad login is rejected**

Log out (clear `auth_token` from Local Storage or reload after clearing), try logging in with a wrong password.
Expected: inline error "اسم المستخدم أو كلمة المرور غير صحيحة." and no navigation away from `/login`.

- [ ] **Step 5: Verify the health endpoint**

Run: `curl http://localhost:5000/health`
Expected: `{"status":"healthy","timestampUtc":"..."}`

- [ ] **Step 6: Stop both dev servers**

Ctrl+C in both terminals.

- [ ] **Step 7: Record the checkpoint**

Update this plan file: check off Task 12's steps and add a one-line note at the bottom of the file: `Phase 0 completed on <date> — backend + frontend tests green, manual E2E login verified.`

- [ ] **Step 8: Commit**

```bash
git add docs/superpowers/plans/2026-07-04-phase0-foundation.md
git commit -m "docs: mark Phase 0 foundation plan complete"
```

---

## Definition of Done (Phase 0)

- [ ] `dotnet build` succeeds for the whole solution.
- [ ] `dotnet test` passes for both backend test projects (unit + integration).
- [ ] `ng test` passes for the Angular workspace.
- [ ] `ng build --configuration production` succeeds.
- [ ] A seeded Admin can log in through the real UI and land on `/admin/dashboard` with a JWT stored client-side.
- [ ] No secrets (connection strings, JWT key, seed password) are present in any committed file — only in `dotnet user-secrets` / local environment.
- [ ] `/health` returns 200 with a JSON payload.

## Out of Scope for This Plan (deferred to later phases per PRD §7)

- Topics/Questions CRUD, bulk Excel import (Phase 1).
- Exam configuration, publish validation, Topic×Difficulty matrix (Phase 1).
- Candidate registration, National ID validation, Batch Gate/Waiting Room, exam engine, snapshotting, auto-grading (Phase 2).
- Reporting/Dashboard, exports, DailyExamStats aggregation job (Phase 3).
- Live Monitoring/SignalR, Anti-Cheating instrumentation, Audit Log, k6 performance pipeline, actual Free-Tier deployment (Phase 4).

Each of these should get its own plan document following this same skill before implementation begins, per the PRD's phase breakdown.

---

Phase 0 completed on 2026-07-05 — backend (8/8 tests) and frontend (11/11 tests) green, manual E2E login verified against real LocalDB + running Angular dev server. A real layout bug (leftover Angular CLI boilerplate pushing the login submit button off-screen) was discovered and fixed during manual verification — see commit `c1b9ed9`.
