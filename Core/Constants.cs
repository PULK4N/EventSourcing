namespace EventSourcing.Core;

public class Constants
{
    public const string INVALID_ORDER_NUMBER_ON_OLD_EVENT =
        "Old event payloads must contain order numbers!";
    public const string DUPLICATE_ORDER_NUMBER_ON_OLD_EVENTS =
        "Two of previously executed events contain same older number!";
    public const string INVALID_ORDER_NUMBER_ON_NEW_EVENT =
        "New event payloads must not contain order numbers!";
    public const string DIFFERENT_STATE_MACHINE_ID_OR_AGGREGATE_ID =
        "Provided events must contain same aggregate id and state machine id as the first event.\nFirst Event Payload aggregateId: {0}, first Event Payload state machine id: {1}";
}
