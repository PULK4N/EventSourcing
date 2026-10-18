using System.Text.Json;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels.UniqueConstraints;

public sealed class UniqueUsernameAndNameConstraint(
    YamlUserStateData stateData,
    string username,
    string name
) : UniqueConstraint(stateData, JsonSerializer.Serialize(new List<string> { username, name }));
