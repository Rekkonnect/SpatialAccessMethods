namespace SpatialAccessMethods;

/// <summary>Repersents a geometric shape.</summary>
public interface IShape
{
    /// <summary>The dimensionality of the shape.</summary>
    public int Rank { get; }

    /// <summary>Determines whether this shape contains the given point.</summary>
    /// <param name="point">The point to determine whether it is contained in the shape.</param>
    /// <returns>Whether the point is contained in this shape.</returns>
    public bool Contains(Point point);
}
