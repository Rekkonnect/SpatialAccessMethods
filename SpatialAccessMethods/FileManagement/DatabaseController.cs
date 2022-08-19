using SpatialAccessMethods.DataStructures;
using UnitsNet;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.FileManagement;

public sealed class DatabaseController : IDisposable
{
    public static readonly DatabaseController Instance = new();

    // Properties used over readonly fields because initialization would require using them as uninitialized
    public static Information DefaultBlockSize => 32.Kibibytes();
    public static Information DefaultMaxBlockMemory => 256.Mebibytes();

    public static readonly int DefaultMaxBlockCount = (int)(DefaultMaxBlockMemory / DefaultBlockSize);

    public const string DefaultEntryFilePath = "datafile";
    public const string DefaultTreeFilePath = "rtreeindex";
    public const string DefaultEntryIDGapFilePath = "datafile.gap";
    public const string DefaultTreeIDGapFilePath = "rtreeindex.gap";

    private readonly MasterBufferController masterController;
    public readonly SpatialDataTable<MapRecordEntry> Table;

    private DatabaseController()
    {
        masterController = new(DefaultMaxBlockMemory);
        Table = InitializeTable();
    }

    public void Dispose()
    {
        masterController.Dispose();
    }
    
    private SpatialDataTable<MapRecordEntry> InitializeTable()
    {
        var entryBufferController = new RecordEntryBufferController(DefaultEntryFilePath, masterController)
        {
            BlockSize = DefaultBlockSize
        };

        var treeBufferController = new ChildBufferController(DefaultTreeFilePath, masterController)
        {
            BlockSize = DefaultBlockSize
        };

        var recordIDGapHeap = InitializeHeap<int>(DefaultEntryIDGapFilePath);
        var treeIDGapHeap = InitializeHeap<int>(DefaultTreeIDGapFilePath);
        
        return new(entryBufferController, treeBufferController, recordIDGapHeap, treeIDGapHeap);
    }
    private MinHeap<T> InitializeHeap<T>(string fileName)
        where T : unmanaged, INumber<T>
    {
        var controller = new ChildBufferController(fileName, masterController)
        {
            BlockSize = DefaultBlockSize
        };
        return new MinHeap<T>(controller);
    }
}
