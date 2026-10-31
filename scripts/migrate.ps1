<#
.SYNOPSIS
  Applies EF Core migrations for every module, in dependency order (docs/ARCHITECTURE.md §5.4).
  Development use. Production migrations run as a controlled deployment step, never at app startup.
.EXAMPLE
  ./scripts/migrate.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$startup = Join-Path $root 'src/Api/Hmis.Api'
$env:ASPNETCORE_ENVIRONMENT = if ($env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT } else { 'Development' }

# Order matters: add new modules here as they are built.
$modules = @(
    @{ Project = 'src/Modules/Identity/Hmis.Modules.Identity.Infrastructure'; Context = 'IdentityDbContext' },
    @{ Project = 'src/Modules/Audit/Hmis.Modules.Audit.Infrastructure'; Context = 'AuditDbContext' }
)

foreach ($m in $modules) {
    Write-Host "Migrating $($m.Context)..." -ForegroundColor Cyan
    dotnet ef database update --project (Join-Path $root $m.Project) --startup-project $startup --context $m.Context
    if ($LASTEXITCODE -ne 0) { throw "Migration failed for $($m.Context)" }
}
Write-Host 'All migrations applied.' -ForegroundColor Green
