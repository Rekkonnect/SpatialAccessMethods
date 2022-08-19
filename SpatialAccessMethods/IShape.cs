namespace SpatialAccessMethods;

public interface IShape
{
    public int Rank { get; }

    public bool Contains(Point point);
}
