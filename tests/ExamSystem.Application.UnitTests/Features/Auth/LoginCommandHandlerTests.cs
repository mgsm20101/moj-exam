using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Auth.Login;
using Moq;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.Auth;

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithTokenAndRefreshToken()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(s => s.ValidateCredentialsAsync("admin", "P@ssw0rd!"))
            .ReturnsAsync(new IdentityValidationResult(true, "user-1", "admin", new List<string> { "Admin" }));

        var tokenGenerator = new Mock<IJwtTokenGenerator>();
        tokenGenerator
            .Setup(g => g.GenerateToken("user-1", "admin", It.Is<IReadOnlyList<string>>(r => r.Contains("Admin"))))
            .Returns("fake-jwt-token");

        var refreshTokens = new Mock<IRefreshTokenService>();
        refreshTokens
            .Setup(s => s.IssueAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fake-refresh-token");

        var handler = new LoginCommandHandler(identityService.Object, tokenGenerator.Object, refreshTokens.Object);

        var result = await handler.Handle(new LoginCommand("admin", "P@ssw0rd!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fake-jwt-token", result.Value!.Token);
        Assert.Equal("fake-refresh-token", result.Value.RefreshToken);
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
        var refreshTokens = new Mock<IRefreshTokenService>();

        var handler = new LoginCommandHandler(identityService.Object, tokenGenerator.Object, refreshTokens.Object);

        var result = await handler.Handle(new LoginCommand("admin", "wrong"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid username or password.", result.Errors);
        tokenGenerator.Verify(g => g.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
        refreshTokens.Verify(s => s.IssueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
