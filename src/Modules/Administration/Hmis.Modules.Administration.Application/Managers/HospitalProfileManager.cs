using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Files;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Domain.Entities;

namespace Hmis.Modules.Administration.Application.Managers;

public sealed class HospitalProfileManager(
    IHospitalProfileRepository repository,
    IAdministrationUnitOfWork unitOfWork,
    IFileStorageService fileStorage,
    IAuditService auditService) : IHospitalProfileManager
{
    public const long MaxLogoBytes = 1024 * 1024;

    public async Task<HospitalProfileDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await GetProfileAsync(cancellationToken));

    public async Task<HospitalProfileDto> UpdateAsync(HospitalProfileUpdateDto request, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        repository.SetExpectedRowVersion(profile, request.RowVersion);

        profile.Update(request.Name, request.NameBangla, request.ShortName, request.Address, request.Phone, request.Email,
            request.Website, request.RegistrationNumber, request.LicenseNumber, request.TaxId, request.OpeningHours);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    public async Task<HospitalProfileDto> UploadLogoAsync(Stream content, long length, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaxLogoBytes)
        {
            throw new BusinessException(AdministrationErrorCodes.LogoInvalid, "The logo must be an image of at most 1 MB.", "file");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        // Decide the type from the file's own bytes, never from its name or the client's Content-Type.
        var (contentType, extension) = FileSignatures.DetectImage(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            ?? throw new BusinessException(AdministrationErrorCodes.LogoInvalid, "The logo must be a PNG, JPEG or WebP image.", "file");

        var profile = await GetProfileAsync(cancellationToken);
        var previousPath = profile.LogoPath;

        buffer.Position = 0;
        var stored = await fileStorage.SaveAsync(buffer, "hospital/logo", extension, contentType, cancellationToken);
        profile.SetLogo(stored.StoragePath, contentType);
        auditService.Record(new AuditEvent("LogoChanged", "Administration", nameof(HospitalProfile), profile.Id.ToString(),
            null, new { stored.ContentType, stored.SizeBytes, stored.Sha256 }));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(stored.StoragePath, CancellationToken.None);
            throw;
        }

        if (previousPath is not null)
        {
            await fileStorage.DeleteAsync(previousPath, cancellationToken);
        }

        return ToDto(profile);
    }

    public async Task<HospitalProfileDto> RemoveLogoAsync(CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        var previousPath = profile.LogoPath;
        if (previousPath is null)
        {
            return ToDto(profile);
        }

        profile.SetLogo(null, null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(previousPath, cancellationToken);
        return ToDto(profile);
    }

    public async Task<BrandingDto> GetBrandingAsync(CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        return new BrandingDto(profile.Name, profile.ShortName, profile.LogoPath is not null, profile.LogoPath is null ? null : profile.RowVersion.ToString(),
            profile.Address, profile.Phone, profile.Email);
    }

    public async Task<LogoFile?> OpenLogoAsync(CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile.LogoPath is null || profile.LogoContentType is null)
        {
            return null;
        }

        var stream = await fileStorage.OpenReadAsync(profile.LogoPath, cancellationToken);
        return stream is null ? null : new LogoFile(stream, profile.LogoContentType);
    }

    private async Task<HospitalProfile> GetProfileAsync(CancellationToken cancellationToken) =>
        await repository.GetAsync(cancellationToken) ?? throw new NotFoundException(nameof(HospitalProfile), "default");

    private static HospitalProfileDto ToDto(HospitalProfile p) =>
        new(p.Name, p.NameBangla, p.ShortName, p.Address, p.Phone, p.Email, p.Website, p.RegistrationNumber,
            p.LicenseNumber, p.TaxId, p.OpeningHours, p.LogoPath is not null, p.RowVersion);

}

public sealed class SystemSettingsManager(ISystemSettingsRepository repository, IAdministrationUnitOfWork unitOfWork)
    : ISystemSettingsManager
{
    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await GetSettingsAsync(cancellationToken));

    public async Task<SystemSettingsDto> UpdateAsync(SystemSettingsUpdateDto request, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        repository.SetExpectedRowVersion(settings, request.RowVersion);
        settings.Update(request.CurrencyCode, request.CurrencySymbol, request.FinancialYearStartMonth);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private async Task<SystemSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await repository.GetAsync(cancellationToken) ?? throw new NotFoundException(nameof(SystemSettings), "default");

    private static SystemSettingsDto ToDto(SystemSettings s) =>
        new(s.TimeZone, s.CurrencyCode, s.CurrencySymbol, s.FinancialYearStartMonth, s.RowVersion);
}
