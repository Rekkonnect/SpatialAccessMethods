using Garyon.Objects.Advanced;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.Generation;

public record PointGenerator(AdvancedRandom Random)
    : Generator(Random)
{
    public static PointGenerator Shared { get; } = new(AdvancedRandomDLC.Shared);

    public Point NextWithinSphereOfRectangle(Rectangle rectangle, double sphereRatio = 1)
    {
        var center = rectangle.Center;
        var centerFromMax = rectangle.MaxPoint - center;
        double maxRadius = centerFromMax.Min;
        double targetRadius = sphereRatio * maxRadius;
        var sample = NextPointWithinUnitSphere(center.Rank);
        double sampleRadius = sample.DistanceFromCenter;
        var sampleMultiplier = targetRadius / sampleRadius;
        var offset = sample * sampleMultiplier;
        return center + offset;
    }
    public unsafe Point NextPointWithinUnitSphere(int rank)
    {
        // This uses the Dropped Coordinates algorithm as proposed in the following paper:
        // http://compneuro.uwaterloo.ca/files/publications/voelker.2017.pdf
        // I have to admit, that's an extremely clever approach

        Span<double> coordinates = stackalloc double[rank + 2];
        for (int i = 0; i < coordinates.Length; i++)
            coordinates[i] = Random.NextDouble();

        double normalization = 0;
        for (int i = 0; i < coordinates.Length; i++)
            normalization += coordinates[i].Square();

        normalization = Math.Sqrt(normalization);

        for (int i = 0; i < coordinates.Length; i++)
            coordinates[i] /= normalization;

        return new(coordinates[2..].ToArray());
    }

    public Point Next(int dimensionality, double coordinateMin, double coordinateMax)
    {
        double[] coordinates = new double[dimensionality];
        for (int i = 0; i < dimensionality; i++)
            coordinates[i] = Random.NextDouble(coordinateMin, coordinateMax);
        return new(coordinates);
    }
    public Point NextWithinRectangle(Rectangle rectangle)
    {
        double[] coordinates = new double[rectangle.Rank];
        for (int i = 0; i < rectangle.Rank; i++)
            coordinates[i] = NextCoordinateWithinRectangle(rectangle, i);
        return new(coordinates);
    }
    public double NextCoordinateWithinRectangle(Rectangle rectangle, int coordinateIndex)
    {
        return Random.NextDouble(rectangle.MinPoint.GetCoordinate(coordinateIndex), rectangle.MaxPoint.GetCoordinate(coordinateIndex));
    }
}
