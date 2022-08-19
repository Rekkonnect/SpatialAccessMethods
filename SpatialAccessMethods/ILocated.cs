namespace SpatialAccessMethods;

public interface ILocated
{
    public Point Location { get; }

#nullable disable
    // TODO: Re-evaluate if avoiding boxing is any useful
    public record struct LocationComparer<TValue, TComparer>(TComparer Comparer) : IComparer<TValue>
        where TValue : ILocated
        where TComparer : IComparer<Point>
    {
        public int Compare(TValue x, TValue y)
        {
            return Comparer.Compare(x.Location, y.Location);
        }
    }
}
