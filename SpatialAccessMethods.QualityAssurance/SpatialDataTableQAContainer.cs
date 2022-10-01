using Garyon.Objects.Advanced;
using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Generation;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.QualityAssurance;

public abstract class SpatialDataTableQAContainer : FileManagementQAContainer
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected SpatialDataTable<MapRecordEntry> Table;
    protected RecordEntryBufferController EntryBufferController;
    protected ChildBufferController TreeBufferController;
    protected MinHeap<int> RecordIDHeap, TreeIDHeap;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    protected RStarTree<MapRecordEntry> Tree => Table.IndexTree;

    protected void InitializeTableComponents()
    {
        var entryStream = CreateMemoryStream(DatabaseController.DefaultBlockSize.BytesInt32());
        var treeStream = CreateMemoryStream(DatabaseController.DefaultBlockSize.BytesInt32());

        TreeBufferController = new ChildBufferController(treeStream, MasterBufferControlling.Constrained)
        {
            BlockSize = DatabaseController.DefaultBlockSize
        };
        EntryBufferController = new RecordEntryBufferController(entryStream, MasterBufferControlling.Constrained)
        {
            BlockSize = DatabaseController.DefaultBlockSize
        };
        EntryBufferController.HeaderBlock.BlockSize = DatabaseController.DefaultBlockSize;

        RecordIDHeap = CreateMinHeap<int>(DatabaseController.DefaultBlockSize, MasterBufferControlling.Constrained);
        TreeIDHeap = CreateMinHeap<int>(DatabaseController.DefaultBlockSize, MasterBufferControlling.Constrained);
    }

    protected void ClearTable()
    {
        Table?.Clear();
    }

    protected void InitializeTable(int dimensionality)
    {
        EntryBufferController.HeaderBlock.Dimensionality = dimensionality;
        InitializeTable();
    }
    private void InitializeTable()
    {
        Table = new(EntryBufferController, TreeBufferController, RecordIDHeap, TreeIDHeap);
    }

    protected class MapRecordEntryCollection
    {
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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MapRecordEntry[] Entries { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Rectangle MBR { get; private set; }

        public int Length => Entries.Length;

        public void Generate(int dimensionality, int count)
        {
            Entries = new MapRecordEntry[count];
            for (int i = 0; i < count; i++)
                Entries[i] = GenerateEntry(dimensionality);

            MBR = Rectangle.CreateForPoints(Entries.Select(e => e.Location).ToArray());
        }

        private static MapRecordEntry GenerateEntry(int dimensionality)
        {
            return MapRecordEntryGenerator.Shared.Next(dimensionality, -1000, 1000, exampleNames);
        }
    }
}
