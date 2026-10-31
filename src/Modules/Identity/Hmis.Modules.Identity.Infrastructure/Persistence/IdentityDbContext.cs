using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Persistence;

/// <summary>Owns the "auth" schema. No other module may use this context.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "auth";

    public override string Schema => SchemaName;

    public override string ModuleName => "Identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
}
