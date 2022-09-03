using Garyon.Extensions;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.QualityAssurance;

namespace SpatialAccessMethods.Tests;

public class SpatialDataTableTests : SpatialDataTableQAContainer
{
    private readonly MapRecordEntryCollection entries = new();

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        InitializeTableComponents();
    }

    [SetUp]
    public void SetupTest()
    {
        ClearTable();
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void BulkLoad(int dimensionality)
    {
        GenerateBulkLoad(dimensionality);

        Assert.That(Table.RecordCount, Is.EqualTo(entries.Length));
        Assert.That(Table.HeaderBlock.TreeNodeCount, Is.EqualTo(entries.Length));
        Assert.That(Table.IndexTree.Root!.Region, Is.EqualTo(entries.MBR));
        Assert.That(Table.VerifyIntegrity(), Is.True);
    }
    private void GenerateBulkLoad(int dimensionality)
    {
        InitializeTable(dimensionality);

        entries.Generate(dimensionality, 1500);

        Table.BulkLoad(entries.Entries);
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void NearestNeighborQuery(int dimensionality)
    {
        GenerateBulkLoad(dimensionality);

        // Assume that this will be randomly generated
        var point = entries.MBR.Center;

        var ascendingDistanceComparer = new ILocated.ClosestDistanceComparer<MapRecordEntry>(point);
        var sortedEntries = entries.Entries.ToArray().SortBy(ascendingDistanceComparer);
        foreach (var neighborCount in new[] { 12, 14, 29, 58, 124, 178, 341, 589 })
        {
            var nnQuery = new SpatialDataTable<MapRecordEntry>.NearestNeighborQuery(point, neighborCount);
            var neighbors = nnQuery.Perform(Table).ToHashSet();
            var sortedByDistance = sortedEntries.Take(neighborCount).ToHashSet();

            Assert.That(sortedByDistance.SetEquals(neighbors), Is.True);
        }
    }
}
