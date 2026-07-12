using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Core
{
    public static class Registration
    {
        public static IServiceCollection RegisterEventSourcingCoreInjection(
            this IServiceCollection services
        )
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            services.AddScoped<OrderNumberHelper>();
            services.AddScoped<StateMachineHandler>();
            services.RegisterStateDataTypes();
            services.RegisterEventTypes();
            services.RegisterUniqueEventConstraintCreators();
            services.AddSingleton<
                IStateMachineDefinitionProvider,
                YamlStateMachineDefinitionProvider
            >();
            // services.RegisterHookTypes();
            services.AddScoped<IEventValidatorProvider, DefaultEventValidatorProvider>();
            services.AddScoped<
                IUniqueEventConstraintProvider,
                DefaultUniqueEventConstraintProvider
            >();

            services.RegisterDevEnvironmentProviders();

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

        public static IServiceCollection RegisterStateDataTypes(this IServiceCollection services)
        {
            var interfaceType = typeof(ISharedStateData);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

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

        public static IServiceCollection RegisterEventTypes(this IServiceCollection services)
        {
            var interfaceType = typeof(IEvent);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

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
            this IServiceCollection services
        )
        {
            var interfaceType = typeof(IUniqueConstraintCreator);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var allImplementations = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(type => interfaceType.IsAssignableFrom(type))
                .Where(type => !type.IsInterface)
                .Where(type => !type.IsAbstract);

            foreach (var implementation in allImplementations)
            {
                if (implementation is Type type)
                    ConstraintCreatorTypeContainer.AddUniqueEventConstraintCreatorType(
                        type
                    );
            }

            return services;
        }
    }
}
