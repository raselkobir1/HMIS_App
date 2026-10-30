# Testing

## Approved deviation (2026-10-31)
The product owner decided that **no test projects** will be created (no xUnit unit, integration, architecture, or Playwright E2E projects). This overrides BRD §17 and TAD §21.

## What replaces them
For each module, before it is marked "Testing Completed" in `IMPLEMENTATION-STATUS.md`:
1. `dotnet build` of the whole solution with zero errors and zero warnings.
2. Migrations apply cleanly to an empty database using the migration script.
3. Manual API verification in Swagger:
   - happy path for every endpoint;
   - validation errors (400 with field errors);
   - unauthenticated (401) and missing-permission (403) cases;
   - not-found (404) and conflict/concurrency (409) cases;
   - the module's business rules (for example duplicate patient warning, MR number uniqueness).
4. Manual UI walkthrough of the module's screens: create, search, edit, permission-hidden menus, and loading/empty/error states.
5. `ng build` with no errors.

The checklist results for each module are recorded in `IMPLEMENTATION-STATUS.md`.

**Risk:** regressions in earlier modules are not detected automatically. Re-check earlier modules' key flows after cross-cutting changes. Test projects can be added later without changing the architecture.
