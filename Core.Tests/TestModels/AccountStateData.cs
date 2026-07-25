using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels;

public class AccountStateData : ISharedStateData
{
    public float Money { get; set; }
    public AggregateId Id { get; set; }
    public bool IsDeleted { get; set; }
}
