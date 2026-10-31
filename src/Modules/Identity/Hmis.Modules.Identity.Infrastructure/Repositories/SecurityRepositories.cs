using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Domain.Entities;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Repositories;

public sealed class PermissionRepository(IdentityDbContext context)
    : BaseRepository<Permission, IdentityDbContext>(context), IPermissionRepository
{
    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken) =>
        await Set.OrderBy(p => p.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
}

public sealed class RefreshTokenRepository(IdentityDbContext context) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken) =>
        await context.RefreshTokens.AddAsync(token, cancellationToken);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken cancellationToken) =>
        await context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null && t.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);
}

public sealed class LoginHistoryRepository(IdentityDbContext context) : ILoginHistoryRepository
{
    public async Task AddAsync(LoginHistory entry, CancellationToken cancellationToken) =>
        await context.LoginHistory.AddAsync(entry, cancellationToken);

    public async Task<PagedResponse<LoginHistoryDto>> SearchAsync(LoginHistoryQuery query, CancellationToken cancellationToken)
    {
        var entries = context.LoginHistory.AsNoTracking();

        if (query.UserId.HasValue)
        {
            entries = entries.Where(h => h.UserId == query.UserId.Value);
        }

        if (query.Succeeded.HasValue)
        {
            entries = entries.Where(h => h.Succeeded == query.Succeeded.Value);
        }

        if (query.FromUtc.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc);
            entries = entries.Where(h => h.AttemptedAt >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc);
            entries = entries.Where(h => h.AttemptedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            entries = entries.Where(h => EF.Functions.ILike(h.UserNameAttempted, pattern));
        }

        var total = await entries.LongCountAsync(cancellationToken);
        var items = await entries
            .OrderByDescending(h => h.AttemptedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ProjectToType<LoginHistoryDto>()
            .ToListAsync(cancellationToken);

        return PagedResponse<LoginHistoryDto>.Create(items, total, query);
    }
}
