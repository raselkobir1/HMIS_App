using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using MapsterMapper;

namespace Hmis.Modules.Identity.Application.Managers;

public sealed class PermissionManager(IPermissionRepository permissionRepository, IMapper mapper) : IPermissionManager
{
    public async Task<IReadOnlyList<PermissionGroupDto>> GetGroupedAsync(CancellationToken cancellationToken)
    {
        var permissions = await permissionRepository.GetAllAsync(cancellationToken);

        return permissions
            .GroupBy(p => p.Module)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.OrderBy(p => p.Code).Select(mapper.Map<PermissionDto>).ToList()))
            .ToList();
    }
}

public sealed class LoginHistoryManager(ILoginHistoryRepository loginHistoryRepository) : ILoginHistoryManager
{
    public Task<PagedResponse<LoginHistoryDto>> SearchAsync(LoginHistoryQuery query, CancellationToken cancellationToken) =>
        loginHistoryRepository.SearchAsync(query, cancellationToken);
}
