using Hmis.Modules.Identity.Application.Models;
using Hmis.Modules.Identity.Domain.Entities;

namespace Hmis.Modules.Identity.Application.Interfaces.Services;

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheckResult Verify(User user, string hashedPassword, string providedPassword);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<string> roles);

    GeneratedRefreshToken GenerateRefreshToken();

    string HashRefreshToken(string token);

    TimeSpan RefreshTokenLifetime { get; }
}
