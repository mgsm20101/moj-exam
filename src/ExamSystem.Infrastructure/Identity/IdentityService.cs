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
