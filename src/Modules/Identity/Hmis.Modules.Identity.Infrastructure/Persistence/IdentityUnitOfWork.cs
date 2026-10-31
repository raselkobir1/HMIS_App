using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;

namespace Hmis.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityUnitOfWork(IdentityDbContext context)
    : UnitOfWork<IdentityDbContext>(context), IIdentityUnitOfWork;
