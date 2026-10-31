using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Domain.Entities;
using Mapster;

namespace Hmis.Modules.Identity.Application.Mappings;

public sealed class IdentityMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Permission, PermissionDto>();
        config.NewConfig<LoginHistory, LoginHistoryDto>();
    }
}
