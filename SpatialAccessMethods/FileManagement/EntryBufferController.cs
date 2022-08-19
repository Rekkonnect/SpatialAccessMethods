using SpatialAccessMethods.Utilities;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.FileManagement;

public abstract class EntryBufferController : ChildBufferController
{
    protected abstract Information EntrySize { get; }

    public virtual int DataOffset => 0;

    public EntryBufferController(string blockFilePath, MasterBufferController masterController)
        : base(blockFilePath, masterController) { }
    public EntryBufferController(ChildBufferController other)
        : base(other) { }
    public EntryBufferController(Stream blockStream, MasterBufferController masterController)
        : base(blockStream, masterController) { }

    protected virtual int GetBlockID(int entryID, out int spanStart)
    {
        int upper = GetEntryPositionOffset(entryID);
        int lower = BlockSize.BytesInt32();
        return Math.DivRem(upper, lower, out spanStart);
    }
    protected virtual int GetEntryPositionOffset(int entryID)
    {
        return entryID * EntrySize.BytesInt32() + DataOffset;
    }

    public void ResizeForEntryCount(int entryCount)
    {
        ResizeFile(NecessaryBlockCount(entryCount));
    }

    public int TotalTableBytes(int entryCount)
    {
        return GetEntryPositionOffset(entryCount);
    }
    public Information TotalTableSize(int entryCount)
    {
        return TotalTableBytes(entryCount).Bytes();
    }
    public int NecessaryBlockCount(int entryCount)
    {
        return TotalTableBytes(entryCount) / BlockSizeBytes;
    }

    public void EnsureLengthForEntry(int entryID)
    {
        int blockID = GetBlockID(entryID, out _);
        EnsureMinimumBlockCount(blockID + 1);
    }

    public Span<byte> LoadDataSpan(int entryID)
    {
        return LoadDataSpan(entryID, out _);
    }
    public Span<byte> LoadDataSpan(int entryID, out DataBlock dataBlock)
    {
        int blockID = GetBlockID(entryID, out int spanStart);
        dataBlock = LoadBlock(blockID);
        return dataBlock.Data.Span.Slice(spanStart, EntrySize.BytesInt32());
    }
}
