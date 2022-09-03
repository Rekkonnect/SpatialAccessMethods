using Garyon.Objects;
using SpatialAccessMethods.Utilities;
using System.Diagnostics;

namespace SpatialAccessMethods;

/// <summary>Represents a hyper-rectangle, defined by two edge points.</summary>
public struct Rectangle : IDominable<Rectangle>, IOverlappableWith<Rectangle>, IEquatable<Rectangle>
{
    /// <summary>Gets the min point of the hyper-rectangle.</summary>
    public Point MinPoint { get; }
    
    /// <summary>Gets the max point of the hyper-rectangle.</summary>
    public Point MaxPoint { get; }

    // The points' ranks have been validated to be equal
    /// <summary>Gets the rank of the hyper-rectangle. It is determined by the one point that defines it.</summary>
    public int Rank => MinPoint.Rank;

    // Both should be invalid at the same time
    public bool IsInvalid => MinPoint.IsInvalid;

    /// <summary>Gets the lengths of the hyper-rectangle's dimensions, expressed as a <see cref="Point"/>.</summary>
    /// <returns>A <seealso cref="Point"/>, with each of its coordinates representing the length of this hyper-rectangle in the respective dimension.</returns>
    public Point DimensionLengths => MaxPoint.DifferenceFrom(MinPoint);

    /// <summary>Gets the hyper-volume of the hyper-rectangle, which is the product of all of its dimension lengths.</summary>
    /// <returns>The hyper-volume of the hyper-rectangle.</returns>
    public double Volume => DimensionLengths.CoordinateProduct;
    /// <summary>Gets the margin of the hyper-rectangle, which is the sum of all of its dimension lengths multiplied by 2 ^ rank.</summary>
    /// <returns>The margin of the hyper-rectangle.</returns>
    public double Margin => (1 << (Rank - 1)) * DimensionLengths.CoordinateSum;

    public Point Center => (MaxPoint + MinPoint) / 2;

    private Rectangle(Point min, Point max)
    {
        Debug.Assert(!min.IsInvalid);
        Debug.Assert(!max.IsInvalid);

        MinPoint = min;
        MaxPoint = max;
    }

    public Point ExtremumPoint(Extremum extremum)
    {
        return extremum switch
        {
            Extremum.Minimum => MinPoint,
            Extremum.Maximum => MaxPoint,
            _ => Center,
        };
    }
    public Domination ResolveDomination(Rectangle other, Extremum dominatingExtremum)
    {
        var subordinateExtremum = dominatingExtremum.Opposing();
        var dominatingPoint = ExtremumPoint(dominatingExtremum);
        var subordinatePoint = other.ExtremumPoint(subordinateExtremum);
        return dominatingPoint.ResolveDomination(subordinatePoint, dominatingExtremum);
    }
    public double AbsoluteExtremumDistanceFrom(Point origin, Extremum extremum)
    {
        return ExtremumPoint(extremum).ManhattanDistanceFrom(origin);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rectangle"/> class from a single point. The resulting hyper-rectangle's
    /// dimensions are all 0, and all its vertices are on the exact point that was given.
    /// </summary>
    /// <param name="single">The single point which will define the resulting hyper-rectangle.</param>
    /// <returns>
    /// The hyper-rectangle whose all vertices are on the single given point. Its dimensions are all equal to 0
    /// in size, and its location is on the exact point.
    /// </returns>
    public static Rectangle FromSinglePoint(Point single)
    {
        return new(single, single);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rectangle"/> class from two edge points that define a hyper-rectangle.
    /// The two points that define the hyper-rectangle are such that they draw a diagonal within the hyper-rectangle. In
    /// other words, the two points must have the largest distance two points can have whilst being within the
    /// hyper-rectangle.
    /// </summary>
    /// <param name="a">The one point that defines the one extreme edge of the hyper-rectangle.</param>
    /// <param name="b">The point that defines the respective other extreme edge of the hyper-rectangle.</param>
    public static Rectangle FromVertices(Point a, Point b)
    {
        return CreateForPoints(a, b);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rectangle"/> class from a collection of points that define a
    /// hyper-rectangle.
    /// </summary>
    /// <param name="points">The points that will be contained in or touch the hyper-rectangle.</param>
    /// <returns>The smallest hyper-rectangle that contains the given points.</returns>
    /// <remarks>
    /// If the given points array only contains one point, the resulting hyper-rectangle is the one
    /// whose center is the given point, and has a total hyper-volume of 0.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown in any of the following cases:
    /// <list type="bullet">
    ///     <item>the given points array is empty</item>
    ///     <item>any of the given points' rank does not match the others'</item>
    /// </list>
    /// </exception>
    public static Rectangle CreateForPoints(params Point[] points)
    {
        if (points.Length is 0)
            throw new ArgumentException("There points array should not be empty.");

        if (points.Length is 1)
            return new(points[0], points[0]);

        int rank = points[0].Rank;
        for (int i = 1; i < points.Length; i++)
            if (points[i].Rank != rank)
                throw new ArgumentException("The points' ranks have to be equal.");

        double[] min = new double[rank];
        double[] max = new double[rank];

        for (int dimension = 0; dimension < rank; dimension++)
        {
            min[dimension] = max[dimension] = points[0].GetCoordinate(dimension);

            for (int pointIndex = 1; pointIndex < points.Length; pointIndex++)
            {
                double coordinate = points[pointIndex].GetCoordinate(dimension);

                if (coordinate < min[dimension])
                    min[dimension] = coordinate;
                else if (coordinate > max[dimension])
                    max[dimension] = coordinate;
            }
        }

        var minPoint = new Point(min);
        var maxPoint = new Point(max);
        return new Rectangle(minPoint, maxPoint);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rectangle"/> class from a collection of hyper-rectangles that define a
    /// hyper-rectangle.
    /// </summary>
    /// <param name="rectangles">The rectangles that will be contained in or touch the edges of the hyper-rectangle.</param>
    /// <returns>The smallest hyper-rectangle that contains the given hyper-rectangles.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown in any of the following cases:
    /// <list type="bullet">
    ///     <item>the hyper-rectangles are not at least 1</item>
    ///     <item>any of the given hyper-rectangles' rank does not match the others'</item>
    /// </list>
    /// </exception>
    public static Rectangle CreateForRectangles(params Rectangle[] rectangles)
    {
        if (rectangles.Length < 1)
            throw new ArgumentException("There must be at least one rectangle.");

        if (rectangles.Length is 1)
            return rectangles.First();

        var points = new Point[rectangles.Length * 2];
        for (int i = 0; i < rectangles.Length; i++)
        {
            points[i * 2] = rectangles[i].MinPoint;
            points[i * 2 + 1] = rectangles[i].MaxPoint;
        }
        return CreateForPoints(points);
    }

    public double MinDistanceFrom(Point point)
    {
        // Use a new method for getting the distance from a point
        return (ClosestVertexTo(point) - point).Absolute.DistanceFromCenter;
    }

    public Point FurthestVertexTo(Point point)
    {
        if (point.Rank != Rank)
            throw new ArgumentException("The point's rank must match the rectangle's rank.");

        var coordinates = new double[Rank];

        for (int i = 0; i < Rank; i++)
        {
            coordinates[i] = FurthestVertex(MinPoint.GetCoordinate(i), MaxPoint.GetCoordinate(i), point.GetCoordinate(i));
        }

        return new(coordinates);
    }
    private static T FurthestVertex<T>(T min, T max, T value)
        where T : INumber<T>
    {
        if (value < min)
            return max;
        if (value > max)
            return min;

        T distanceMin = value - min;
        T distanceMax = max - value;
        if (distanceMin < distanceMax)
            return max;

        return min;
    }

    public double LargestDistanceFrom(Point point)
    {
        return FurthestVertexTo(point).DistanceFrom(point);
    }
    public double ShortestDistanceFrom(Point point)
    {
        if (Contains(point))
            return 0;

        return ClosestVertexTo(point).DistanceFrom(point);
    }

    public Point ClosestVertexTo(Point point)
    {
        if (point.Rank != Rank)
            throw new ArgumentException("The point's rank must match the rectangle's rank.");

        var coordinates = new double[Rank];
        
        for (int i = 0; i < Rank; i++)
        {
            coordinates[i] = ClosestVertex(MinPoint.GetCoordinate(i), MaxPoint.GetCoordinate(i), point.GetCoordinate(i));
        }

        return new(coordinates);
    }
    private static T ClosestVertex<T>(T min, T max, T value)
        where T : INumber<T>
    {
        if (value < min)
            return min;
        if (value > max)
            return max;

        T distanceMin = value - min;
        T distanceMax = max - value;
        if (distanceMin < distanceMax)
            return min;

        return max;
    }

    public bool Contains(Point point) => Contains(point, true);
    public bool Contains(Rectangle rectangle) => Contains(rectangle, true);

    /// <summary>Determines whether this hyper-rectangle contains a given point, optionally including being on any edge of the hyper-rectangle.</summary>
    /// <param name="point">The point to determine if it is contained within the hyper-rectangle.</param>
    /// <param name="includeEdges">
    /// Determines whether the point being on any edge of the hyper-rectangle is permitted. If this is <see langword="true"/>, and
    /// and if the point touches any edge, it will still count as if it is contained. Otherwise, for the point to be contained, it
    /// must not touch any edge and be within the hyper-rectangle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the point is contained within the hyper-rectangle. If edges are not included and the
    /// point touches any of the hyper-rectangle's edges, or being outside of it returns <see langword="false"/>. In other
    /// words, all of the point's coordinates must be strictly less than all the max point's coordinates, and
    /// strictly greater than all the min point's coordinates, if the <paramref name="includeEdges"/> parameter is
    /// <see langword="false"/>. Otherwise, all of the point's coordinates must not be greater than any of the max point's
    /// coordinates, and not be less than any of the min point's coordinates.
    /// </returns>
    /// <exception cref="ArgumentException">The given point's rank is not the same as this hyper-rectangle's.</exception>
    public bool Contains(Point point, bool includeEdges)
    {
        return Contains(FromSinglePoint(point), includeEdges);
    }

    /// <summary>Determines whether this hyper-rectangle overlaps with another hyper-rectangle, including its edges and vertices.</summary>
    /// <param name="other">The other hyper-rectangle to determine if it overlaps.</param>
    /// <returns><see langword="true"/> if this hyper-rectangle overlaps with the other, meaning they have common points, otherwise <see langword="false"/>.</returns>
    public bool Overlaps(Rectangle other) => Intersection(other) is not null;

    /// <summary>Determines whether this hyper-rectangle contains another hyper-rectangle, optionally including touching on the edges of the hyper-rectangle.</summary>
    /// <param name="rectangle">The hyper-rectangle to determine if it is contained within the hyper-rectangle.</param>
    /// <param name="includeEdges">
    /// Determines whether the hyper-rectangle being on any edge of the hyper-rectangle is permitted. If this is <see langword="true"/>, and
    /// and if the hyper-rectangle touches any edges, it will still count as if it is contained. Otherwise, for the hyper-rectangle to be contained, it
    /// must not touch any edges and be within the hyper-rectangle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the hyper-rectangle is contained within the hyper-rectangle. If edges are not included and the
    /// hyper-rectangle touches any of the hyper-rectangle's edges, or being outside of it returns <see langword="false"/>. In other
    /// words, all of the hyper-rectangle's coordinates must be strictly less than all the max hyper-rectangle's coordinates, and
    /// strictly greater than all the min hyper-rectangle's coordinates, if the <paramref name="includeEdges"/> parameter is
    /// <see langword="false"/>. Otherwise, all of the hyper-rectangle's coordinates must not be greater than any of the max hyper-rectangle's
    /// coordinates, and not be less than any of the min hyper-rectangle's coordinates.
    /// </returns>
    /// <exception cref="ArgumentException">The two hyper-rectangles' ranks are not the same.</exception>
    public bool Contains(Rectangle rectangle, bool includeEdges)
    {
        int rank = Rank;
        if (rectangle.Rank != rank)
            throw new ArgumentException("The ranks do not match.");

        for (int i = 0; i < rank; i++)
        {
            double innerMinCoordinate = rectangle.MinPoint.GetCoordinate(i);
            double innerMaxCoordinate = rectangle.MaxPoint.GetCoordinate(i);
            double outerMinCoordinate = MinPoint.GetCoordinate(i);
            double outerMaxCoordinate = MaxPoint.GetCoordinate(i);

            if (innerMinCoordinate < outerMinCoordinate)
                return false;
            if (innerMaxCoordinate > outerMaxCoordinate)
                return false;

            if (!includeEdges)
            {
                if (innerMaxCoordinate == outerMaxCoordinate)
                    return false;
                if (innerMinCoordinate == outerMinCoordinate)
                    return false;
            }
        }

        return true;
    }

    /// <summary>Returns an expanded version of this hyper-rectangle that also contains the given point.</summary>
    /// <param name="point">The point to also contain in the expanded hyper-rectangle.</param>
    /// <returns>
    /// A hyper-rectangle that is an expansion of this one that also includes the given point. If the point is
    /// already contained in this hyper-rectangle, this existing instance is returned. Otherwise, a new
    /// hyper-rectangle is created with its dimensions expanded accordingly.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the given point's rank does not match this hyper-rectangle's rank.</exception>
    public Rectangle Expand(Point point)
    {
        if (Contains(point, true))
            return this;

        return CreateForPoints(MinPoint, point, MaxPoint);
    }

    // Really annoying how the code seems copy-pasted
    public Rectangle Union(Rectangle other)
    {
        int rank = Rank;
        if (Rank != other.Rank)
            throw new ArgumentException("The rectangles' ranks have to be equal.");

        double[] min = new double[rank];
        double[] max = new double[rank];

        for (int dimension = 0; dimension < rank; dimension++)
        {
            min[dimension] = Math.Min(MinPoint.GetCoordinate(dimension), other.MinPoint.GetCoordinate(dimension));
            max[dimension] = Math.Max(MaxPoint.GetCoordinate(dimension), other.MaxPoint.GetCoordinate(dimension));
        }

        var minPoint = new Point(min);
        var maxPoint = new Point(max);
        return new Rectangle(minPoint, maxPoint);
    }
    public Rectangle? Intersection(Rectangle other)
    {
        int rank = Rank;
        if (Rank != other.Rank)
            throw new ArgumentException("The rectangles' ranks have to be equal.");

        double[] min = new double[rank];
        double[] max = new double[rank];

        for (int dimension = 0; dimension < rank; dimension++)
        {
            var min0 = min[dimension] = Math.Max(MinPoint.GetCoordinate(dimension), other.MinPoint.GetCoordinate(dimension));
            var max0 = max[dimension] = Math.Min(MaxPoint.GetCoordinate(dimension), other.MaxPoint.GetCoordinate(dimension));

            // Invalid in just one coordinate = no intersection
            if (min0 > max0)
                return null;
        }

        var minPoint = new Point(min);
        var maxPoint = new Point(max);
        return new Rectangle(minPoint, maxPoint);
    }

    public double OverlappingVolume(Rectangle other) => Intersection(other)?.Volume ?? 0;

    public bool Equals(Rectangle other)
    {
        return MinPoint == other.MinPoint
            && MaxPoint == other.MaxPoint;
    }

    public override bool Equals(object? obj)
    {
        return obj is Rectangle rectangle && Equals(rectangle);
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(MinPoint, MaxPoint);
    }

    public static bool operator ==(Rectangle left, Rectangle right) => left.Equals(right);
    public static bool operator !=(Rectangle left, Rectangle right) => !(left == right);

    public record struct ExtremumPointDistanceFromOriginComparer(Extremum Extremum, Point Origin)
        : IComparer<Rectangle>
    {
        public int Compare(Rectangle x, Rectangle y)
        {
            return GetDistanceFromOrigin(x).CompareTo(GetDistanceFromOrigin(y));
        }

        private double GetDistanceFromOrigin(Rectangle rectangle) => rectangle.AbsoluteExtremumDistanceFrom(Origin, Extremum);
    }
}
