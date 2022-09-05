using BenchmarkDotNet.Attributes;
using SpatialAccessMethods.Generation;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.Benchmarks;

[MemoryDiagnoser]
public class RectangleRangeQueryBenchmark : RangeQueryBenchmark<Rectangle>
{
    // Additional parameterization of relative coordinate variance (how close to a hypercube the generated rectangle is)
    // is ignored so as to reduce complexity of the requirements for finalizing this project
    // These are just benchmarks for randomly generated data, and could be far further extended

    protected override unsafe Rectangle GenerateShape()
    {
        // Get any point within the rectangle
        var sample = PointGenerator.Shared.NextWithinRectangle(Entries.MBR);

        double maxMultiplier = Dimensionality * 0.86;
        double minMultiplier = 1 / maxMultiplier;

        // Generate a random point that acts as the positive offset such that its volume ratio equals the given
        Span<double> sideRatios = stackalloc double[Dimensionality];
        double totalRatio = 1;
        for (int i = 0; i < sideRatios.Length - 1; i++)
        {
            double ratio = AdvancedRandomDLC.Shared.NextDouble(minMultiplier, maxMultiplier);
            sideRatios[i] = ratio;
            totalRatio *= ratio; 
        }
        // Statistically this should not be outside the normal range we have defined
        // But it should not pose any problems given that this is one arbitrary distribution scenario we're testing
        // And should barely skew the results (no thesis available for that conclusion)

        // Eventually, all the side ratios must multiply to 1
        sideRatios[^1] = 1 / totalRatio;

        var offset = new double[Dimensionality];
        var targetVolume = MBRVolumeRatio * Entries.MBR.Volume;
        var masterSideMultiplier = Math.Pow(targetVolume, 1D / Dimensionality);
        for (int i = 0; i < Dimensionality; i++)
        {
            offset[i] = sideRatios[i] * masterSideMultiplier;
        }

        var nextPoint = sample + new Point(offset);
        return Rectangle.FromVertices(sample, nextPoint);
    }
}
