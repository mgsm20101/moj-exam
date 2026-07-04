using Microsoft.AspNetCore.Identity;

namespace ExamSystem.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
