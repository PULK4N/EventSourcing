using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Optimizations.Tests.TestModels;

public class AddToCounter : IEvent
{
    public int Amount { get; set; }

    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo)
    {
        var counter = (CounterStateData)stateData;
        counter.Count += Amount;
        return counter;
    }
}
