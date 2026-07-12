namespace EventSourcing.Shared.Containers
{
    public static class HookTypeContainer
    {
        private static readonly Dictionary<string, Type> hookTypes = new Dictionary<string, Type>();

        public static void AddHookType(string fullName, Type hook)
        {
            var name = fullName.Split('.').Last();
            hookTypes.Add(name, hook);
        }

        public static Type GetHookType(string name)
        {
            if (!hookTypes.ContainsKey(name))
                throw new ArgumentNullException(
                    $"Hook type with name '{name}' not found.",
                    nameof(name)
                );
            return hookTypes[name];
        }
    }
}
