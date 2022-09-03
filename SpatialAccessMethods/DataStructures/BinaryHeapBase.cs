using Garyon.Extensions;
using Garyon.Objects;
using SpatialAccessMethods.Utilities;
using System.Collections;
using System.Numerics;

namespace SpatialAccessMethods.DataStructures;

public abstract class BinaryHeapBase<TValue> : IBinaryHeap<TValue>
    where TValue : IComparable<TValue>, IEquatable<TValue>
{
    public abstract ComparisonResult TopNodeInequality { get; }
    public ComparisonKinds TopNodeComparisonKinds => TopNodeInequality.GetComparisonKind();

    public abstract int EntryCount { get; private protected set; }
    public bool IsEmpty => EntryCount is 0;

    public int Height => HeightForEntries(EntryCount);

    public Node Root => GetNode(0);

    public abstract TValue? Pop();
    public abstract void Add(TValue value);
    public abstract void PreserveMaxEntryCount(int maxEntryCount);

    public void EliminateLastLevel()
    {
        int targetHeight = Height - 1;
        PreserveMaxEntryCount(EntriesForHeight(targetHeight));
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

    public abstract Node GetNode(int index);

    protected abstract void SetValue(int index, TValue value);
    
    protected void SwapWithParent(int index)
    {
        SwapWithParent(GetNode(index));
    }
    protected void SwapWithParent(Node node)
    {
        node.SwapWithParent();
    }

    protected bool ShouldElevate(Node child, Node parent)
    {
        return ShouldElevate(child.Value, parent.Value);
    }
    protected bool ShouldElevate(TValue child, TValue parent)
    {
        return child.SatisfiesComparison(parent, TopNodeComparisonKinds);
    }
    protected bool HigherOrEqualPriority(TValue child, TValue parent)
    {
        return child.SatisfiesComparison(parent, TopNodeComparisonKinds | ComparisonKinds.Equal);
    }

    protected void PushUp(Node node)
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

    protected void PushDown(Node node)
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

    public abstract IEnumerator<TValue> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected internal static int HeightForEntries(int entryCount)
    {
        if (entryCount is 0)
            return 0;

        return BitOperations.Log2((uint)entryCount) + 1;
    }
    protected internal static int EntriesForHeight(int height)
    {
        return (1 << height) - 1;
    }

    public struct Node
    {
        public BinaryHeapBase<TValue> Heap { get; }

        private TValue value;
        private int index;

        public TValue Value
        {
            get => value;
            set
            {
                this.value = value;
                UpdateStateOnHeap();
            }
        }
        public int Index
        {
            get => index;
            set
            {
                index = value;
                UpdateStateOnHeap();
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

        public Node(TValue value, int index, BinaryHeapBase<TValue> heap)
        {
            this.value = value;
            this.index = index;
            Heap = heap;
        }

        private void UpdateStateOnHeap()
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
