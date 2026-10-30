namespace Hmis.BuildingBlocks.Domain.Abstractions;

/// <summary>Marks an entity whose changes are written to the audit log.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AuditedAttribute : Attribute;

/// <summary>Excludes a property of an audited entity from audit old/new values (e.g. hashes, tokens).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;
