using System.Text.Json;

namespace EventSourcing.Persistence.Serialization;

public static class EventJsonSerializer
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.General);

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static string SerializeRuntimeObject(object value) =>
        JsonSerializer.Serialize(
            value,
            value.GetType(),
            Options
        );

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)!;

    public static object Deserialize(
        string json,
        Type returnType
    ) =>
        JsonSerializer.Deserialize(
            json,
            returnType,
            Options
        )!;
}
