using Garyon.Extensions;
using Garyon.Functions;
using Garyon.Objects;
using SpatialAccessMethods.Utilities;
using System.Text;

namespace SpatialAccessMethods;

/// <summary>Represents a point in space of any dimension.</summary>
public struct Point : IEqualityOperators<Point, Point>, IDominable<Point>
{
    private readonly double[] coordinates;

    /// <summary>Gets the rank of the point, which is the number of dimensions of the space it belongs to.</summary>
    public int Rank => coordinates.Length;

    /// <summary>Determines whether this point instance is a dummy invalid one.</summary>
    public bool IsInvalid => coordinates is null;

    /// <summary>Calculates and gets the sum of the point's coordinates.</summary>
    public double CoordinateSum => coordinates.Sum();
    /// <summary>Calculates and gets the product of the point's coordinates.</summary>
    public double CoordinateProduct => coordinates.Product();

    public double DistanceFromCenter
    {
        get
        {
            double sum = 0;

            for (int i = 0; i < Rank; i++)
                sum += Math.Pow(coordinates[i], 2);

            return Math.Sqrt(sum);
        }
    }

    /// <summary>
    /// Gets the absolute version of the point, whose coordinates are the absolute values of the respective coordinates
    /// of this point.
    /// </summary>
    public Point Absolute
    {
        get
        {
            var result = new double[Rank];
            for (int i = 0; i < result.Length; i++)
                result[i] = Math.Abs(coordinates[i]);

            return new Point(result);
        }
    }

    public double Min => coordinates.Min();
    public double Max => coordinates.Max();

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/> class out of an array of coordinates.
    /// </summary>
    /// <param name="coordinates">The coordinates of the point.</param>
    public Point(params double[] coordinates)
    {
        this.coordinates = coordinates.ToArray();
    }

    /// <summary>Gets the coordinate at the specified dimension.</summary>
    /// <param name="dimension">The zero-based index of the dimension to get the coordinate of.</param>
    /// <returns>The coordinate at the specified dimension.</returns>
    public double GetCoordinate(int dimension) => coordinates[dimension];

    /// <summary>Gets the difference from another point.</summary>
    /// <param name="other">The other point to get the difference from.</param>
    /// <returns>The difference from the other point. The difference is calculated as <see langword="this"/> - <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the other point's rank is not the same as this point's rank.</exception>
    public Point DifferenceFrom(Point other)
    {
        int rank = Rank;
        if (other.Rank != rank)
            throw new ArgumentException("The points must have the same rank.");

        double[] difference = new double[rank];

        for (int i = 0; i < rank; i++)
            difference[i] = coordinates[i] - other.coordinates[i];

        return new Point(difference);
    }

    public double DistanceFrom(Point other)
    {
        return DifferenceFrom(other).Absolute.DistanceFromCenter;
    }

    /// <summary>Gets the Manhattan distance from another point.</summary>
    /// <param name="other">The other point to get the Manhattan distance from.</param>
    /// <returns>The Manhattan distance from the other point, where each of the coordinates is the absolute value of the distance.</returns>
    /// <exception cref="ArgumentException">The other point's rank is not the same as this point's rank.</exception>
    public Point ManhattanDistancePointFrom(Point other)
    {
        return DifferenceFrom(other).Absolute;
    }

    public double ManhattanDistanceFrom(Point other) => ManhattanDistancePointFrom(other).CoordinateSum;

    public Domination ResolveDomination(Point other, Extremum dominatingExtremum)
    {
        var result = Domination.Indeterminate;

        int coordinate = 0;
        while (result is Domination.Indeterminate)
        {
            if (coordinate >= Rank)
                return Domination.Indeterminate;

            result = ResolveDomination(other, dominatingExtremum, coordinate);
            coordinate++;

            if (result is not Domination.Indeterminate)
                break;
        }

        while (coordinate < Rank)
        {
            var temporary = ResolveDomination(other, dominatingExtremum, coordinate);
            if (temporary is not Domination.Indeterminate)
            {
                if (temporary != result)
                    return Domination.Indeterminate;
            }

            coordinate++;
        }

        return result;
    }
    private Domination ResolveDomination(Point other, Extremum dominatingExtremum, int coordinate)
    {
        var comparisonResult = GetCoordinate(coordinate).GetComparisonResult(other.GetCoordinate(coordinate));
        
        if (comparisonResult is ComparisonResult.Equal)
            return Domination.Indeterminate;

        if (comparisonResult == dominatingExtremum.TargetComparisonResult())
        {
            return Domination.Dominant;
        }
        
        return Domination.Subordinate;
    }

    public static Point operator +(Point left, Point right)
    {
        int rank = left.Rank;
        if (right.Rank != rank)
            throw new ArgumentException("The points must have the same rank.");

        double[] sum = new double[rank];
        for (int i = 0; i < rank; i++)
            sum[i] = left.coordinates[i] + right.coordinates[i];
        return new(sum);
    }
    
    public static Point operator -(Point left, Point right)
    {
        return left.DifferenceFrom(right);
    }

    public static Point operator +(Point point, double scalar) => ScalarAdd(point, scalar);
    public static Point operator +(double scalar, Point point) => ScalarAdd(point, scalar);

    public static Point operator -(Point left, double right) => left + (-right);
    
    public static Point operator *(Point point, double scalar) => ScalarMultiply(point, scalar);
    public static Point operator *(double scalar, Point point) => ScalarMultiply(point, scalar);
    
    public static Point operator /(Point left, double right)
    {
        // Avoiding the left * (1 / right) expression for precision errors
        double[] sum = new double[left.Rank];
        for (int i = 0; i < left.Rank; i++)
            sum[i] = left.coordinates[i] / right;
        return new(sum);
    }

    private static Point ScalarAdd(Point point, double scalar)
    {
        double[] sum = new double[point.Rank];
        for (int i = 0; i < point.Rank; i++)
            sum[i] = point.coordinates[i] + scalar;
        return new(sum);
    }
    private static Point ScalarMultiply(Point point, double scalar)
    {
        double[] sum = new double[point.Rank];
        for (int i = 0; i < point.Rank; i++)
            sum[i] = point.coordinates[i] * scalar;
        return new(sum);
    }

    public static bool operator ==(Point left, Point right)
    {
        if (left.Rank != right.Rank)
            return false;

        for (int i = 0; i < left.Rank; i++)
            if (left.coordinates[i] != right.coordinates[i])
                return false;

        return true;
    }
    public static bool operator !=(Point left, Point right) => !(left == right);
    
    public override bool Equals(object? obj) => obj is Point point && this == point;
    public bool Equals(Point other) => this == other;

    public override int GetHashCode() => HashCodeFactory.Combine(coordinates);

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append('(');
        for (int i = 0; i < Rank; i++)
        {
            builder.Append(coordinates[i]);
            if (i < Rank - 1)
                builder.Append(", ");
        }
        builder.Append(')');

        return builder.ToString();
    }

    /// <summary>Provides a coordinate comparison behavior that compares <seealso cref="Point"/> instances based on their coordinates on the target dimension.</summary>
    public sealed class CoordinateComparer : IComparer<Point>
    {
        private static readonly LookupList<CoordinateComparer> cachedComparers = new();

        /// <summary>The dimension at which the points' coordinates are compared.</summary>
        public int Dimension { get; }

        private CoordinateComparer(int dimension)
        {
            Dimension = dimension;
        }

        public int Compare(Point x, Point y)
        {
            return x.GetCoordinate(Dimension).CompareTo(y.GetCoordinate(Dimension));
        }

        public static CoordinateComparer ForDimension(int dimension)
        {
            if (dimension < 0)
                throw new ArgumentException("The dimension cannot be negative");

            var cached = cachedComparers[dimension];
            if (cached is null)
            {
                cached = new(dimension);
                cachedComparers[dimension] = cached;
            }
            return cached;
        }
    }
    /// <summary>Provides a coordinate sum comparison behavior that compares <seealso cref="Point"/> instances based on the sum of their coordinates.</summary>
    public sealed class CoordinateSumComparer : IComparer<Point>
    {
        public static CoordinateSumComparer Instance { get; } = new();
        private CoordinateSumComparer() { }

        public int Compare(Point x, Point y)
        {
            return x.CoordinateSum.CompareTo(y.CoordinateSum);
        }
    }
    /// <summary>Provides a coordinate product comparison behavior that compares <seealso cref="Point"/> instances based on the product of their coordinates.</summary>
    public sealed class CoordinateProductComparer : IComparer<Point>
    {
        public static CoordinateProductComparer Instance { get; } = new();
        private CoordinateProductComparer() { }

        public int Compare(Point x, Point y)
        {
            return x.CoordinateProduct.CompareTo(y.CoordinateProduct);
        }
    }

    public record struct OriginDistanceComparer(Point Origin) : IComparer<Point>
    {
        public int Compare(Point x, Point y)
        {
            return OriginDistance(x).CompareTo(OriginDistance(y));
        }
        
        private double OriginDistance(Point point)
        {
            return point.ManhattanDistanceFrom(Origin);
        }
    }
}
