using SpatialAccessMethods.Utilities;
using UnitsNet;

namespace SpatialAccessMethods.FileManagement;

public static class FileManagementHelpers
{
    public static void CreateMissingDirectoryForPath(string filePath)
    {
        new FileInfo(filePath).Directory?.Create();
    }
}

public class ChildBufferController : BufferController, IDisposable
{
    private bool shouldDispose = true;
    public readonly Stream BlockStream;
    
    public MasterBufferController MasterBufferController { get; }

    public virtual Information BlockSize { get; init; }
    public int BlockSizeBytes => BlockSize.BytesInt32();

    public virtual int MandatoryBlocks => 0;

    public ChildBufferController(string blockFilePath, MasterBufferController masterController)
    {
        FileManagementHelpers.CreateMissingDirectoryForPath(blockFilePath);
        BlockStream = File.Open(blockFilePath, FileMode.OpenOrCreate);
        EnsureMinimumBlockCount(0);
        MasterBufferController = masterController;
    }
    public ChildBufferController(ChildBufferController childBufferController)
    {
        BlockStream = childBufferController.BlockStream;
        MasterBufferController = childBufferController.MasterBufferController;
        BlockSize = childBufferController.BlockSize;

        // Since the child is wrapped, avoid closing the file upon disposal
        childBufferController.shouldDispose = false;
    }

    public ChildBufferController(Stream blockStream, MasterBufferController masterController)
    {
        BlockStream = blockStream;
        MasterBufferController = masterController;
    }

    public void EnsureMinimumBlockCount(int minimumBlockCount)
    {
        if (BlockStream.Length < TotalBytes(minimumBlockCount))
        {
            ResizeFile(minimumBlockCount);
        }
    }
    public void ResizeFile(int dataBlockCount)
    {
        BlockStream.SetLength(TotalBytes(dataBlockCount));
    }

    public void MarkDirty<TBlock>(TBlock block)
        where TBlock : IBlock
    {
        var position = block.PositionIn(this);
        MasterBufferController.MarkDirty(position, block);
    }

    private int TotalBytes(int dataBlockCount) => TotalBlocks(dataBlockCount) * BlockSizeBytes;
    private int TotalBlocks(int dataBlockCount) => MandatoryBlocks + dataBlockCount;

    public override void Dispose()
    {
        if (!shouldDispose)
            return;

        BlockStream.Dispose();
    }

    protected void DisableDisposal()
    {
        shouldDispose = false;
    }

    protected void Dump(IBlock block)
    {
        block.WriteToStream(this);
    }
    protected DataBlock LoadUnconstrained(int id)
    {
        return MasterBufferController.LoadUnconstrained(this, id);
    }
    
    public DataBlock LoadBlock(int id)
    {
        return MasterBufferController.Load(this, id);
    }
}
