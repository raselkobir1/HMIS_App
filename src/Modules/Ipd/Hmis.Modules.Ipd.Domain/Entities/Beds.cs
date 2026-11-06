using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Ipd.Domain.Entities;

public enum WardType
{
    General,
    Cabin,
    Icu,
    Hdu,
    Nicu,
    Maternity,
    Other
}

/// <summary>BRD Module 13 bed statuses. Occupied is set only by admission, transfer and discharge.</summary>
public enum BedStatus
{
    Available,
    Occupied,
    Reserved,
    UnderMaintenance,
    CleaningRequired
}

/// <summary>Pricing group of beds (e.g. "General ward bed", "Single cabin AC"): daily rent and suggested admission deposit.</summary>
[Audited]
public sealed class BedCategory : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private BedCategory() { }

    public string Name { get; private set; } = string.Empty;
    public decimal DailyRate { get; private set; }
    public decimal SuggestedDeposit { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static BedCategory Create(string name, decimal dailyRate, decimal suggestedDeposit)
    {
        var category = new BedCategory();
        category.Update(name, dailyRate, suggestedDeposit);
        return category;
    }

    public void Update(string name, decimal dailyRate, decimal suggestedDeposit)
    {
        if (dailyRate < 0 || suggestedDeposit < 0)
        {
            throw new DomainException("BED_CATEGORY_AMOUNT_INVALID", "Rates and deposits cannot be negative.");
        }

        Name = name.Trim();
        DailyRate = Math.Round(dailyRate, 2, MidpointRounding.AwayFromZero);
        SuggestedDeposit = Math.Round(suggestedDeposit, 2, MidpointRounding.AwayFromZero);
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

/// <summary>A ward, cabin block or unit. Building and floor are free text until a building register is needed.</summary>
[Audited]
public sealed class Ward : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private Ward() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public WardType Type { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public string? Building { get; private set; }
    public string? Floor { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static Ward Create(string code, string name, WardType type, Guid? departmentId, string? building, string? floor)
    {
        var ward = new Ward { Code = code.Trim().ToUpperInvariant() };
        ward.Update(name, type, departmentId, building, floor);
        return ward;
    }

    public void Update(string name, WardType type, Guid? departmentId, string? building, string? floor)
    {
        Name = name.Trim();
        Type = type;
        DepartmentId = departmentId;
        Building = Clean(building);
        Floor = Clean(floor);
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[Audited]
public sealed class Bed : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private Bed() { }

    public Guid WardId { get; private set; }
    public string BedNumber { get; private set; } = string.Empty;

    /// <summary>Room or cabin number, when beds are grouped in rooms.</summary>
    public string? Room { get; private set; }

    public Guid CategoryId { get; private set; }
    public BedStatus Status { get; private set; } = BedStatus.Available;
    public string? StatusNote { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    /// <summary>Only an active, Available bed takes a patient; a reserved bed is released by housekeeping first.</summary>
    public bool CanBeAssigned => IsActive && Status == BedStatus.Available;

    /// <summary>Why the bed cannot take a patient now, for the person admitting or transferring.</summary>
    public string? UnavailableReason => CanBeAssigned
        ? null
        : !IsActive
            ? $"Bed {BedNumber} is not in use."
            : Status switch
            {
                BedStatus.Occupied => $"Bed {BedNumber} is occupied.",
                BedStatus.Reserved => $"Bed {BedNumber} is reserved{(StatusNote is null ? "" : $" ({StatusNote})")}. Housekeeping must mark it available first.",
                BedStatus.UnderMaintenance => $"Bed {BedNumber} is under maintenance.",
                _ => $"Bed {BedNumber} is waiting to be cleaned."
            };

    public static Bed Create(Guid wardId, string bedNumber, string? room, Guid categoryId)
    {
        var bed = new Bed { WardId = wardId };
        bed.Update(bedNumber, room, categoryId);
        return bed;
    }

    public void Update(string bedNumber, string? room, Guid categoryId)
    {
        BedNumber = bedNumber.Trim().ToUpperInvariant();
        Room = string.IsNullOrWhiteSpace(room) ? null : room.Trim();
        CategoryId = categoryId;
    }

    public void SetActive(bool isActive)
    {
        if (!isActive && Status == BedStatus.Occupied)
        {
            throw new DomainException("BED_OCCUPIED", "An occupied bed cannot be deactivated. Transfer or discharge the patient first.");
        }

        IsActive = isActive;
    }

    /// <summary>Housekeeping and reservations. Occupancy changes only through admission, transfer and discharge.</summary>
    public void SetStatus(BedStatus status, string? note)
    {
        if (status == BedStatus.Occupied || Status == BedStatus.Occupied)
        {
            throw new DomainException("BED_OCCUPIED", Status == BedStatus.Occupied
                ? "The bed is occupied. Transfer or discharge the patient to free it."
                : "Beds become occupied only by admitting or transferring a patient.");
        }

        Status = status;
        StatusNote = status == BedStatus.Available || string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    internal void Occupy()
    {
        if (UnavailableReason is { } reason)
        {
            throw new DomainException("BED_UNAVAILABLE", reason);
        }

        Status = BedStatus.Occupied;
        StatusNote = null;
    }

    /// <summary>A bed that a patient has left needs cleaning before the next patient.</summary>
    internal void Vacate()
    {
        Status = BedStatus.CleaningRequired;
        StatusNote = null;
    }
}
