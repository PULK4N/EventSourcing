namespace EventSourcing.Shared.Models;

public sealed record UniqueEventConstraintData(string ConstraintName, string ValueToHash);
