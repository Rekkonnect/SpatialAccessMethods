using Garyon.Objects;

namespace SpatialAccessMethods.DataStructures;

public interface IBinaryHeap<TValue> : IEnumerable<TValue>, IValidatableStructure
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    public abstract ComparisonResult TopNodeInequality { get; }
    public abstract int EntryCount { get; }

    public int Height { get; }

    public abstract TValue? Pop();
    public abstract void Add(TValue value);
    public abstract void PreserveMaxEntryCount(int maxEntryCount);
}

public interface IMinHeap<TValue> : IBinaryHeap<TValue>, IValidatableStructure
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    ComparisonResult IBinaryHeap<TValue>.TopNodeInequality => ComparisonResult.Less;
}
public interface IMaxHeap<TValue> : IBinaryHeap<TValue>, IValidatableStructure
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    ComparisonResult IBinaryHeap<TValue>.TopNodeInequality => ComparisonResult.Greater;
}
