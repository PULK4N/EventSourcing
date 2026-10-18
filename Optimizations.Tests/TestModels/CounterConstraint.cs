using EventSourcing.Shared.Models;

namespace EventSourcing.Optimizations.Tests.TestModels;

public class CounterConstraint(CounterStateData stateData)
    : UniqueConstraint(stateData, stateData.Count.ToString());
