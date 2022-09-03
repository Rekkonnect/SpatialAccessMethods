using BenchmarkDotNet.Attributes;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Benchmarks;

public class NearestNeighborQueryBenchmark : SpatialDataTableBenchmark
{
    // Arbitrary values are the best
    [Params(0, 0.12, 0.43, 0.5, 0.76, 0.91, 1)]
    public double CenterDistanceRatio { get; set; }

    [Params(5, 18, 25, 72, 143)]
    public int Neighbors { get; set; }

    private Point focalPoint;

    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        focalPoint = GeneratePointAtDistanceFromCenter(CenterDistanceRatio);
    }

    [Benchmark(Baseline = true)]
    public void NearestNeighborQuery()
    {
        PerformQuery(new SpatialDataTable<MapRecordEntry>.NearestNeighborQuery(focalPoint, Neighbors));
    }
    [Benchmark]
    public void SerialNearestNeighborQuery()
    {
        PerformQuery(new SpatialDataTable<MapRecordEntry>.SerialNearestNeighborQuery(focalPoint, Neighbors));
    }
}
