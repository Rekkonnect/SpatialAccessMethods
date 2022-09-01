using Garyon.Extensions;
using Garyon.Extensions.ArrayExtensions;
using Garyon.Objects.Advanced;
using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using UnitsNet.NumberExtensions.NumberToVolume;

namespace SpatialAccessMethods.Tests;

public class SpatialDataTableTests : FileManagementTestContainer
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private SpatialDataTable<MapRecordEntry> table;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private RecordEntryBufferController entryBufferController;
    private ChildBufferController treeBufferController;
    private MinHeap<int> recordIDHeap, treeIDHeap;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var entryStream = new MemoryStream(DatabaseController.DefaultBlockSize.BytesInt32());
        var treeStream = new MemoryStream(DatabaseController.DefaultBlockSize.BytesInt32());
        
        treeBufferController = new ChildBufferController(treeStream, MasterBufferControlling.Constrained)
        {
            BlockSize = DatabaseController.DefaultBlockSize
        };
        entryBufferController = new RecordEntryBufferController(entryStream, MasterBufferControlling.Constrained)
        {
            BlockSize = DatabaseController.DefaultBlockSize
        };
        entryBufferController.HeaderBlock.BlockSize = DatabaseController.DefaultBlockSize;

        recordIDHeap = CreateMinHeap<int>(DatabaseController.DefaultBlockSize, MasterBufferControlling.Constrained);
        treeIDHeap = CreateMinHeap<int>(DatabaseController.DefaultBlockSize, MasterBufferControlling.Constrained);
    }

    [SetUp]
    public void SetupTest()
    {
        table?.Clear();
    }
    private void InitializeTableWithDimensionality(int dimensionality)
    {
        entryBufferController.HeaderBlock.Dimensionality = dimensionality;
        InitializeTable();
    }
    private void InitializeTable()
    {
        table = new(entryBufferController, treeBufferController, recordIDHeap, treeIDHeap);
    }

    private static readonly string[] exampleNames =
    {
        "Another",
        "White Another",
        "Black Another",
        "EX",
        "GRAVITY",
        "HEAVENLY",
        "BLASTIX RIOTZ",
    };
    private static readonly AdvancedRandom advancedRandom = new();

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void BulkLoad(int dimensionality)
    {
        BulkLoad(dimensionality, out var entries, out var outermostMBR);

        Assert.That(table.RecordCount, Is.EqualTo(entries.Length));
        Assert.That(table.HeaderBlock.TreeNodeCount, Is.EqualTo(entries.Length));
        Assert.That(table.IndexTree.Root!.Region, Is.EqualTo(outermostMBR));
        Assert.That(table.VerifyIntegrity(), Is.True);
    }
    private void BulkLoad(int dimensionality, out MapRecordEntry[] entries, out Rectangle outermostMBR)
    {
        // Set the dimensionality up
        InitializeTableWithDimensionality(dimensionality);

        // Stressing the computer is fun
        entries = new MapRecordEntry[1500];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = GenerateEntry();
        }

        outermostMBR = Rectangle.CreateForPoints(entries.Select(e => e.Location).ToArray());

        table.BulkLoad(entries);

        MapRecordEntry GenerateEntry()
        {
            return new(GeneratePoint(), GenerateName());
        }
        Point GeneratePoint()
        {
            double[] coordinates = new double[dimensionality];
            for (int i = 0; i < dimensionality; i++)
                coordinates[i] = GenerateCoordinate();
            return new(coordinates);
        }
        static double GenerateCoordinate()
        {
            return advancedRandom.NextSingle(-1000, 1000);
        }
        static string? GenerateName()
        {
            bool isNull = advancedRandom.NextBoolean();
            if (isNull)
                return null;

            return exampleNames.GetRandom(advancedRandom);
        }
    }

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void NearestNeighborQuery(int dimensionality)
    {
        BulkLoad(dimensionality, out var entries, out var outermostMBR);

        // Assume that this will be randomly generated
        var point = outermostMBR.Center;
        // TODO: Provide generation methods for Point and Rectangle

        var ascendingDistanceComparer = new ILocated.ClosestDistanceComparer<MapRecordEntry>(point);
        var sortedEntries = entries.ToArray().SortBy(ascendingDistanceComparer);
        foreach (var neighborCount in new[] { 12, 14, 29, 58, 124, 178, 341, 589 })
        {
            var nnQuery = new SpatialDataTable<MapRecordEntry>.NearestNeighborQuery(point, neighborCount);
            var neighbors = nnQuery.Perform(table).ToHashSet();
            var sortedByDistance = sortedEntries.Take(neighborCount).ToHashSet();

            Assert.That(sortedByDistance.SetEquals(neighbors), Is.True);
        }
    }
}
