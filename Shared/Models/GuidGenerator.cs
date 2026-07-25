using UUIDNext;

namespace EventSourcing.Shared.Models;

public static class DatabaseFriendlyGuidGenerator
{
    private static Database? database = null;

    public static void SetDefaultGuidGenerationDatabase(Database configuredDatabase)
    {
        if (database is not null)
            return;

        database = configuredDatabase;
    }

    public static Guid NewGuid() =>
        database switch
        {
            Database.PostgreSql => Uuid.NewDatabaseFriendly(Database.PostgreSql),
            Database.SqlServer => Uuid.NewDatabaseFriendly(Database.SqlServer),
            _ => throw new ArgumentNullException(nameof(database))
        };
}
