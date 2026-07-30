namespace EventSourcing.Core;

public class Constants
{
    public const string INVALID_ORDER_NUMBER_ON_OLD_EVENT =
        "Old event payloads must contain order numbers!";
    public const string INVALID_ORDER_NUMBER_ON_NEW_EVENT =
        "New event payloads must not contain order numbers!";
}
