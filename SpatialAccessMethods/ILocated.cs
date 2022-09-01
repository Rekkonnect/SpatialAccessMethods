﻿namespace SpatialAccessMethods;

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

    public interface IDistanceComparer<TValue> : IComparer<TValue>
        where TValue : ILocated
    {
        public Point FocalPoint { get; }

        public sealed int CompareClosest(TValue x, TValue y)
        {
            return x.Location.DistanceFrom(FocalPoint).CompareTo(y.Location.DistanceFrom(FocalPoint));
        }
        public sealed int CompareFurthest(TValue x, TValue y)
        {
            return -CompareClosest(x, y);
        }
    }
    public record ClosestDistanceComparer<TValue>(Point FocalPoint) : IDistanceComparer<TValue>
        where TValue : ILocated
    {
        public int Compare(TValue x, TValue y)
        {
            return (this as IDistanceComparer<TValue>).CompareClosest(x, y);
        }
    }
    public record FurthestDistanceComparer<TValue>(Point FocalPoint) : IComparer<TValue>
        where TValue : ILocated
    {
        public int Compare(TValue x, TValue y)
        {
            return (this as IDistanceComparer<TValue>).CompareFurthest(x, y);
        }
    }
#nullable restore
}
