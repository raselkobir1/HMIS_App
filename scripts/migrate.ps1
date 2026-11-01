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
    @{ Project = 'src/Modules/Audit/Hmis.Modules.Audit.Infrastructure'; Context = 'AuditDbContext' },
    @{ Project = 'src/Modules/Administration/Hmis.Modules.Administration.Infrastructure'; Context = 'AdministrationDbContext' },
    @{ Project = 'src/Modules/HR/Hmis.Modules.HR.Infrastructure'; Context = 'HrDbContext' },
    @{ Project = 'src/Modules/Patient/Hmis.Modules.Patient.Infrastructure'; Context = 'PatientDbContext' },
    @{ Project = 'src/Modules/Appointment/Hmis.Modules.Appointment.Infrastructure'; Context = 'AppointmentDbContext' },
    @{ Project = 'src/Modules/Clinical/Hmis.Modules.Clinical.Infrastructure'; Context = 'ClinicalDbContext' },
    @{ Project = 'src/Modules/Billing/Hmis.Modules.Billing.Infrastructure'; Context = 'BillingDbContext' },
    @{ Project = 'src/Modules/Ipd/Hmis.Modules.Ipd.Infrastructure'; Context = 'IpdDbContext' },
    @{ Project = 'src/Modules/Nursing/Hmis.Modules.Nursing.Infrastructure'; Context = 'NursingDbContext' },
    @{ Project = 'src/Modules/Notifications/Hmis.Modules.Notifications.Infrastructure'; Context = 'NotificationsDbContext' }
)

# Build once; each module migration then runs without rebuilding (avoids repeated, conflicting builds).
dotnet build $startup -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

foreach ($m in $modules) {
    Write-Host "Migrating $($m.Context)..." -ForegroundColor Cyan
    dotnet ef database update --no-build --project (Join-Path $root $m.Project) --startup-project $startup --context $m.Context
    if ($LASTEXITCODE -ne 0) { throw "Migration failed for $($m.Context)" }
}
Write-Host 'All migrations applied.' -ForegroundColor Green
