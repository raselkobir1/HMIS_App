<#
.SYNOPSIS
  Adds an EF Core migration to a module.
.EXAMPLE
  ./scripts/add-migration.ps1 -Module Identity -Name AddUserPhone
#>
param(
    [Parameter(Mandatory)] [string] $Module,
    [Parameter(Mandatory)] [string] $Name
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef migrations add $Name `
    --project (Join-Path $root "src/Modules/$Module/Hmis.Modules.$Module.Infrastructure") `
    --startup-project (Join-Path $root 'src/Api/Hmis.Api') `
    --context "${Module}DbContext" `
    --output-dir Persistence/Migrations
