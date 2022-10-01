using SpatialAccessMethods.Utilities;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.FileManagement;

[HeaderProperty<int>("BlockSizeBytes")]
[HeaderProperty<int>("BlockCount")]
[HeaderProperty<int>("RecordCount")]
[HeaderProperty<int>("MaxID")]
[HeaderProperty<int>("MaxTreeNodeID")]
[HeaderProperty<int>("TreeNodeCount")]
[HeaderProperty<int, byte>("Dimensionality")]
public partial struct DataHeaderBlock : IHeaderBlock
{
    public RawData Data { get; set; }

    public bool IsDefault => Data is null;

    public Information BlockSize
    {
        get => BlockSizeBytes.Bytes();
        set => BlockSizeBytes = value.BytesInt32();
    }

    public DataHeaderBlock(IBlock block)
    {
        Data = block.Data;
    }

    public static implicit operator DataHeaderBlock(DataBlock block) => new(block);
}
