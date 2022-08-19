namespace SpatialAccessMethods.FileManagement;

/// <summary>Represents the most common type of block.</summary>
public struct DataBlock : IBlock
{
    public int ID { get; }

    public RawData Data { get; set; }

    public DataBlock(int id, RawData data)
    {
        ID = id;
        Data = data;
    }
}
