using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Administration.Domain.Entities;

public enum YearFormat
{
    None,
    TwoDigit,
    FourDigit
}

public enum SequenceResetPolicy
{
    Never,
    Yearly
}

/// <summary>
/// Configurable business-number generator (MR number, invoice number, ...).
/// Format: Prefix + optional year + zero-padded counter, e.g. MR + 26 + 000123 = "MR26000123".
/// The counter itself is advanced by an atomic SQL update, not through this entity.
/// </summary>
[Audited]
public sealed class NumberSequence : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    public const int MinPadLength = 3;
    public const int MaxPadLength = 10;

    private NumberSequence() { }

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Prefix { get; private set; } = string.Empty;
    public YearFormat YearFormat { get; private set; }
    public int PadLength { get; private set; }
    public SequenceResetPolicy ResetPolicy { get; private set; }

    /// <summary>Last number issued. Changes on every allocation, so it is excluded from the audit trail.</summary>
    [NotAudited]
    public long CurrentValue { get; private set; }

    /// <summary>Year the counter belongs to (used by the yearly reset).</summary>
    [NotAudited]
    public int PeriodYear { get; private set; }

    public uint RowVersion { get; private set; }

    public static NumberSequence Create(
        string key, string name, string prefix, YearFormat yearFormat, int padLength, SequenceResetPolicy resetPolicy, int currentYear)
    {
        var sequence = new NumberSequence { Key = key.Trim().ToUpperInvariant(), Name = name.Trim(), PeriodYear = currentYear };
        sequence.Configure(prefix, yearFormat, padLength, resetPolicy);
        return sequence;
    }

    public void Configure(string prefix, YearFormat yearFormat, int padLength, SequenceResetPolicy resetPolicy)
    {
        if (padLength is < MinPadLength or > MaxPadLength)
        {
            throw new DomainException("SEQUENCE_PAD_LENGTH", $"Number length must be between {MinPadLength} and {MaxPadLength} digits.");
        }

        if (resetPolicy == SequenceResetPolicy.Yearly && yearFormat == YearFormat.None)
        {
            // Without the year in the number, a yearly reset would re-issue numbers already used.
            throw new DomainException("SEQUENCE_RESET_NEEDS_YEAR", "A yearly reset requires the year to be part of the number.");
        }

        Prefix = prefix.Trim().ToUpperInvariant();
        YearFormat = yearFormat;
        PadLength = padLength;
        ResetPolicy = resetPolicy;
    }

    /// <summary>The counter value the next allocation will use (ignoring a pending yearly reset).</summary>
    public long NextValue => CurrentValue + 1;

    /// <summary>
    /// Moves the counter forward so the next issued number is <paramref name="nextValue"/>. Setting the value it already
    /// has is a no-op; moving backwards would re-issue numbers and is refused.
    /// </summary>
    public void SetNextValue(long nextValue)
    {
        if (nextValue == NextValue)
        {
            return;
        }

        if (nextValue < NextValue)
        {
            throw new DomainException("SEQUENCE_CANNOT_DECREASE",
                $"The next number is already {NextValue}; a sequence can only move forward, so enter {NextValue} or more.");
        }

        CurrentValue = nextValue - 1;
    }

    public string Format(long value, int year) => Format(Prefix, YearFormat, PadLength, value, year);

    public static string Format(string prefix, YearFormat yearFormat, int padLength, long value, int year)
    {
        var yearPart = yearFormat switch
        {
            YearFormat.TwoDigit => (year % 100).ToString("00"),
            YearFormat.FourDigit => year.ToString("0000"),
            _ => string.Empty
        };

        return $"{prefix}{yearPart}{value.ToString().PadLeft(padLength, '0')}";
    }

    /// <summary>The number the next allocation will produce (for previews in the UI).</summary>
    public string PreviewNext(int currentYear)
    {
        var resets = ResetPolicy == SequenceResetPolicy.Yearly && PeriodYear != currentYear;
        return Format(resets ? 1 : CurrentValue + 1, currentYear);
    }
}
