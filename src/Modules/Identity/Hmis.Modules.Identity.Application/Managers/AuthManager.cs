using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Interfaces.Services;
using Hmis.Modules.Identity.Application.Models;
using Hmis.Modules.Identity.Application.Options;
using Hmis.Modules.Identity.Domain.Constants;
using Hmis.Modules.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Hmis.Modules.Identity.Application.Managers;

public sealed class AuthManager(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ILoginHistoryRepository loginHistoryRepository,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    IRequestContext requestContext,
    IDateTimeProvider dateTime,
    IOptions<AccountSecurityOptions> securityOptions,
    IAuditService auditService) : IAuthManager
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    private readonly AccountSecurityOptions _security = securityOptions.Value;

    public async Task<AuthSessionDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var now = dateTime.UtcNow;
        var user = await userRepository.GetByNormalizedUserNameOrEmailAsync(User.Normalize(request.UserNameOrEmail), cancellationToken);

        if (user is null)
        {
            await RecordAttemptAsync(null, request.UserNameOrEmail, LoginFailureReasons.UnknownUser, cancellationToken);
            throw new UnauthorizedException(InvalidCredentialsMessage, IdentityErrorCodes.InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            await RecordAttemptAsync(user.Id, request.UserNameOrEmail, LoginFailureReasons.LockedOut, cancellationToken);
            throw LockedOut(user, now);
        }

        var passwordResult = passwordHasher.Verify(user, user.PasswordHash, request.Password);
        if (passwordResult == PasswordCheckResult.Failed)
        {
            user.RecordFailedLogin(now, _security.MaxFailedAccessAttempts, TimeSpan.FromMinutes(_security.LockoutMinutes));
            await RecordAttemptAsync(user.Id, request.UserNameOrEmail, LoginFailureReasons.InvalidPassword, cancellationToken);

            throw user.IsLockedOut(now)
                ? LockedOut(user, now)
                : new UnauthorizedException(InvalidCredentialsMessage, IdentityErrorCodes.InvalidCredentials);
        }

        // Account state is revealed only after the password is proven.
        if (!user.IsActive)
        {
            await RecordAttemptAsync(user.Id, request.UserNameOrEmail, LoginFailureReasons.Inactive, cancellationToken);
            throw new UnauthorizedException("Your account is inactive. Contact the system administrator.", IdentityErrorCodes.AccountInactive);
        }

        if (passwordResult == PasswordCheckResult.SuccessRehashNeeded)
        {
            user.SetPassword(passwordHasher.Hash(user, request.Password), user.MustChangePassword);
        }

        user.RecordSuccessfulLogin(now);
        await loginHistoryRepository.AddAsync(
            LoginHistory.Record(user.Id, request.UserNameOrEmail, true, null, requestContext.IpAddress, requestContext.UserAgent, now),
            cancellationToken);

        var session = await IssueSessionAsync(user, Guid.CreateVersion7(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    public async Task<AuthSessionDto> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw InvalidRefreshToken();
        }

        var now = dateTime.UtcNow;
        var stored = await refreshTokenRepository.GetByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (stored is null || stored.IsExpired(now))
        {
            throw InvalidRefreshToken();
        }

        if (stored.IsRevoked)
        {
            // A rotated token was presented again: assume theft and end the whole session family.
            await RevokeFamilyAsync(stored.FamilyId, now, "Reuse detected", cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.IsLockedOut(now))
        {
            stored.Revoke(now, "Account unavailable");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        var session = await IssueSessionAsync(user, stored.FamilyId, cancellationToken);
        stored.Revoke(now, "Rotated", session.RefreshTokenId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var stored = await refreshTokenRepository.GetByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (stored is null)
        {
            return;
        }

        await RevokeFamilyAsync(stored.FamilyId, dateTime.UtcNow, "Logout", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthSessionDto> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        var user = await GetActiveCurrentUserAsync(cancellationToken);

        if (passwordHasher.Verify(user, user.PasswordHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            throw new BusinessException(IdentityErrorCodes.InvalidCurrentPassword, "The current password is incorrect.", "currentPassword");
        }

        if (passwordHasher.Verify(user, user.PasswordHash, request.NewPassword) != PasswordCheckResult.Failed)
        {
            throw new BusinessException(IdentityErrorCodes.PasswordReused, "The new password must be different from the current password.", "newPassword");
        }

        var now = dateTime.UtcNow;
        user.SetPassword(passwordHasher.Hash(user, request.NewPassword), mustChangePassword: false);
        auditService.Record(new AuditEvent("PasswordChanged", "Identity", nameof(User), user.Id.ToString()));

        // Sign out every other session; the caller receives a fresh one.
        foreach (var token in await refreshTokenRepository.GetActiveByUserAsync(user.Id, now, cancellationToken))
        {
            token.Revoke(now, "Password changed");
        }

        var session = await IssueSessionAsync(user, Guid.CreateVersion7(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await GetActiveCurrentUserAsync(cancellationToken);
        return await BuildCurrentUserAsync(user, cancellationToken);
    }

    private async Task<User> GetActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);

        return user is { IsActive: true } ? user : throw new UnauthorizedException();
    }

    private async Task<(AuthSessionDto Result, Guid RefreshTokenId)> IssueSessionAsync(
        User user,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var currentUserDto = await BuildCurrentUserAsync(user, cancellationToken);
        var accessToken = tokenService.CreateAccessToken(user, currentUserDto.Roles);
        var generated = tokenService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Issue(
            user.Id, generated.TokenHash, familyId, dateTime.UtcNow, tokenService.RefreshTokenLifetime, requestContext.IpAddress);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        var response = new AuthResponseDto(accessToken.Token, accessToken.ExpiresAt, currentUserDto);
        return (new AuthSessionDto(response, generated.Token, refreshToken.ExpiresAt), refreshToken.Id);
    }

    private async Task<CurrentUserDto> BuildCurrentUserAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await userRepository.GetRoleNamesAsync(user.Id, cancellationToken);

        // Until the password is changed the user holds no permissions (enforced again by PermissionChecker).
        var permissions = user.MustChangePassword
            ? []
            : (await userRepository.GetAccessSnapshotAsync(user.Id, cancellationToken))?.PermissionCodes.Order().ToList() ?? [];

        return new CurrentUserDto(user.Id, user.UserName, user.FullName, user.Email, roles, permissions, user.MustChangePassword);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, string reason, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokenRepository.GetActiveByFamilyAsync(familyId, now, cancellationToken))
        {
            token.Revoke(now, reason);
        }
    }

    private async Task RecordAttemptAsync(Guid? userId, string userName, string failureReason, CancellationToken cancellationToken)
    {
        await loginHistoryRepository.AddAsync(
            LoginHistory.Record(userId, userName, false, failureReason, requestContext.IpAddress, requestContext.UserAgent, dateTime.UtcNow),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static UnauthorizedException LockedOut(User user, DateTime now)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((user.LockoutEnd!.Value - now).TotalMinutes));
        return new UnauthorizedException(
            $"Account is locked due to repeated failed sign-in attempts. Try again in {minutes} minute(s).",
            IdentityErrorCodes.AccountLocked);
    }

    private static UnauthorizedException InvalidRefreshToken() =>
        new("Your session has expired. Please sign in again.", IdentityErrorCodes.InvalidRefreshToken);
}
