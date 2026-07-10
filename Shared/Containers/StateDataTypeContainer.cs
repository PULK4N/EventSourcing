namespace EventSourcing.Shared.Containers
{
    public static class StateDataTypeContainer
    {
        private static readonly Dictionary<string, Type> stateDataTypes =
            new Dictionary<string, Type>();

        public static void AddStateDataType(string fullName, Type stateData)
        {
            var name = fullName.Split('.').Last();
            stateDataTypes.Add(name, stateData);
        }

        public static Type? GetStateDataType(string name)
        {
            if (!stateDataTypes.ContainsKey(name))
                return null;
            return stateDataTypes[name];
        }
    }
}
