namespace SpatialAccessMethods.FileManagement;

// Ensure that the properties are properly compared
public sealed record BlockPosition(ChildBufferController BufferController, int Index)
{
    public Stream Stream => BufferController.BlockStream;

    public long StreamPosition
    {
        get => (long)Index * BufferController.BlockSizeBytes;
    }
    
    public void WriteToStream(IBlock block)
    {
        if (!block.Data.Dirty)
            return;

        Stream.Seek(StreamPosition, SeekOrigin.Begin);
        Stream.Write(block.Data.Span);
    }

    public bool Equals(BlockPosition? position)
    {
        return Index == position?.Index
            && BufferController.Equals(position?.BufferController);
    }
    public override int GetHashCode()
    {
        return BufferController.GetHashCode() ^ Index;
    }
}
