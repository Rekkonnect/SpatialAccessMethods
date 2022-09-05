using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using SpatialAccessMethods.Benchmarks;

// TODO: Fix the iteration cycle to last at least 250ms
RunExport<BulkLoadingBenchmark>();
RunExport<RectangleRangeQueryBenchmark>();
RunExport<SphereRangeQueryBenchmark>();
RunExport<NearestNeighborQueryBenchmark>();
RunExport<SkylineQueryBenchmark>();

static void RunExport<TBenchmark>()
{
    var summary = BenchmarkRunner.Run<TBenchmark>();
    var files = DefaultExporters.Csv.ExportToFiles(summary, NullLogger.Instance)
        .Select(f => new FileInfo(f));
    foreach (var file in files)
    {
        // TODO: Change the file to something friendlier 
        var newName = file.Name;
        file.MoveTo(newName);
    }
}