using BenchmarkDotNet.Attributes;

namespace SpatialAccessMethods.Benchmarks;

[MemoryDiagnoser]
public class SphereRangeQueryBenchmark : RangeQueryBenchmark<Sphere>
{
    [Params(0, 0.12, 0.43, 0.5, 0.76, 0.91, 1)]
    public double CenterDistanceRatio { get; set; }

    protected override Sphere GenerateShape()
    {
        var center = GeneratePointAtDistanceFromCenter(CenterDistanceRatio);
        var volume = MBRVolumeRatio * Entries.MBR.Volume;
        return Sphere.SphereForVolume(center, Entries.MBR.Rank, volume);
    }
}
