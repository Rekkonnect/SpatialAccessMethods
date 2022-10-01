namespace SpatialAccessMethods;

public interface ILocated
{
    public Point Location { get; }

#nullable disable
    public sealed record LocationComparer<TValue, TComparer>(TComparer Comparer) : IComparer<TValue>
        where TValue : ILocated
        where TComparer : IComparer<Point>
    {
        public int Compare(TValue x, TValue y)
        {
            return Comparer.Compare(x.Location, y.Location);
        }
    }

    public abstract record DistanceComparer<TValue>(Point FocalPoint) : IComparer<TValue>
        where TValue : ILocated
    {
        public abstract int Compare(TValue a, TValue b);

        protected int CompareClosest(TValue x, TValue y)
        {
            return x.Location.DistanceFrom(FocalPoint).CompareTo(y.Location.DistanceFrom(FocalPoint));
        }
        protected int CompareFurthest(TValue x, TValue y)
        {
            return -CompareClosest(x, y);
        }
    }
    public record ClosestDistanceComparer<TValue>(Point FocalPoint) : DistanceComparer<TValue>(FocalPoint)
        where TValue : ILocated
    {
        public override int Compare(TValue x, TValue y)
        {
            return CompareClosest(x, y);
        }
    }
    public record FurthestDistanceComparer<TValue>(Point FocalPoint) : DistanceComparer<TValue>(FocalPoint)
        where TValue : ILocated
    {
        public override int Compare(TValue x, TValue y)
        {
            return CompareFurthest(x, y);
        }
    }
#nullable restore
}
