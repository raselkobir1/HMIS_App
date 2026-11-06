using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Domain.Entities;

namespace Hmis.Modules.Administration.Application.Managers;

public sealed class NumberSequenceManager(
    INumberSequenceRepository repository,
    IAdministrationUnitOfWork unitOfWork,
    IDateTimeProvider dateTime,
    IAuditService auditService) : INumberSequenceManager
{
    public async Task<IReadOnlyList<NumberSequenceDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<NumberSequenceDto> UpdateAsync(string key, NumberSequenceUpdateDto request, CancellationToken cancellationToken)
    {
        var sequence = await GetAsync(key, cancellationToken);
        repository.SetExpectedRowVersion(sequence, request.RowVersion);
        sequence.Configure(request.Prefix, request.YearFormat, request.PadLength, request.ResetPolicy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(sequence);
    }

    public async Task<NumberSequenceDto> SetNextValueAsync(string key, NumberSequenceNextValueDto request, CancellationToken cancellationToken)
    {
        var sequence = await GetAsync(key, cancellationToken);
        var previousNext = sequence.PreviewNext(Year);
        sequence.SetNextValue(request.NextValue);

        // CurrentValue is [NotAudited] (it changes on every allocation), so record this manual jump explicitly.
        auditService.Record(new AuditEvent("NextValueChanged", "Administration", nameof(NumberSequence), sequence.Key,
            new { NextNumber = previousNext }, new { NextNumber = sequence.PreviewNext(Year) }));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(sequence);
    }

    private int Year => dateTime.LocalToday.Year;

    private async Task<NumberSequence> GetAsync(string key, CancellationToken cancellationToken) =>
        await repository.GetByKeyAsync(key.ToUpperInvariant(), cancellationToken) ?? throw new NotFoundException(nameof(NumberSequence), key);

    private NumberSequenceDto ToDto(NumberSequence s) =>
        new(s.Key, s.Name, s.Prefix, s.YearFormat, s.PadLength, s.ResetPolicy, s.CurrentValue, s.PreviewNext(Year), s.RowVersion);
}

/// <summary>INumberSequenceService contract for other modules (e.g. Patient asks for the next MR number).</summary>
public sealed class NumberSequenceService(INumberSequenceRepository repository, IDateTimeProvider dateTime) : INumberSequenceService
{
    /// <summary>A number sequence the caller depends on has not been seeded; a 422 with the key is more useful than a 500.</summary>
    private const string NumberSequenceMissing = "NUMBER_SEQUENCE_MISSING";

    public async Task<string> NextAsync(string key, CancellationToken cancellationToken)
    {
        var year = dateTime.LocalToday.Year;
        var allocated = await repository.AllocateNextAsync(key.ToUpperInvariant(), year, cancellationToken)
            ?? throw new BusinessException(
                NumberSequenceMissing,
                $"Number sequence '{key.ToUpperInvariant()}' is not configured. Add it under Administration > Number sequences and try again.");

        return NumberSequence.Format(allocated.Prefix, allocated.YearFormat, allocated.PadLength, allocated.Value, year);
    }
}
