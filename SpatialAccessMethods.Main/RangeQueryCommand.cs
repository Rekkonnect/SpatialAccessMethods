using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Main;

public abstract class RangeQueryCommand<TShape> : QueryCommand
    where TShape : IShape, IOverlappableWith<Rectangle>
{
    public abstract TShape Range { get; }

    protected override SpatialDataTable<T>.IQuery GetQuery<T>()
    {
        return new SpatialDataTable<T>.RangeQuery<TShape>(Range);
    }
}
