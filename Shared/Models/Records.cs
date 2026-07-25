namespace EventSourcing.Shared.Models;

public readonly record struct AggregateId(Guid Value)
{
    public static AggregateId New() => new(DatabaseFriendlyGuidGenerator.NewGuid());

    public static AggregateId FromDatabaseGuid(Guid guid) => new AggregateId(guid);
}

public readonly record struct EventExecutor(Guid Value)
{
    public static EventExecutor New() => new(DatabaseFriendlyGuidGenerator.NewGuid());

    public static EventExecutor FromDatabaseGuid(Guid guid) => new EventExecutor(guid);
}
