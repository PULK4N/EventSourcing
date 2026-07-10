using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels;

public class TransferMoney : IEvent
{
    public float MoneySent { get; set; }

    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo)
    {
        var accountStateData = (AccountStateData)stateData;

        accountStateData.Money -= MoneySent;

        return accountStateData;
    }
}
