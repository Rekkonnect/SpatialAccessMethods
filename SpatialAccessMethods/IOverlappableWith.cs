namespace SpatialAccessMethods;

public interface IOverlappableWith<TShape> : IShape
{
    public bool Overlaps(TShape shape);
}
