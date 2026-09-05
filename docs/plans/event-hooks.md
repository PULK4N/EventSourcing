# Event hooks

Project: EventSourcing  
Feature ID: `01a07270-3630-763f-b1db-4db3aa3dbd25`  
Status: runtime hook contracts, configuration, provider, and DI registration implemented and verified.

## Requested behavior

An application can configure hooks for an event. The runtime selects them through `IEventHookProvider`. The default `StateMachineEventHookProvider` reads the event's `hooks` from `IStateMachineDefinitionProvider`. Applications can replace the hook provider with an implementation that reads another state machine, a database, or another source.

Do not reuse, adapt, or invoke `IHookScheduler`. Preserve its existing file. This is a constraint of this feature, not a reusable project policy.

Hooks are C# implementations resolved through dependency injection and identified by their concrete class names. This plan covers runtime contracts, event-level configuration, provider selection, and registration. Application dispatch and delivery are outside its scope.

## File placement

| File | Proposed change |
| --- | --- |
| `Shared/Interfaces/IEventHook.cs` | Add the application-facing executable hook contract. |
| `Core/Interfaces/IEventHookProvider.cs` | Add asynchronous event-to-hooks selection. |
| `Core/Models/StateMachineDefinition.cs` | Add `Hooks` to `StateMachineEventDefinition` only. |
| `Core/Providers/YamlStateMachineDefinitionProvider.cs` | Normalize null hook sets and reject blank names. |
| `Core/Providers/StateMachineEventHookProvider.cs` | Add the default definition-backed provider and DI hook lookup. |
| `Core/Registation.cs` | Register the default provider with `TryAddScoped`. Keep the existing filename. |
| `Core.Tests/StateMachineEventHookProviderTests.cs` | Add selection and registration-validation tests. |
| `Core.Tests/EventHookRegistrationTests.cs` | Add default and custom-provider DI tests. |
| `Core.Tests/YamlStateMachineDefinitionProviderTests.cs` | Extend YAML coverage for hook sets. |

No new project, persistence schema, static hook container, scheduler integration, or consumer-specific hook implementation is needed for this first version.

## Proposed contracts and configuration

The runtime code below has been implemented. Existing-file snippets show the relevant additions, not complete files. DI override examples illustrate the supported registration options.

`Shared/Interfaces/IEventHook.cs`:

```csharp
using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Interfaces;

public interface IEventHook
{
    /// <summary>
    /// Handles an event. Treat the payload as read-only;
    /// mutations are not persisted by this call.
    /// </summary>
    Task Execute(EventPayload payload, CancellationToken cancellationToken = default);
}
```

The hook name is its concrete class name, obtained through `hook.GetType().Name`, with case-sensitive matching. There is no separate name property or alias. YAML uses names such as `NotifyOwnerHook`. Reject duplicate class names in the registered hook collection, including classes in different namespaces with the same simple name. Renaming a hook class requires updating its configured references.

`Core/Interfaces/IEventHookProvider.cs`:

```csharp
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces;

public interface IEventHookProvider
{
    /// <summary>
    /// Selects hooks for one event without executing them. Return each hook once.
    /// Hook order is unspecified.
    /// </summary>
    Task<List<IEventHook>> GetHooks(
        EventPayload payload,
        CancellationToken cancellationToken = default);
}
```

Add to the existing `StateMachineEventDefinition` class in `Core/Models/StateMachineDefinition.cs`:

```csharp
public HashSet<string> Hooks { get; set; } = new(StringComparer.Ordinal);
```

Only event-level hooks are introduced. Do not add root-level defaults or copy projection batch semantics into hooks. Each event's hook selection is independent.

```yaml
id: example-state-machine
stateData: ExampleStateData
events:
  ExampleCreatedV1:
    hooks:
      - NotifyOwnerHook
      - RecordAuditHook
  ExampleUpdatedV1: {}
```

`ExampleStateData` and the event types above are illustrative application types, not existing runtime types. Existing YAML without `hooks` continues to select no hooks. Treat `hooks: null` as an empty set. YAML still uses a sequence; deserializing into the set removes duplicate names. Hook execution order is unspecified.

Inside the existing YAML loader validation loop, alongside normalization of `Projections`:

```csharp
eventDefinition.Hooks ??= new HashSet<string>(StringComparer.Ordinal);

foreach (var hookName in eventDefinition.Hooks)
{
    if (string.IsNullOrWhiteSpace(hookName))
        throw new InvalidOperationException(
            $"Event '{eventName}' in state machine '{definition.Id}' "
            + "contains an empty hook name.");
}
```

Validate hook names against DI registrations in the scoped default hook provider below. Do not inject scoped hook instances into the singleton YAML provider. Type compatibility is enforced by accepting only `IEventHook` registrations. YAML syntax is validated when definitions load; unresolved names are validated when the default hook provider is constructed. This is not a promise of eager host-startup validation. A custom provider does not instantiate the default provider or require its YAML hook names to resolve.

## Default provider

`Core/Providers/StateMachineEventHookProvider.cs`:

```csharp
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
```

The event definition's set ensures each configured hook name appears once, so the default provider needs no duplicate-tracking collection. Missing event configuration selects no hooks, matching existing optional event-configuration lookups. An unknown state-machine ID still propagates the existing definition-provider exception. Resolve the machine using the payload's metadata, not CLR event type alone: one event type can have different hook configuration in different machines.

The scoped dictionary caches hook instances only for that DI scope. The default provider enumerates registered hook implementations; hook constructors must not perform effects. No static singleton instances or parameterless-constructor requirement is introduced. Immutable loaded definitions are assumed during a scope, as elsewhere in the runtime; dynamic providers own their own consistency rules.

## Registration and provider override

Add `using Microsoft.Extensions.DependencyInjection.Extensions;` and this registration inside `RegisterEventSourcingCore` in `Core/Registation.cs`:

```csharp
services.TryAddScoped<IEventHookProvider, StateMachineEventHookProvider>();
```

Application hook registrations:

```csharp
services.AddScoped<IEventHook, NotifyOwnerHook>();
services.AddScoped<IEventHook, RecordAuditHook>();
services.RegisterEventSourcingCore(typeof(ApplicationMarker).Assembly);
```

`NotifyOwnerHook`, `RecordAuditHook`, and `ApplicationMarker` are illustrative application types. Hook implementations receive dependencies through their constructors. The runtime must not instantiate application handlers itself.

A custom provider registered before core registration is preserved:

```csharp
services.AddScoped<IEventHookProvider, HookStateMachineEventHookProvider>();
services.RegisterEventSourcingCore(typeof(ApplicationMarker).Assembly);
```

Or explicitly replace the default after core registration:

```csharp
services.Replace(
    ServiceDescriptor.Scoped<IEventHookProvider, HookStateMachineEventHookProvider>());
```

Do not modify `IStateMachineDefinitionProvider` to override hook selection. Other runtime behavior still uses it. Custom selection replaces the default hook source completely; there is no implicit merge with YAML hooks.

A custom provider implements `IEventHookProvider` and can load hook selections from another source. Its implementation is outside this runtime plan.

## Implementation sequence and verification

1. Add shared/core contracts, the event-level hook set, YAML normalization, and the default provider.
2. Add replaceable scoped registration. Verify custom providers work independently of the default YAML hook resolver.
3. Run focused Core tests for configuration, selection, and DI behavior.

Acceptance tests:

| Area | Assertions |
| --- | --- |
| YAML | Missing hooks, null hooks, and empty sets select none; duplicate sequence entries deserialize into one set entry; case-sensitive names remain distinct; blank entries fail with event/machine context. Do not assert set iteration order. Use the existing serialized static-container test collection. |
| Provider | Resolves exact, case-sensitive concrete class names for the payload's machine/event; same event type in different machines selects different hooks; duplicate YAML entries select once; unconfigured events select none; unknown machine throws; missing registrations or duplicate simple class names (including across namespaces) fail. |
| Isolation | Returned lists are fresh, so modifying one result does not modify definitions or later results. |
| DI | Default provider is scoped; custom registration before core is retained; explicit replacement after core is selected; scoped dependencies are disposed with their DI scope. No static hook registry. |
| Compatibility | Existing YAML without hooks continues to load and selects no hooks. |

Suggested targeted test command after implementation:

```text
dotnet test Core.Tests/Core.Tests.csproj --filter "FullyQualifiedName~EventHook|FullyQualifiedName~YamlStateMachineDefinitionProviderTests"
```

Verification: 22 focused provider and YAML tests passed, followed by 3 DI registration tests after registration was added (25 total). Covered missing/null/duplicate YAML entries, case-sensitive class-name lookup, unknown hooks and machines, duplicate class names, selection without execution, result isolation, cancellation, scoped dependency disposal, and custom provider registration before/after core setup. `git diff --check` passed. The test build reported existing nullable-property warnings in Shared and Persistence. No application dispatch or worker was added.
