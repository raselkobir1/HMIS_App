#!/bin/sh
# Applies every module's migrations in dependency order (same order as scripts/migrate.ps1).
set -e
cd /src
for m in \
  "Identity:IdentityDbContext" \
  "Audit:AuditDbContext" \
  "Administration:AdministrationDbContext" \
  "HR:HrDbContext" \
  "Patient:PatientDbContext" \
  "Appointment:AppointmentDbContext" \
  "Clinical:ClinicalDbContext" \
  "Billing:BillingDbContext" \
  "Ipd:IpdDbContext" \
  "Nursing:NursingDbContext" \
  "Notifications:NotificationsDbContext"
do
  module="${m%%:*}"
  context="${m##*:}"
  echo "Migrating $context..."
  dotnet ef database update --no-build --configuration Release \
    --project "src/Modules/$module/Hmis.Modules.$module.Infrastructure" \
    --startup-project src/Api/Hmis.Api \
    --context "$context"
done
echo "All migrations applied."
