using BenchmarkDotNet.Attributes;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Generation;
using SpatialAccessMethods.QualityAssurance;

namespace SpatialAccessMethods.Benchmarks;

[MemoryDiagnoser]
[IterationCount(50)]
public abstract class SpatialDataTableBenchmark : SpatialDataTableQAContainer
{
    protected readonly MapRecordEntryCollection Entries = new();

    [Params(2, 3, 4)]
    public int Dimensionality { get; set; }
    [Params(1234, 2500, 6123, 15000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public virtual void Setup()
    {
        InitializeTableComponents();
        Entries.Generate(Dimensionality, EntryCount);
        InitializeTable(Dimensionality);
    }

    [IterationSetup]
    public void SetupIteration()
    {
        Tree.Clear();
    }

    protected void PerformQuery(SpatialDataTable<MapRecordEntry>.IQuery query)
    {
        query.Perform(Table);
    }

    protected Point GeneratePointAtDistanceFromCenter(double targetRadiusRatio)
    {
        return PointGenerator.Shared.NextWithinSphereOfRectangle(Entries.MBR, targetRadiusRatio);
    }
}

