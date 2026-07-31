using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels;

public class AccountStateData(AggregateId aggregateId) : ISharedStateData
{
    public float Money { get; set; }
    public AggregateId Id { get; init; } = aggregateId;
    public bool IsDeleted { get; set; }
}
