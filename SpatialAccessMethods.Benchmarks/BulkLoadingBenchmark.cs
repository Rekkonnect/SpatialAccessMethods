using BenchmarkDotNet.Attributes;

namespace SpatialAccessMethods.Benchmarks;

public class BulkLoadingBenchmark : SpatialDataTableBenchmark
{
    // This benchmark only accounts for the serial loading of the R*-tree
    // There would be no reason to include the overhead of the insertion in the table
    // As it is obvious that storing into the table is O(n) in nature
    // which would in turn skew the comaprison results between bulk and serial loading
    // less favorably for the bulk loading

    [Benchmark(Baseline = true)]
    public void BulkLoad()
    {
        // Alleviate the cost of storing all the entires to an array by providing the raw array
        Tree.BulkLoad(Entries.Entries);
    }
    [Benchmark]
    public void LoadSerially()
    {
        for (int i = 0; i < Entries.Length; i++)
        {
            Tree.Insert(Entries.Entries[i]);
        }
    }
}
