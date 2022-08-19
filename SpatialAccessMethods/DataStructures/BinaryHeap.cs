using Garyon.Extensions;
using Garyon.Objects;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using System.Numerics;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.DataStructures;

// Although the data structure is generic, it absolutely does not guarantee block boundary safety
// And won't be taken care of, due to not needing to, as for the purposes of this assignment, only int will be used
public abstract class BinaryHeap<TValue> : ISecondaryStorageDataStructure
    where TValue : unmanaged, INumber<TValue>
{
    public HeapEntryBufferController BufferController { get; }

    ChildBufferController ISecondaryStorageDataStructure.BufferController => BufferController;

    public abstract ComparisonResult TopNodeInequality { get; }
    public ComparisonKinds TopNodeComparisonKinds => TopNodeInequality.GetComparisonKind();

    private int entryCount;

    public int EntryCount
    {
        get => entryCount;
        private set
        {
            if (entryCount == value)
                return;

            entryCount = value;
            BufferController.ResizeForEntryCount(value);
            GetEntryCountRef(out var dataBlock) = entryCount;
            BufferController.MarkDirty(dataBlock);
        }
    }
    public Node Root => GetNode(0);

    public int Levels
    {
        get
        {
            if (entryCount is 0)
                return 0;
            
            return BitOperations.Log2((uint)entryCount) + 1;
        }
    }

    public bool IsEmpty => entryCount is 0;

    protected BinaryHeap(ChildBufferController bufferController)
    {
        BufferController = new(bufferController);
        entryCount = GetEntryCountRef(out _);
    }
    
    // WARNING: Do not leak the reference outside the calling context or it will be lost
    private ref int GetEntryCountRef(out DataBlock dataBlock)
    {
        dataBlock = BufferController.LoadBlock(0);
        return ref dataBlock.Data.Span.ValueRef<int>();
    }

    public void Add(TValue value)
    {
        EntryCount++;

        var node = CreateNewNode(value);
        PushUp(node);
    }
    public TValue Pop()
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
        int targetLevel = Levels - 1;
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

    private TValue ReadValue(int index)
    {
        var slice = SliceForIndex(index);
        return slice.ReadValue<TValue>();
    }
    private void WriteValue(int index, TValue value)
    {
        var slice = SliceForIndex(index, out var dataBlock);
        slice.WriteValue(value);
        BufferController.MarkDirty(dataBlock);
    }
    private Node CreateNewNode(TValue value)
    {
        WriteValue(entryCount - 1, value);
        return new(value, entryCount - 1, this);
    }
    private unsafe Span<byte> SliceForIndex(int index)
    {
        return SliceForIndex(index, out _);
    }
    private unsafe Span<byte> SliceForIndex(int index, out DataBlock dataBlock)
    {
        return BufferController.LoadDataSpan(index, out dataBlock);
    }
    public Node GetNode(int index)
    {
        if (index < 0 || index >= entryCount)
            return Node.CreateInvalid();

        var value = ReadValue(index);
        return new(value, index, this);
    }

    public sealed class HeapEntryBufferController : EntryBufferController
    {
        protected override unsafe Information EntrySize => sizeof(TValue).Bytes();

        public override int DataOffset => sizeof(int);
        
        public HeapEntryBufferController(string blockFilePath, MasterBufferController masterController)
            : base(blockFilePath, masterController) { }
        public HeapEntryBufferController(ChildBufferController other)
            : base(other) { }
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
            Heap.WriteValue(index, value);
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
            return highest == leftChild.value ? leftChild : rightChild;
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
            return new(default, -1, null!);
        }
    }
}

public class MinHeap<TValue> : BinaryHeap<TValue>
    where TValue : unmanaged, INumber<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Less;

    public MinHeap(ChildBufferController bufferController)
        : base(bufferController) { }
}

public class MaxHeap<TValue> : BinaryHeap<TValue>
    where TValue : unmanaged, INumber<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Greater;

    public MaxHeap(ChildBufferController bufferController)
        : base(bufferController) { }
}
