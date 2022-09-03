using Garyon.Extensions;
using Garyon.Objects;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using System.Collections;
using System.Numerics;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.DataStructures;

// Although the data structure is generic, it absolutely does not guarantee block boundary safety
// And won't be taken care of, due to not needing to, as for the purposes of this assignment, only int will be used
public abstract class BinaryHeap<TValue> : BinaryHeapBase<TValue>, ISecondaryStorageDataStructure
    where TValue : unmanaged, INumber<TValue>
{
    public HeapEntryBufferController BufferController { get; }

    ChildBufferController ISecondaryStorageDataStructure.BufferController => BufferController;

    private int entryCount;

    public sealed override int EntryCount
    {
        get => entryCount;
        private protected set
        {
            if (entryCount == value)
                return;

            entryCount = value;
            BufferController.ResizeForEntryCount(value);
            GetEntryCountRef(out var dataBlock) = entryCount;
            BufferController.MarkDirty(dataBlock);
        }
    }

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

    public sealed override void Add(TValue value)
    {
        EntryCount++;

        var node = CreateNewNode(value);
        PushUp(node);
    }
    public sealed override TValue Pop()
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

    public sealed override void PreserveMaxEntryCount(int maxEntryCount)
    {
        if (entryCount < maxEntryCount)
            return;

        // This will automatically trim away the last levels
        EntryCount = maxEntryCount;
    }

    // This is unnecessary, given the presence of the above method
    public void EliminateLastLevel()
    {
        int targetHeight = Height - 1;
        EntryCount = EntriesForHeight(targetHeight);
    }

    public void Clear()
    {
        EntryCount = 0;
    }

    protected sealed override void SetValue(int index, TValue value)
    {
        WriteValue(index, value);
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
    public sealed override Node GetNode(int index)
    {
        if (index < 0 || index >= entryCount)
            return Node.CreateInvalid();

        var value = ReadValue(index);
        return new(value, index, this);
    }

    public sealed override IEnumerator<TValue> GetEnumerator()
    {
        return new Enumerator(this);
    }

    private record class Enumerator(BinaryHeap<TValue> Heap) : IEnumerator<TValue>
    {
        private int currentIndex = -1;

        public TValue Current => Heap.GetNode(currentIndex).Value;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            currentIndex++;
            return currentIndex < Heap.entryCount;
        }

        public void Reset()
        {
            currentIndex = -1;
        }

        void IDisposable.Dispose() { }
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
}

public class MinHeap<TValue> : BinaryHeap<TValue>, IMinHeap<TValue>
    where TValue : unmanaged, INumber<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Less;

    public MinHeap(ChildBufferController bufferController)
        : base(bufferController) { }
}

public class MaxHeap<TValue> : BinaryHeap<TValue>, IMaxHeap<TValue>
    where TValue : unmanaged, INumber<TValue>
{
    public override ComparisonResult TopNodeInequality => ComparisonResult.Greater;

    public MaxHeap(ChildBufferController bufferController)
        : base(bufferController) { }
}
