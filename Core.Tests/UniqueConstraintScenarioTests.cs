using System.Collections;
using System.Data.Common;
using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels.UniqueConstraints;
using EventSourcing.Persistence;
using EventSourcing.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace EventSourcing.Core.Tests;

/// <summary>
/// Runs events through the real handler, calculator, and SQL event store on SQLite, with a new
/// DbContext per write the way separate requests would, and checks the stored constraint rows.
/// </summary>
[Collection(StaticTypeContainerCollection.Name)]
public sealed class UniqueConstraintScenarioTests : IDisposable
{
    private const string StateMachineId = "users-state-machine";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Dictionary<string, AggregateId> _aggregates = new();
    private readonly IStateMachineDefinitionProvider _definitions;
    private readonly DatabaseCallRecorder _databaseCalls = new();

    [Fact]
    public async Task SettingEmailStoresIt()
    {
        await Write(("A", new("x")));

        await AssertRows(Email("A", "x"));
    }

    [Fact]
    public async Task ClearingEmailRemovesIt()
    {
        await Write(("A", new("x")));

        await Write(("A", new(null)));

        await AssertRows();
    }

    [Fact]
    public async Task SettingAndClearingInOneWriteStoresNothing()
    {
        await Write(("A", new("x")), ("A", new(null)));

        await AssertRows();
    }

    [Fact]
    public async Task OnlyTheFinalStateIsStored()
    {
        await Write(("A", new("x")));

        await Write(("A", new(null)), ("A", new("x")), ("A", new(null)), ("A", new("x")));

        await AssertRows(Email("A", "x"));
    }

    [Fact]
    public async Task EveryConstraintOfTheStateIsStored()
    {
        await Write(("A", new("x", "john", "John Smith")));

        await AssertRows(Email("A", "x"), UsernameAndName("A", "john", "John Smith"));
    }

    [Fact]
    public async Task RepeatingTheSameValueKeepsOneRow()
    {
        await Write(("A", new("x")));

        await Write(("A", new("x")));

        await AssertRows(Email("A", "x"));
    }

    [Fact]
    public async Task ChangingEmailReplacesIt()
    {
        await Write(("A", new("x")));

        await Write(("A", new("y")));

        await AssertRows(Email("A", "y"));
    }

    [Fact]
    public async Task SavedValueCanMoveToAnotherAggregate()
    {
        await Write(("A", new("x")));

        await Write(("A", new(null)), ("B", new("x")));

        await AssertRows(Email("B", "x"));
    }

    [Fact]
    public async Task ValueHeldOnlyInsideOneWriteCanMoveToAnotherAggregate()
    {
        await Write(("A", new("x")), ("A", new(null)), ("B", new("x")));

        await AssertRows(Email("B", "x"));
    }

    [Fact]
    public async Task AggregatesCanSwapValues()
    {
        await Write(("A", new("x")), ("B", new("y")));

        await Write(("A", new("y")), ("B", new("x")));

        await AssertRows(Email("A", "y"), Email("B", "x"));
    }

    [Fact]
    public async Task FreedValueCanBeTakenInALaterWrite()
    {
        await Write(("A", new("x")));
        await Write(("A", new(null)));

        await Write(("B", new("x")));

        await AssertRows(Email("B", "x"));
    }

    [Fact]
    public async Task DeletingFreesEveryValue()
    {
        await Write(("A", new("x", "john", "John Smith")));

        await Write(("A", new("x", "john", "John Smith", IsDeleted: true)));

        await AssertRows();
    }

    [Fact]
    public async Task AggregatesWithoutValuesStoreNothing()
    {
        await Write(("A", new(null)), ("B", new(null)));

        await AssertRows();
    }

    [Fact]
    public async Task SameUsernameWithAnotherNameIsAllowed()
    {
        await Write(("B", new(null, "john", "John Smith")));

        await Write(("A", new(null, "john", "John Doe")));

        await AssertRows(UsernameAndName("B", "john", "John Smith"), UsernameAndName("A", "john", "John Doe"));
    }

    [Fact]
    public async Task CallbackEventsCountTowardsTheFinalState()
    {
        await Write(("A", new("x")));

        await using var dbContext = CreateDbContext();
        await CreateHandler(dbContext)
            .ExecuteEvents([ Payload("A", new(null)) ], _ => [ Payload("A", new("x")) ]);

        await AssertRows(Email("A", "x"));
    }

    [Theory]
    [InlineData("x", false)]
    [InlineData("y", true)]
    public async Task ConstraintRowsAreWrittenOnlyWhenValuesChange(string email, bool valuesChange)
    {
        await Write(("A", new("x")));
        var eventCount = await CountEvents();
        _databaseCalls.Clear();

        await Write(("A", new(email)));

        Assert.Equal(
            valuesChange,
            _databaseCalls.Commands.Any(command => command.Contains("UniqueConstraints"))
        );
        Assert.Equal(eventCount + 1, await CountEvents());
        await AssertRows(Email("A", email));
    }

    /// <remarks>
    /// Calls the SQL store directly, because <see cref="EventStoreWithOutbox"/> always opens
    /// its own transaction around the store and the outbox.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoreStartsTransactionOnlyWhenValuesChange(bool valuesChange)
    {
        var payload = Payload("A", new("x"));
        payload.EventExecutionInfo.OrderNumber = 1;
        var stateInfo = StateInfo.Create(new YamlUserStateData(Id("A")), StateMachineId, Id("A"));
        stateInfo.LastExecutedPayloads = [ payload ];
        stateInfo.UniqueConstraints = [ Email("A", "x") ];
        stateInfo.UniqueConstraintsChanged = valuesChange;
        _databaseCalls.Clear();

        await using (var dbContext = CreateDbContext())
            await new BaseSqlEventStore(dbContext).Write([ stateInfo ]);

        Assert.Equal(valuesChange ? 1 : 0, _databaseCalls.TransactionsStarted);
        Assert.Equal(1, await CountEvents());
        await AssertRows(valuesChange ? [ Email("A", "x") ] : [ ]);
    }

    [Fact]
    public async Task OnlyAggregatesWhoseValuesChangeAreRewritten()
    {
        await Write(("A", new("x")), ("B", new("y")));
        var rowIdsBefore = await RowIdsByAggregate();

        await Write(("A", new("x")), ("B", new("z")));

        var rowIdsAfter = await RowIdsByAggregate();
        Assert.Equal(rowIdsBefore[Id("A").Value], rowIdsAfter[Id("A").Value]);
        Assert.NotEqual(rowIdsBefore[Id("B").Value], rowIdsAfter[Id("B").Value]);
        await AssertRows(Email("A", "x"), Email("B", "z"));
    }

    [Fact]
    public async Task TakingAValueAnotherAggregateHoldsFails()
    {
        await Write(("B", new("x")));

        await AssertWriteFails(("A", new("x")));

        await AssertRows(Email("B", "x"));
    }

    [Fact]
    public async Task TwoAggregatesTakingTheSameNewValueFails()
    {
        await AssertWriteFails(("A", new("x")), ("B", new("x")));

        await AssertRows();
    }

    [Fact]
    public async Task SameUsernameAndNameFails()
    {
        await Write(("B", new(null, "john", "John Smith")));

        await AssertWriteFails(("A", new(null, "john", "John Smith")));

        await AssertRows(UsernameAndName("B", "john", "John Smith"));
    }

    [Fact]
    public async Task FailedWriteKeepsTheValuesTheAggregateHeld()
    {
        await Write(("A", new("x")), ("B", new("y")));

        await AssertWriteFails(("A", new("y")));

        await AssertRows(Email("A", "x"), Email("B", "y"));
    }

    public UniqueConstraintScenarioTests()
    {
        _connection.Open();
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureCreated();

        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions
            .Setup(provider => provider.Get(StateMachineId))
            .Returns(
                new StateMachineDefinition
                {
                    Id = StateMachineId,
                    StateData = nameof(YamlUserStateData),
                    InitializationEvents = [ nameof(YamlUserUpdated) ]
                }
            );
        _definitions = definitions.Object;
    }

    public void Dispose() => _connection.Dispose();

    private async Task Write(params (string Aggregate, YamlUserUpdated Event)[] events)
    {
        await using var dbContext = CreateDbContext();
        await CreateHandler(dbContext)
            .ExecuteEvents(events.Select(item => Payload(item.Aggregate, item.Event)).ToList());
    }

    /// <summary>Checks that the write throws and saves no events.</summary>
    private async Task AssertWriteFails(params (string Aggregate, YamlUserUpdated Event)[] events)
    {
        var eventCount = await CountEvents();

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => Write(events));

        Assert.Equal(eventCount, await CountEvents());
    }

    private async Task AssertRows(params UniqueConstraint[] expected)
    {
        await using var dbContext = CreateDbContext();
        var rows = await dbContext.UniqueConstraints.ToListAsync();

        Assert.Equal(expected.Select(Describe).Order(), rows.Select(Describe).Order());
    }

    private UniqueConstraint Email(string aggregate, string email) =>
        new UniqueUserEmailConstraint(new YamlUserStateData(Id(aggregate)), email);

    private UniqueConstraint UsernameAndName(string aggregate, string username, string name) =>
        new UniqueUsernameAndNameConstraint(new YamlUserStateData(Id(aggregate)), username, name);

    private StateMachineHandler CreateHandler(EventSourcingDbContext dbContext)
    {
        var stateCalculator = new StateCalculator(
            new OrderNumberHelper(),
            new StateMachineStateDataProvider(_definitions),
            new EventValidatorProvider(_definitions),
            _definitions
        );
        var eventStore = new EventStore(new BaseSqlEventStore(dbContext));
        // The SQL Outbox needs a database-generated row version that SQLite does not provide,
        // and the outbox is not under test here; without projections this one writes nothing.
        var outbox = new ProjectionOutbox(_definitions, stateCalculator, eventStore, [ ]);

        return new StateMachineHandler(
            stateCalculator,
            new EventStoreWithOutbox(dbContext, eventStore, outbox)
        );
    }

    private EventSourcingDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<EventSourcingDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_databaseCalls.Commands, _databaseCalls.Transactions)
                .Options
        );

    private async Task<Dictionary<Guid, long>> RowIdsByAggregate()
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.UniqueConstraints.ToDictionaryAsync(row => row.AggregateId, row => row.Id);
    }

    private EventPayload Payload(string aggregate, YamlUserUpdated eventData) =>
        EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            Id(aggregate),
            StateMachineId,
            eventData
        );

    private async Task<int> CountEvents()
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.SerializedEventPayload.CountAsync();
    }

    private AggregateId Id(string aggregate)
    {
        if (!_aggregates.TryGetValue(aggregate, out var aggregateId))
        {
            aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
            _aggregates[aggregate] = aggregateId;
        }

        return aggregateId;
    }

    private static string Describe(UniqueConstraint constraint) =>
        $"{constraint.AggregateId}:{constraint.ConstraintName}:{Convert.ToHexString(constraint.Hash)}";

    /// <summary>Records the SQL commands and transactions every created DbContext runs.</summary>
    private sealed class DatabaseCallRecorder
    {
        public CommandRecorder Commands { get; } = new();
        public TransactionRecorder Transactions { get; } = new();
        public int TransactionsStarted => Transactions.Started;

        public void Clear()
        {
            Commands.Clear();
            Transactions.Started = 0;
        }
    }

    private sealed class CommandRecorder : DbCommandInterceptor, IEnumerable<string>
    {
        private readonly List<string> _commandTexts = [ ];

        public void Clear() => _commandTexts.Clear();

        public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
        {
            _commandTexts.Add(result.CommandText);
            return result;
        }

        public IEnumerator<string> GetEnumerator() => _commandTexts.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TransactionRecorder : DbTransactionInterceptor
    {
        public int Started { get; set; }

        public override DbTransaction TransactionStarted(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result
        )
        {
            Started++;
            return result;
        }

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default
        )
        {
            Started++;
            return ValueTask.FromResult(result);
        }
    }
}
