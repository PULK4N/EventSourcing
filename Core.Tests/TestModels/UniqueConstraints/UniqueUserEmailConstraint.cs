using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels.UniqueConstraints;

public sealed class UniqueUserEmailConstraint(YamlUserStateData stateData, string email)
    : UniqueConstraint(stateData, email);
