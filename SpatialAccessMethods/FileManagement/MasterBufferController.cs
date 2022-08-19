using SpatialAccessMethods.DataStructures;
using System.Collections;
using System.Threading.Tasks.Dataflow;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.FileManagement;

public sealed class MasterBufferController : BufferController
{
    private readonly Information maxBlockMemory;
    private Information currentBlockMemory;

    private readonly BlockDumpQueueDictionary blockDumpQueueDictionary = new();

    public MasterBufferController(Information blockMemoryLimit)
    {
        maxBlockMemory = blockMemoryLimit;
        currentBlockMemory = 0.Bytes();
    }

    public DataBlock Load(ChildBufferController bufferController, int id)
    {
        return Load(new BlockPosition(bufferController, id));
    }
    public DataBlock Load(BlockPosition loadingPosition)
    {
        bool contained = blockDumpQueueDictionary.TryGetValue(loadingPosition, out var existing);
        if (contained)
            return existing;

        // This does not handle the case that the loading block's size
        // is greater than the limit
        // Should never be the case under normal usage
        var blockSize = loadingPosition.BufferController.BlockSize;
        currentBlockMemory += blockSize;
        DataBlock? loaded = null;

        while (currentBlockMemory >= maxBlockMemory)
        {
            var dumpedPosition = DumpNextBlock();
            if (dumpedPosition is null)
                break;

            loaded = Swap(dumpedPosition, loadingPosition);

            currentBlockMemory -= dumpedPosition.BufferController.BlockSize;
        }
        
        var loadedValue = loaded ?? ReadRegister(loadingPosition);

        return loadedValue;
    }
    
    public DataBlock LoadUnconstrained(ChildBufferController bufferController, int id)
    {
        return LoadUnconstrained(new BlockPosition(bufferController, id));
    }
    public DataBlock LoadUnconstrained(BlockPosition loadingPosition)
    {
        return Read(loadingPosition);
    }

    private BlockPosition? DumpNextBlock()
    {
        var position = blockDumpQueueDictionary.Dequeue();
        
        if (position is not null)
            Dump(position);

        return position;
    }

    private DataBlock Swap(BlockPosition dumpedPosition, BlockPosition loadedPosition)
    {
        return Swap(dumpedPosition, blockDumpQueueDictionary[dumpedPosition], loadedPosition);
    }
    private DataBlock Swap(BlockPosition dumpedPosition, DataBlock dumpedBlock, BlockPosition loadedPosition)
    {
        var dataMemory = dumpedBlock.Data;
        var loadedBlock = new DataBlock(loadedPosition.Index, dataMemory);

        blockDumpQueueDictionary.Remove(dumpedPosition);
        blockDumpQueueDictionary.Add(loadedPosition, loadedBlock);

        Read(loadedPosition, loadedBlock);
        return loadedBlock;
    }

    public void Dump(ChildBufferController dumpedBlockBufferController, int index)
    {
        var position = new BlockPosition(dumpedBlockBufferController, index);
        Dump(position);
    }
    public void Dump(BlockPosition dumpedPosition)
    {
        dumpedPosition.WriteToStream(blockDumpQueueDictionary[dumpedPosition]);
    }

    public void MarkDirty<TBlock>(BlockPosition position, TBlock block)
        where TBlock : IBlock
    {
        block.Data.Dirty = true;
        UpdateBlockState(position, block);
    }
    private void UpdateBlockState<TBlock>(BlockPosition position, TBlock block)
        where TBlock : IBlock
    {
        blockDumpQueueDictionary.Update(position, block);
    }

    public override void Dispose()
    {
        foreach (var blockPosition in blockDumpQueueDictionary)
        {
            Dump(blockPosition);
        }
    }

    private DataBlock ReadRegister(BlockPosition loadingPosition)
    {
        var loadedBlock = Read(loadingPosition);
        blockDumpQueueDictionary.Add(loadingPosition, loadedBlock);
        return loadedBlock;
    }
    private DataBlock Read(BlockPosition loadingPosition)
    {
        // As this is allocated on the heap, it should live until no longer referenced
        var array = GC.AllocateUninitializedArray<byte>(loadingPosition.BufferController.BlockSizeBytes, false);
        return Read(loadingPosition, new RawData(array));
    }
    private DataBlock Read(BlockPosition loadingPosition, RawData blockMemory)
    {
        var block = new DataBlock(loadingPosition.Index, blockMemory);
        Read(loadingPosition, block);
        return block;
    }
    private void Read(BlockPosition loadingPosition, DataBlock block)
    {
        block.Data.Dirty = false;
        loadingPosition.Stream.Seek(loadingPosition.StreamPosition, SeekOrigin.Begin);
        loadingPosition.Stream.Read(block.Data.Span);
    }

    private sealed class BlockDumpQueueDictionary : IEnumerable<BlockPosition>
    {
        private readonly Dictionary<BlockPosition, DataBlock> blocks = new();
        private readonly BlockDumpQueue blockDumpQueue = new();

        public void Add(BlockPosition position, DataBlock block)
        {
            blockDumpQueue.Enqueue(position, block);
            blocks.Add(position, block);
        }
        public void Remove(BlockPosition position)
        {
            bool removed = blocks.Remove(position, out var block);
            if (!removed)
                return;
            
            blockDumpQueue.Remove(position, block);
        }

        public void Update<TBlock>(BlockPosition position, TBlock block)
            where TBlock : IBlock
        {
            blockDumpQueue.Update(position, block);
        }
        
        public BlockPosition? Dequeue()
        {
            return blockDumpQueue.Dequeue();
        }

        public bool TryGetValue(BlockPosition position, out DataBlock existing)
        {
            return blocks.TryGetValue(position, out existing);
        }

        public DataBlock this[BlockPosition position]
        {
            get => blocks[position];
            set => blocks[position] = value;
        }

        public IEnumerator<BlockPosition> GetEnumerator() => blockDumpQueue.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class BlockDumpQueue : IEnumerable<BlockPosition>
    {
        private readonly LinkedQueueSet<BlockPosition> clean = new();
        private readonly LinkedQueueSet<BlockPosition> dirty = new();

        public int Count => clean.Count + dirty.Count;

        public void Enqueue<TBlock>(BlockPosition position, TBlock block)
            where TBlock : IBlock
        {
            GetQueueForBlock(block).Enqueue(position);
        }
        public BlockPosition? Dequeue()
        {
            return GetNextDequeueable().Dequeue();
        }

        public void Remove<TBlock>(BlockPosition position, TBlock block)
            where TBlock : IBlock
        {
            GetQueueForBlock(block).Remove(position);
        }

        public void Update<TBlock>(BlockPosition position, TBlock block)
            where TBlock : IBlock
        {
            bool dirty = block.Data.Dirty;
            var currentQueue = GetQueueForBlock(block);
            var expectedQueue = GetQueueForState(dirty);
            if (currentQueue == expectedQueue)
                return;

            currentQueue.Remove(position);
            expectedQueue.Enqueue(position);
        }

        private LinkedQueueSet<BlockPosition> GetNextDequeueable()
        {
            if (clean.IsEmpty)
                return dirty;

            return clean;
        }

        private LinkedQueueSet<BlockPosition> GetQueueForBlock<TBlock>(TBlock block)
            where TBlock : IBlock
        {
            return GetQueueForState(block.Data.Dirty);
        }
        private LinkedQueueSet<BlockPosition> GetQueueForState(bool isDirty)
        {
            return isDirty switch
            {
                true => dirty,
                false => clean,
            };
        }

        public IEnumerator<BlockPosition> GetEnumerator() => clean.Concat(dirty).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
