using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods;

public interface IRecordSerializable<TSelf>
    where TSelf : IID, IRecordSerializable<TSelf>
{
    public bool IsAlive { get; }

    public static abstract TSelf Parse(Span<byte> span, IHeaderBlock headerInformation);
    public abstract void Write(Span<byte> span, IHeaderBlock headerInformation);

    public abstract void Deallocate(Span<byte> span);

    public static TSelf Parse(Span<byte> span, IHeaderBlock headerInformation, int id)
    {
        var parsed = TSelf.Parse(span, headerInformation);
        parsed.ID = id;
        return parsed;
    }
}
