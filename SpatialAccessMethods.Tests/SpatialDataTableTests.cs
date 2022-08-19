using Garyon.Objects.Advanced;
using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.Tests;

public class SpatialDataTableTests : FileManagementTestContainer
{
    private SpatialDataTable<MapRecordEntry> table;
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
        // Set the dimensionality up
        InitializeTableWithDimensionality(dimensionality);

        // Stressing the computer is fun
        var values = new MapRecordEntry[1500];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = GenerateEntry();
        }

        table.BulkLoad(values);

        Assert.That(table.RecordCount, Is.EqualTo(values.Length));
        Assert.That(table.VerifyIntegrity(), Is.True);

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
}
