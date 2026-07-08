using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Auth.Refresh;
using Moq;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.Auth;

public class RefreshCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidRefreshToken_ReturnsNewTokenPair()
    {
        var refreshTokens = new Mock<IRefreshTokenService>();
        refreshTokens
            .Setup(s => s.RotateAsync("old-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshRotationResult(true, "user-1", "new-refresh"));

        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(s => s.GetUserInfoAsync("user-1"))
            .ReturnsAsync(new IdentityUserInfo("user-1", "admin", new List<string> { "Admin" }));

        var tokenGenerator = new Mock<IJwtTokenGenerator>();
        tokenGenerator
            .Setup(g => g.GenerateToken("user-1", "admin", It.IsAny<IReadOnlyList<string>>()))
            .Returns("new-access-token");

        var handler = new RefreshCommandHandler(refreshTokens.Object, identityService.Object, tokenGenerator.Object);

        var result = await handler.Handle(new RefreshCommand("old-refresh"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token", result.Value!.Token);
        Assert.Equal("new-refresh", result.Value.RefreshToken);
        Assert.Equal("admin", result.Value.UserName);
    }

    [Fact]
    public async Task Handle_InvalidRefreshToken_ReturnsFailureAndNeverMintsAccessToken()
    {
        var refreshTokens = new Mock<IRefreshTokenService>();
        refreshTokens
            .Setup(s => s.RotateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshRotationResult.Failure());

        var identityService = new Mock<IIdentityService>();
        var tokenGenerator = new Mock<IJwtTokenGenerator>();

        var handler = new RefreshCommandHandler(refreshTokens.Object, identityService.Object, tokenGenerator.Object);

        var result = await handler.Handle(new RefreshCommand("bogus"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        tokenGenerator.Verify(
            g => g.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()),
            Times.Never);
    }
}
