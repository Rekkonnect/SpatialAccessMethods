﻿using Garyon.Extensions;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.QualityAssurance;

namespace SpatialAccessMethods.Tests;

public class SpatialDataTableTests : SpatialDataTableQAContainer
{
    private readonly MapRecordEntryCollection entries = new();

    private const int generatedEntryCount = 1500;
    private const int insertedEntryCount = 250;

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
            var neighbors = nnQuery.Perform(Table).ToHashSet();
            var sortedByDistance = sortedEntries.Take(neighborCount).ToHashSet();

            Assert.That(sortedByDistance.SetEquals(neighbors), Is.True);
        }
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Insert(int dimensionality)
    {
        InitializeGenerate(dimensionality, insertedEntryCount);

        var currentRootMBR = new Rectangle();
        int insertedEntries = 0;
        AssertCurrentState();

        // Insert first entry
        var firstEntry = entries.Entries.First();
        Table.Add(firstEntry);
        insertedEntries++;
        currentRootMBR = Rectangle.FromSinglePoint(firstEntry.Location);

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
            Assert.That(Table.HeaderBlock.TreeNodeCount, Is.EqualTo(insertedEntries));
            Assert.That(Table.VerifyIntegrity(), Is.True);
        }
        void AssertCurrentStateIncludingRootRegion()
        {
            AssertCurrentState();
            Assert.That(Table.IndexTree.Root!.Region, Is.EqualTo(currentRootMBR));
        }
    }
}
