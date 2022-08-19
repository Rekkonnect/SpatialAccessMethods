namespace SpatialAccessMethods.FileManagement;

public interface IBlock
{
    public int ID { get; }
    
    public RawData Data { get; set; }
}

public static class IBlockExtensions
{
    public static BlockPosition PositionIn(this IBlock block, ChildBufferController controller)
    {
        return new(controller, block.ID);
    }
    public static void WriteToStream(this IBlock block, ChildBufferController controller)
    {
        PositionIn(block, controller).WriteToStream(block);
    }
}
