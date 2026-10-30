# Coding Standards

Follows TAD §11 (naming) and §25 (governance). This file lists only the additions.

## Backend
- File-scoped namespaces. One public type per file. Namespace = project + folder.
- `Nullable` enabled; warnings are treated as errors in Release builds.
- Naming: `PatientCreateDto`, `PatientCreateValidator`, `PatientManager`/`IPatientManager`, `PatientRepository`/`IPatientRepository`, `PatientController`, `PatientConfiguration`.
- Async throughout, with the `Async` suffix and `CancellationToken` as the last parameter.
- Controllers do these things only: bind the request, call one manager method, and return a response through `ApiControllerBase` helpers.
- Managers do these things only: apply business rules, run the workflow, set the transaction boundary and map results. They throw typed exceptions for failures.
- Repositories do persistence only. They never call `SaveChanges`; the Unit of Work does that.
- Entities: private setters where it is practical, with state changes made through methods (`patient.Deactivate(reason)`). Collections are exposed as `IReadOnlyCollection`.
- Mapster: one `IRegister` class per module (`PatientMappingConfig`). Mappings are not configured inline in managers.
- Do not use MediatR, CQRS handlers, static service locators, or `IHttpContextAccessor` in managers (use `ICurrentUser`).
- Money is `decimal`; time comes from `IDateTimeProvider.UtcNow`.

## Frontend
- Standalone components, `OnPush` change detection, and signals for component state.
- One API service per feature (`patient-api.service.ts`). Components never call `HttpClient` directly.
- Typed Reactive Forms. Validation messages come from the shared `form-error` component.
- File names follow the Angular style guide (`patient-search.page.ts`, `patient-form.component.ts`).
- No business rules in components; only rules that affect how the form behaves.
