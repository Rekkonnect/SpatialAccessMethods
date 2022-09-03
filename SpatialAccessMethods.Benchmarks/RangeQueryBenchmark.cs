using BenchmarkDotNet.Attributes;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Benchmarks;

public abstract class RangeQueryBenchmark<TShape> : SpatialDataTableBenchmark
    where TShape : IShape, IOverlappableWith<Rectangle>
{
    protected TShape Shape;

    [Params(0.07, 0.13, 0.16, 0.24, 0.35, 0.5, 0.76)]
    public double MBRVolumeRatio { get; set; }

    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        Shape = GenerateShape();
    }

    [Benchmark(Baseline = true)]
    public void RangeQuery()
    {
        PerformQuery(new SpatialDataTable<MapRecordEntry>.RangeQuery<TShape>(Shape));
    }
    [Benchmark]
    public void SerialRangeQuery()
    {

        PerformQuery(new SpatialDataTable<MapRecordEntry>.SerialRangeQuery<TShape>(Shape));
    }

    protected abstract TShape GenerateShape();
}
