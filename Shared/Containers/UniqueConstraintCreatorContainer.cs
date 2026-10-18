using EventSourcing.Shared.Interfaces;

namespace EventSourcing.Shared.Containers;

public static class UniqueConstraintCreatorContainer
{
    private static readonly Dictionary<Type, List<IUniqueConstraintCreator>> creatorsByStateData =
        new();

    public static void AddUniqueConstraintCreator(Type creatorType)
    {
        var creator = (IUniqueConstraintCreator)Activator.CreateInstance(creatorType)!;

        if (!creatorsByStateData.TryGetValue(creator.StateDataType, out var creators))
        {
            creators = [ ];
            creatorsByStateData.Add(creator.StateDataType, creators);
        }

        creators.Add(creator);
    }
}
