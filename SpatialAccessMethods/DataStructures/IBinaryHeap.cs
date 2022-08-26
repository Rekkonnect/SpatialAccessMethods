using Garyon.Objects;
using System.Numerics;

namespace SpatialAccessMethods.DataStructures;

public interface IBinaryHeap<TValue>
{
    public abstract ComparisonResult TopNodeInequality { get; }

    public int EntryCount { get; }
    public int Height { get; }

    protected static int HeightForEntries(int entryCount)
    {
        if (entryCount is 0)
            return 0;

        return BitOperations.Log2((uint)entryCount) + 1;
    }
    protected internal static int EntriesForHeight(int height)
    {
        return (1 << (height + 1)) - 1;
    }
}
