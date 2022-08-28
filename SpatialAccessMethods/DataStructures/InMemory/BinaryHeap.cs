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
    // The comparer is not currently used, but could be in the future
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

    protected BinaryHeap(IComparer<TValue>? comparer = null)
    {
        comparer ??= Comparer<TValue>.Default;
        this.comparer = comparer;
    }

    public void Add(TValue value)
    {
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

        return poppedValue;
    }

    public void PreserveMaxEntryCount(int maxEntryCount)
    {
        if (entryCount < maxEntryCount)
            return;

        // This will automatically trim away the last levels
        EntryCount = maxEntryCount;
    }

    // This is unnecessary, given the presence of the above method
    public void EliminateLastLevel()
    {
        int targetLevel = Height - 1;
        EntryCount = MaxEntriesForHeap(targetLevel);
    }

    private static int MaxEntriesForHeap(int levels)
    {
        return 1 << levels - 1;
    }

    public void Clear()
    {
        EntryCount = 0;
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
        return child.SatisfiesComparison(parent, TopNodeComparisonKinds);
    }
    private bool HigherOrEqualHierarchy(TValue child, TValue parent)
    {
        return child.SatisfiesComparison(parent, TopNodeComparisonKinds | ComparisonKinds.Equal);
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

    public IEnumerator<TValue> GetEnumerator() => contents.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class HeapArray : IEnumerable<TValue>
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
            contents.CopyTo(newContents.AsSpan());
            contents = newContents;
        }

        public void SetNode(Node node)
        {
            this[node.Index] = node.Value;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            // .NET remind me why the fuck this is a thing
            return ((IEnumerable<TValue>)contents).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

    public MinHeap(IComparer<TValue>? comparer = null)
        : base(comparer) { }
}

public class MaxHeap<TValue> : BinaryHeap<TValue>
    where TValue : IComparable<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Greater;

    public MaxHeap(IComparer<TValue>? comparer = null)
        : base(comparer) { }
}