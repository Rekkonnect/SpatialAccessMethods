using SpatialAccessMethods.Utilities;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.FileManagement;

[HeaderProperty<int>(nameof(BlockSizeBytes))]
[HeaderProperty<int>(nameof(BlockCount))]
[HeaderProperty<int>(nameof(RecordCount))]
[HeaderProperty<int>(nameof(MaxID))]
[HeaderProperty<int>(nameof(MaxTreeNodeID))]
[HeaderProperty<int>(nameof(TreeNodeCount))]
[HeaderProperty<int>(nameof(Dimensionality))]
[HeaderProperty<int>(nameof(RootID))]
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
