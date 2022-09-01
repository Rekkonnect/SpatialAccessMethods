using Garyon.Extensions;
using Garyon.Objects;
using SpatialAccessMethods.Utilities;
using System.Collections;
using System.Numerics;

namespace SpatialAccessMethods.DataStructures.InMemory;

// Slightly copy-pasted from the secondary storage version
public abstract class BinaryHeap<TValue> : IBinaryHeap<TValue>, IEnumerable<TValue>
    where TValue : IComparable<TValue>
{
    private readonly IComparer<TValue> comparer;
    private readonly HeapArray contents = new();
    private int entryCount;

    public abstract ComparisonResult TopNodeInequality { get; }
    public ComparisonKinds TopNodeComparisonKinds => TopNodeInequality.GetComparisonKind();

    public int EntryCount
    {
        get => entryCount;
        private set
        {
            if (entryCount == value)
                return;

            entryCount = value;
            contents.Height = Height;
        }
    }
    public Node Root => GetNode(0);

    public int Height => IBinaryHeap<TValue>.HeightForEntries(entryCount);

    public bool IsEmpty => entryCount is 0;

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

    public void Add(TValue value)
    {
        EvaluateNewlyAddedLowestPriorityValue(value);

        EntryCount++;

        var node = CreateNewNode(value);
        PushUp(node);
    }
    public TValue? Pop()
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

    public void PreserveMaxEntryCount(int maxEntryCount)
    {
        if (entryCount <= maxEntryCount)
            return;

        // This will automatically trim away the last levels
        EntryCount = maxEntryCount;
        IterateLowestPriorityValuesFinalLevel();
    }

    public void EliminateLastLevel()
    {
        int targetLevel = Height - 1;
        PreserveMaxEntryCount(MaxEntriesForHeap(targetLevel));
    }

    private static int MaxEntriesForHeap(int levels)
    {
        return 1 << levels - 1;
    }

    public void Clear()
    {
        EntryCount = 0;
        LowestPriorityValue = default;
    }

    private void PushUp(Node node)
    {
        while (true)
        {
            if (!node.HasParent)
                return;

            var parent = node.GetParent();

            bool shouldPushUp = ShouldElevate(node, parent);
            if (!shouldPushUp)
                break;

            node.SwapWithParent();
        }
    }

    private void PushDown(Node node)
    {
        while (true)
        {
            var child = node.GetHighestPriorityChild();
            if (child.IsInvalid)
                return;

            bool shouldPushDown = ShouldElevate(child, node);

            if (!shouldPushDown)
                break;

            node.SwapWithNode(ref child);
        }
    }

    private void SwapWithParent(int index)
    {
        SwapWithParent(GetNode(index));
    }
    private void SwapWithParent(Node node)
    {
        node.SwapWithParent();
    }

    private bool ShouldElevate(Node child, Node parent)
    {
        return ShouldElevate(child.Value, parent.Value);
    }
    private bool ShouldElevate(TValue child, TValue parent)
    {
        return comparer.SatisfiesComparison(child, parent, TopNodeComparisonKinds);
    }
    private bool HigherOrEqualPriority(TValue candidate, TValue baseline)
    {
        return comparer.SatisfiesComparison(candidate, baseline, TopNodeComparisonKinds | ComparisonKinds.Equal);
    }

    private TValue GetValue(int index)
    {
        return contents[index];
    }
    private void SetValue(int index, TValue value)
    {
        contents[index] = value;
    }
    private Node CreateNewNode(TValue value)
    {
        SetValue(entryCount - 1, value);
        return new(value, entryCount - 1, this);
    }
    public Node GetNode(int index)
    {
        if (index < 0 || index >= entryCount)
            return Node.CreateInvalid();

        var value = GetValue(index);
        return new(value, index, this);
    }

    public bool ValidateStructure()
    {
        if (IsEmpty)
            return true;

        return ValidateNode(Root);
    }
    private bool ValidateNode(Node node)
    {
        return ValidateLeftChild(node)
            && ValidateRightChild(node);
    }
    private bool ValidateLeftChild(Node parent)
    {
        if (!parent.HasLeftChild)
            return true;

        var left = parent.GetLeftChild();
        return ValidateNode(parent, left);
    }
    private bool ValidateRightChild(Node parent)
    {
        if (!parent.HasRightChild)
            return true;

        var right = parent.GetRightChild();
        return ValidateNode(parent, right);
    }
    private bool ValidateNode(Node parent, Node child)
    {
        return HigherOrEqualPriority(parent.Value, child.Value)
            && ValidateNode(child);
    }

    public IEnumerator<TValue> GetEnumerator() => contents.GetEnumerator(entryCount);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
        public int MaxEntries => IBinaryHeap<TValue>.EntriesForHeight(Height);

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

    public struct Node
    {
        public BinaryHeap<TValue> Heap { get; }

        private TValue value;
        private int index;

        public TValue Value
        {
            get => value;
            set
            {
                this.value = value;
                UpdateValueOnHeap();
            }
        }
        public int Index
        {
            get => index;
            set
            {
                index = value;
                UpdateValueOnHeap();
            }
        }

        public bool IsInvalid => index < 0;

        public bool IsRoot => Index is 0;
        public int Level => BitOperations.Log2((uint)Index + 1) + 1;

        public int ParentIndex => (Index - 1) / 2;
        public int LeftChildIndex => Index * 2 + 1;
        public int RightChildIndex => LeftChildIndex + 1;

        public bool HasParent => ParentIndex >= 0;
        public bool HasLeftChild => LeftChildIndex < Heap.EntryCount;
        public bool HasRightChild => RightChildIndex < Heap.EntryCount;
        public bool HasAnyChild => HasLeftChild;

        public Node(TValue value, int index, BinaryHeap<TValue> heap)
        {
            this.value = value;
            this.index = index;
            Heap = heap;
        }

        private void UpdateValueOnHeap()
        {
            Heap.SetValue(index, value);
        }

        public Node GetParent() => Heap.GetNode(ParentIndex);
        public Node GetLeftChild() => Heap.GetNode(LeftChildIndex);
        public Node GetRightChild() => Heap.GetNode(RightChildIndex);

        public Node GetHighestPriorityChild()
        {
            if (!HasLeftChild)
                return CreateInvalid();

            if (!HasRightChild)
                return GetLeftChild();

            var leftChild = GetLeftChild();
            var rightChild = GetRightChild();

            var highest = Heap.TopNodeInequality.HighestPriorityValue(leftChild.value, rightChild.value);
            return highest.Equals(leftChild.value) ? leftChild : rightChild;
        }

        public void SwapWithParent()
        {
            var parent = GetParent();
            SwapWithNode(ref parent);
        }
        public void SwapWithNode(ref Node other)
        {
            int otherIndex = other.Index;
            other.Index = Index;
            Index = otherIndex;
        }

        public static Node CreateInvalid()
        {
            return new(default!, -1, null!);
        }
    }
}

public class MinHeap<TValue> : BinaryHeap<TValue>
    where TValue : IComparable<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Less;

    public TValue? MaxValue => LowestPriorityValue;

    public MinHeap(IComparer<TValue>? comparer = null)
        : base(comparer) { }
}

public class MaxHeap<TValue> : BinaryHeap<TValue>
    where TValue : IComparable<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Greater;

    public TValue? MinValue => LowestPriorityValue;

    public MaxHeap(IComparer<TValue>? comparer = null)
        : base(comparer) { }
}