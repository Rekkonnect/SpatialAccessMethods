using SpatialAccessMethods.Generation;

namespace SpatialAccessMethods.Benchmarks;

public class RectangleRangeQueryBenchmark : RangeQueryBenchmark<Rectangle>
{
    protected override Rectangle GenerateShape()
    {
        var sample = PointGenerator.Shared.NextWithinRectangle(Shape);

        // Generate a random point that acts as the positive offset such that its volume ratio equals the given
        var coordinates = new double[sample.Rank];
        for (int i = 0; i < coordinates.Length; i++)
        {
            // TODO
        }

        var nextPoint = new Point(coordinates);
        return Rectangle.FromVertices(sample, nextPoint);
    }
}
