using EventSourcing.Shared.Models;

namespace EventSourcing.Optimizations.Tests.TestModels;

public class CounterStateData(AggregateId aggregateId) : ISharedStateData
{
    public int Count { get; set; }
    public AggregateId Id { get; init; } = aggregateId;
    public bool IsDeleted { get; set; }
}
