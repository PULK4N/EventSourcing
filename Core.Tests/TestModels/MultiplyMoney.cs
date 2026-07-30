using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels;

public class MultiplyMoney : IEvent
{
    public float Multiplier { get; set; }

    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo)
    {
        var accountStateData = (AccountStateData)stateData;

        accountStateData.Money *= Multiplier;

        return accountStateData;
    }
}
