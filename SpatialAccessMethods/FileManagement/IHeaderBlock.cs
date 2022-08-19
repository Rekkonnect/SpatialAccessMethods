using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.FileManagement;

public interface IHeaderBlock : IBlock
{
    int IBlock.ID => 0;

    // Cannot make those protected because DIMs break
    // Damn you, C#
    // https://github.com/dotnet/csharplang/discussions/6171
    public sealed T GetHeaderProperty<T>(int start)
        where T : unmanaged
    {
        return Data.Span.ReadValue<T>(start);
    }
    public sealed void SetHeaderProperty<T>(int start, T value)
        where T : unmanaged, IEqualityOperators<T, T>
    {
        if (GetHeaderProperty<T>(start) == value)
            return;

        Data.Span.WriteValue(value, start);
        Data.Dirty = true;
    }
}

public static class IHeaderBlockExtensions
{
    public static DataBlock AsDataBlock(this IHeaderBlock block)
    {
        return new(block.ID, block.Data);
    }
}
