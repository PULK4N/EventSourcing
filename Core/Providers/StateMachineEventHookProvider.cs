using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers;

public sealed class StateMachineEventHookProvider : IEventHookProvider
{
    private readonly IStateMachineDefinitionProvider _definitions;
    private readonly Dictionary<string, IEventHook> _hooks = new(StringComparer.Ordinal);

    public StateMachineEventHookProvider(
        IStateMachineDefinitionProvider definitions,
        IEnumerable<IEventHook> hooks)
    {
        _definitions = definitions;

        foreach (var hook in hooks)
        {
            var hookName = hook.GetType().Name;
            if (!_hooks.TryAdd(hookName, hook))
                throw new InvalidOperationException(
                    $"Multiple event hooks are registered with class name '{hookName}'.");
        }

        // This provider owns YAML selection; validate its configured references
        // without requiring the singleton definition provider to resolve services.
        foreach (var definition in definitions.GetAll())
        {
            foreach (var (eventName, eventDefinition) in definition.Events)
            {
                foreach (var hookName in eventDefinition.Hooks)
                {
                    if (string.IsNullOrWhiteSpace(hookName)
                        || !_hooks.ContainsKey(hookName))
                        throw new InvalidOperationException(
                            $"Hook '{hookName}' for event '{eventName}' in "
                            + $"state machine '{definition.Id}' is not registered.");
                }
            }
        }
    }

    public Task<List<IEventHook>> GetHooks(
        EventPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        var info = payload.EventExecutionInfo;
        var definition = _definitions.Get(info.StateMachineId);
        if (!definition.Events.TryGetValue(info.EventName, out var eventDefinition))
            return Task.FromResult(new List<IEventHook>());

        var selected = new List<IEventHook>();
        foreach (var hookName in eventDefinition.Hooks)
        {
            if (!_hooks.TryGetValue(hookName, out var hook))
                throw new InvalidOperationException(
                    $"Hook '{hookName}' for event '{info.EventName}' in "
                    + $"state machine '{info.StateMachineId}' is not registered.");

            selected.Add(hook);
        }

        return Task.FromResult(selected);
    }
}
