using Garyon.Extensions;
using Garyon.Objects;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Generation;
using SpatialAccessMethods.QualityAssurance;

namespace SpatialAccessMethods.Tests;

public class SpatialDataTableTests : SpatialDataTableQAContainer
{
    private readonly MapRecordEntryCollection entries = new();

    private const int generatedEntryCount = 1500;
    private const int insertedEntryCount = 8250;

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

    // TODO: Add assertions for the tree node counts satisfying a given range
    // i.e. nodes at 50% capacity  <=  TreeNodeCount  <=  nodes at 100% capacity

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void BulkLoad(int dimensionality)
    {
        GenerateBulkLoad(dimensionality);

        Assert.That(Table.RecordCount, Is.EqualTo(entries.Length));
        Assert.That(Table.IndexTree.Root!.Region, Is.EqualTo(entries.MBR));
        Assert.That(Table.VerifyIntegrity(), Is.True);
    }
    private void GenerateBulkLoad(int dimensionality)
    {
        InitializeGenerate(dimensionality);
        Table.BulkLoad(entries.Entries);
    }
    private void InitializeGenerate(int dimensionality, int entryCount = 0)
    {
        if (entryCount is 0)
        {
            entryCount = generatedEntryCount;
        }

        ClearTable();
        InitializeTable(dimensionality);
        entries.Generate(dimensionality, entryCount);
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
            var neighbors = nnQuery.Perform(Table).ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);
            var sortedByDistance = sortedEntries.Take(neighborCount).ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);

            var maxDistance = neighbors.Max(e => e.Location.DistanceFrom(point));
            var expectedMaxDistance = sortedByDistance.Max(e => e.Location.DistanceFrom(point));
            Assert.That(maxDistance, Is.EqualTo(expectedMaxDistance));
        }
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void RangeQuery(int dimensionality)
    {
        GenerateBulkLoad(dimensionality);

        var mbr = entries.MBR;

        // Sufficiently large number to probabilistically catch multiple cases without killing speed running the tests
        // Currently there is high chance this test fails
        // And when run in parallel that chance is increased
        for (int i = 0; i < 23; i++)
        {
            var a = PointGenerator.Shared.NextWithinRectangle(mbr);
            var b = PointGenerator.Shared.NextWithinRectangle(mbr);
            var range = Rectangle.FromVertices(a, b);
            var containedEntries = entries.Entries.Where(e => range.Contains(e.Location)).ToArray();

            var rangeQuery = new SpatialDataTable<MapRecordEntry>.RangeQuery<Rectangle>(range);
            var containedFromQuery = rangeQuery.Perform(Table).ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);
            Assert.That(containedFromQuery.SetEquals(containedEntries), Is.True);
        }
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void SkylineQuery(int dimensionality)
    {
        GenerateBulkLoad(dimensionality);

        var skylineQuery = new SpatialDataTable<MapRecordEntry>.SkylineQuery(Extremum.Minimum);
        var skylinePoints = skylineQuery.Perform(Table).ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);
        var entrySet = entries.Entries.ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);
        entrySet.ExceptWith(entrySet);

        // Only this check is a sufficient criterion
        // If there were any entries missing from the skyline, their domination would be resolved as indeterminate
        foreach (var entry in entrySet)
        {
            foreach (var skylinePoint in skylinePoints)
            {
                var domination = skylinePoint.Location.ResolveDomination(entry.Location, Extremum.Minimum);
                Assert.That(domination, Is.EqualTo(Domination.Dominant));
            }
        }
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Insert(int dimensionality)
    {
        InitializeGenerate(dimensionality, insertedEntryCount);

        int insertedEntries = 0;
        AssertCurrentState();

        // Insert first entry
        var firstEntry = entries.Entries.First();
        Table.Add(firstEntry);
        insertedEntries++;
        var currentRootMBR = Rectangle.FromSinglePoint(firstEntry.Location);

        AssertCurrentStateIncludingRootRegion();

        for (int i = 1; i < entries.Length; i++)
        {
            var entry = entries.Entries[i];
            Table.Add(entry);
            insertedEntries++;
            currentRootMBR = currentRootMBR.Expand(entry.Location);

            AssertCurrentStateIncludingRootRegion();
        }

        void AssertCurrentState()
        {
            Assert.That(Table.RecordCount, Is.EqualTo(insertedEntries));
            Assert.That(Table.VerifyIntegrity(), Is.True);
        }
        void AssertCurrentStateIncludingRootRegion()
        {
            AssertCurrentState();
            Assert.That(Table.IndexTree.Root!.Region, Is.EqualTo(currentRootMBR));
        }

        // Assert that the correct entries were added

        var tableEntries = Table.GetAllEntries().ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);
        var generatedEntries = entries.Entries.ToHashSet(MapRecordEntry.DataEqualityComparer.Instance);

        Assert.That(tableEntries.SetEquals(generatedEntries), Is.True);
        var pointedEntryIDs = Tree.GetAllPointedEntryIDs().ToHashSet();
        Assert.That(pointedEntryIDs.SetEquals(tableEntries.Select(e => e.ID)), Is.True);
    }
}
