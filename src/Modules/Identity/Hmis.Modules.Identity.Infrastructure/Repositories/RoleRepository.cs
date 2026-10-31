using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Domain.Entities;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Repositories;

public sealed class RoleRepository(IdentityDbContext context)
    : BaseRepository<Role, IdentityDbContext>(context), IRoleRepository
{
    public Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken) =>
        Set.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeRoleId, CancellationToken cancellationToken) =>
        Set.AnyAsync(r => r.NormalizedName == normalizedName && r.Id != excludeRoleId, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetAllWithPermissionsAsync(CancellationToken cancellationToken) =>
        await Set.Include(r => r.Permissions).ToListAsync(cancellationToken);

    public async Task<PagedResponse<RoleListItemDto>> SearchAsync(RoleListQuery query, CancellationToken cancellationToken)
    {
        var roles = Set.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            roles = roles.Where(r => EF.Functions.ILike(r.Name, pattern));
        }

        roles = query.SortDirection == SortDirection.Desc ? roles.OrderByDescending(r => r.Name) : roles.OrderBy(r => r.Name);

        var total = await roles.LongCountAsync(cancellationToken);
        var items = await roles
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new RoleListItemDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                Context.UserRoles.Count(ur => ur.RoleId == r.Id),
                r.Permissions.Count))
            .ToListAsync(cancellationToken);

        return PagedResponse<RoleListItemDto>.Create(items, total, query);
    }

    public Task<RoleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken) =>
        Set.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RoleDetailDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                r.Permissions.Select(p => p.PermissionId).ToList(),
                r.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);
}
