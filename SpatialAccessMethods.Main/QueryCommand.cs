using ManyConsole;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Main;

public abstract class QueryCommand : ConsoleCommand
{
    protected abstract SpatialDataTable<T>.IQuery GetQuery<T>()
        where T : ILocated, IID, IRecordSerializable<T>;
    
    private SpatialDataTable<T>.IQuery GetQuery<T>(SpatialDataTable<T> table)
        where T : ILocated, IID, IRecordSerializable<T>
    {
        return GetQuery<T>();
    }

    private IEnumerable<MapRecordEntry> PerformQuery()
    {
        return PerformQuery(DatabaseController.Instance.Table);
    }
    private IEnumerable<T> PerformQuery<T>(SpatialDataTable<T> table)
        where T : ILocated, IID, IRecordSerializable<T>
    {
        return GetQuery(table).Perform(table);
    }

    public sealed override int Run(string[] remainingArguments)
    {
        var result = PerformQuery().ToArray();
        Console.WriteLine($"The query returned the following: {result.Length} entries:");
        foreach (var entry in result)
        {
            Console.Write("- ");
            Console.WriteLine(entry);
        }

        return 0;
    }
}
