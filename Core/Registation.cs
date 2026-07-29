using System.Reflection;
using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;

namespace EventSourcing.Core
{
    public static class Registration
    {
        public static IServiceCollection RegisterEventSourcingCore(
            this IServiceCollection services,
            params Assembly[] applicationAssemblies
        )
        {
            services.AddScoped<OrderNumberHelper>();
            services.AddScoped<StateMachineHandler>();
            services.AddScoped<StateCalculator>();
            services.RegisterStateDataTypes(applicationAssemblies);
            services.RegisterEventTypes(applicationAssemblies);
            services.RegisterUniqueEventConstraintCreators(applicationAssemblies);
            services.RegisterEventValidators(applicationAssemblies);
            services.AddSingleton<
                IStateMachineDefinitionProvider,
                YamlStateMachineDefinitionProvider
            >();
            services.AddScoped<IEventValidatorProvider, EventValidatorProvider>();
            services.AddScoped<IStateDataProvider, StateMachineStateDataProvider>();
            services.AddScoped<
                IUniqueEventConstraintProvider,
                StateMachineUniqueEventConstraintProvider
            >();

            services.AddScoped<IOutbox, ProjectionOutbox>();

            return services;
        }

        public static IServiceCollection RegisterDevEnvironmentProviders(
            this IServiceCollection services
        )
        {
            services.AddScoped<IStateDataProvider, AppSettingsConfigurationStateDataProvider>();

            return services;
        }

        public static IServiceCollection RegisterProdEnvironmentProviders(
            this IServiceCollection services
        )
        {
            services.AddScoped<IStateDataProvider, StateDataProvider>();

            return services;
        }

        public static IServiceCollection RegisterStateDataTypes(
            this IServiceCollection services,
            params Assembly[] applicationAssemblies
        )
        {
            var interfaceType = typeof(ISharedStateData);
            var assemblies = applicationAssemblies;

            var allImplementations = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(type => interfaceType.IsAssignableFrom(type))
                .Where(type => !type.IsInterface)
                .Where(type => !type.IsAbstract);

            foreach (var implementation in allImplementations)
            {
                if (implementation is Type type)
                    StateDataTypeContainer.AddStateDataType(type);
            }

            return services;
        }

        public static IServiceCollection RegisterEventTypes(
            this IServiceCollection services,
            params Assembly[] applicationAssemblies
        )
        {
            var interfaceType = typeof(IEvent);
            var assemblies = applicationAssemblies;

            var allImplementations = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(type => interfaceType.IsAssignableFrom(type))
                .Where(type => !type.IsInterface)
                .Where(type => !type.IsAbstract);

            foreach (var implementation in allImplementations)
            {
                if (implementation is Type type)
                    EventTypeContainer.AddEventType(type);
            }

            return services;
        }

        public static IServiceCollection RegisterUniqueEventConstraintCreators(
            this IServiceCollection services,
            params Assembly[] applicationAssemblies
        )
        {
            var interfaceType = typeof(IUniqueConstraintCreator);
            var assemblies = applicationAssemblies;

            var allImplementations = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(type => interfaceType.IsAssignableFrom(type))
                .Where(type => !type.IsInterface)
                .Where(type => !type.IsAbstract);

            foreach (var implementation in allImplementations)
            {
                if (implementation is Type type)
                    ConstraintCreatorTypeContainer.AddUniqueEventConstraintCreator(type);
            }

            return services;
        }

        public static IServiceCollection RegisterEventValidators(
            this IServiceCollection services,
            params Assembly[] applicationAssemblies
        )
        {
            var interfaceType = typeof(IEventValidator);

            var allImplementations = applicationAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => interfaceType.IsAssignableFrom(type))
                .Where(type => !type.IsInterface)
                .Where(type => !type.IsAbstract);

            foreach (var implementation in allImplementations)
            {
                EventValidatorContainer.AddEventValidator(implementation);
            }

            return services;
        }
    }
}
