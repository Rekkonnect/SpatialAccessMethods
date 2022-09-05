using BenchmarkDotNet.Attributes;
using Garyon.Objects;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Benchmarks;

[MemoryDiagnoser]
public class SkylineQueryBenchmark : SpatialDataTableBenchmark
{
    [Benchmark(Baseline = true)]
    public void SkylineQuery()
    {
        PerformQuery(new SpatialDataTable<MapRecordEntry>.SkylineQuery(Extremum.Minimum));
    }
    [Benchmark]
    public void SkylineQuerySerial()
    {
        PerformQuery(new SpatialDataTable<MapRecordEntry>.SerialSkylineQuery(Extremum.Minimum));
    }
}
