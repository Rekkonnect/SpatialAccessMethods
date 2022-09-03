using Garyon.Objects;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.DataStructures.InMemory;

public abstract class BinaryHeap<TValue> : BinaryHeapBase<TValue>
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    private readonly IComparer<TValue> comparer;
    private readonly HeapArray contents = new();
    private int entryCount;

    public sealed override int EntryCount
    {
        get => entryCount;
        private protected set
        {
            if (entryCount == value)
                return;

            entryCount = value;
            contents.Height = Height;
        }
    }

    /// <summary>
    /// Gets the lowest priority value, which is the value furthest away from being the top node.
    /// Specifically, in the max heap, this reflects the min value, whereas for the min heap, this reflects the max value.
    /// </summary>
    /// <remarks>
    /// It is always assumed that the values do not dynamically change through the <seealso cref="Node"/> instances.
    /// In such scenarios, there is low but existing chance this is outdated, just like the general heap structure.
    /// </remarks>
    public TValue? LowestPriorityValue { get; private set; }

    protected BinaryHeap(IComparer<TValue>? comparer = null)
    {
        comparer ??= Comparer<TValue>.Default;
        this.comparer = comparer;
    }

    public sealed override void Add(TValue value)
    {
        EvaluateNewlyAddedLowestPriorityValue(value);

        EntryCount++;

        var node = CreateNewNode(value);
        PushUp(node);
    }
    public sealed override TValue? Pop()
    {
        if (IsEmpty)
            return default;

        var lastNode = GetNode(entryCount - 1);
        var root = Root;
        var poppedValue = root.Value;
        root.Value = lastNode.Value;
        PushDown(root);

        EntryCount--;

        EvaluatePoppedLowestPriorityValue();

        return poppedValue;
    }

    private void EvaluateNewlyAddedLowestPriorityValue(TValue newValue)
    {
        if (IsEmpty || HigherOrEqualPriority(newValue, LowestPriorityValue!))
            LowestPriorityValue = newValue;
    }

    // Only in the case that the popped element is the last in the heap will the lowest
    // priority value be changed; and in this instance there is no other to replace it
    private void EvaluatePoppedLowestPriorityValue()
    {
        if (IsEmpty)
            LowestPriorityValue = default;
    }

    // This is an O(n) operation, despite being overall optimal in other aspects of the data structure
    private void IterateLowestPriorityValuesFinalLevel()
    {
        switch (entryCount)
        {
            case 0:
                LowestPriorityValue = default;
                return;
            case 1:
                LowestPriorityValue = contents[0];
                return;
        }

        int endIndex = entryCount - 1;
        // Only the next parent is going to have values of lower priority
        int startIndex = GetNode(endIndex).ParentIndex + 1;

        // Reset and base on that
        LowestPriorityValue = contents[0];
        for (int index = startIndex; index <= endIndex; index++)
        {
            var current = contents[index];
            if (HigherOrEqualPriority(current, LowestPriorityValue))
            {
                LowestPriorityValue = current;
            }
        }
    }

    public sealed override void PreserveMaxEntryCount(int maxEntryCount)
    {
        if (entryCount <= maxEntryCount)
            return;

        // This will automatically trim away the last levels
        EntryCount = maxEntryCount;
        IterateLowestPriorityValuesFinalLevel();
    }

    public void Clear()
    {
        EntryCount = 0;
        LowestPriorityValue = default;
    }

    private TValue GetValue(int index)
    {
        return contents[index];
    }
    protected sealed override void SetValue(int index, TValue value)
    {
        contents[index] = value;
    }
    private Node CreateNewNode(TValue value)
    {
        SetValue(entryCount - 1, value);
        return new(value, entryCount - 1, this);
    }
    public sealed override Node GetNode(int index)
    {
        if (index < 0 || index >= entryCount)
            return Node.CreateInvalid();

        var value = GetValue(index);
        return new(value, index, this);
    }

    public sealed override IEnumerator<TValue> GetEnumerator() => contents.GetEnumerator(entryCount);

    private sealed class HeapArray
    {
        private int height;

        private TValue[] contents;

        public int Height
        {
            get => height;
            set
            {
                if (height == value)
                    return;

                height = value;
                ReallocateContentsArray();
            }
        }
        public int MaxEntries => EntriesForHeight(Height);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public HeapArray()
        {
            ReallocateContentsArray();
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private void ReallocateContentsArray()
        {
            var newContents = new TValue[MaxEntries];
            contents.CopyToSafe(newContents.AsSpan());
            contents = newContents;
        }

        public void SetNode(Node node)
        {
            this[node.Index] = node.Value;
        }

        public IEnumerator<TValue> GetEnumerator(int entryCount)
        {
            return new ArraySegment<TValue>(contents, 0, entryCount).GetEnumerator();
        }

        public TValue this[int index]
        {
            get => contents[index];
            set => contents[index] = value;
        }
    }
}

public class MinHeap<TValue> : BinaryHeap<TValue>, IMinHeap<TValue>
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Less;

    public TValue? MaxValue => LowestPriorityValue;

    public MinHeap(IComparer<TValue>? comparer = null)
        : base(comparer) { }
}

public class MaxHeap<TValue> : BinaryHeap<TValue>, IMaxHeap<TValue>
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Greater;

    public TValue? MinValue => LowestPriorityValue;

    public MaxHeap(IComparer<TValue>? comparer = null)
        : base(comparer) { }
}