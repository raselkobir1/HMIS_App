using Hmis.Modules.Identity.Application.Interfaces.Services;
using Hmis.Modules.Identity.Application.Models;
using Hmis.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Hmis.Modules.Identity.Infrastructure.Services;

/// <summary>ASP.NET Core Identity's PBKDF2 password hasher (decision D9).</summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordCheckResult Verify(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return PasswordCheckResult.Failed;
        }

        return _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordCheckResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
            _ => PasswordCheckResult.Failed
        };
    }
}
