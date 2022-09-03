using BenchmarkDotNet.Attributes;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Generation;
using SpatialAccessMethods.QualityAssurance;

namespace SpatialAccessMethods.Benchmarks;

public abstract class SpatialDataTableBenchmark : SpatialDataTableQAContainer
{
    protected readonly MapRecordEntryCollection Entries = new();

    [Params(2, 3, 4)]
    public int Dimensionality { get; set; }
    [Params(500, 1500, 2500, 4850, 8000, 15000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public virtual void Setup()
    {
        InitializeTableComponents();
        Entries.Generate(Dimensionality, EntryCount);
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

