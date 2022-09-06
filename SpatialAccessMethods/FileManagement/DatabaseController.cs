using Garyon.Extensions;
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

    public const string DefaultTableFileName = "datafile";
    public const string DefaultIndexFileName = "index";

    public const string FileNamesFilePath = "db/filenames.txt";

    private readonly MasterBufferController masterController;
    private readonly Lazy<SpatialDataTable<MapRecordEntry>> lazyTable;

    public SpatialDataTable<MapRecordEntry> Table => lazyTable.Value;

    public string TableFileName { get; private set; } = PrependSubdirectory(DefaultTableFileName);
    public string IndexFileName { get; private set; } = PrependSubdirectory(DefaultIndexFileName);

    public string TableIDGapFileName => GetGapFileName(TableFileName);
    public string IndexIDGapFileName => GetGapFileName(IndexFileName);

    private DatabaseController()
    {
        masterController = new(DefaultMaxBlockMemory);
        LoadFileNames();
        lazyTable = new(InitializeTable);
    }

    private void LoadFileNames()
    {
        if (!File.Exists(FileNamesFilePath))
            return;

        var text = File.ReadAllText(FileNamesFilePath);
        var lines = text.GetLines();
        if (lines.Length != 2)
            throw new FileLoadException("The file names file should only consist of two lines specifying the file names of the table file and the index file.");

        TableFileName = lines[0];
        IndexFileName = lines[1];
    }
    private void DumpFileNames()
    {
        File.WriteAllText(FileNamesFilePath, $"{TableFileName}\r\n{IndexFileName}");
    }

    // Changing the file names while the table is loaded is not supported yet
    public void SetFileNames(string tableFileName, string indexFileName)
    {
        TableFileName = tableFileName;
        IndexFileName = indexFileName;
        DumpFileNames();
    }

    public void Dispose()
    {
        masterController.Dispose();
    }
    
    private SpatialDataTable<MapRecordEntry> InitializeTable()
    {
        var tableBufferController = new RecordEntryBufferController(TableFileName, masterController)
        {
            BlockSize = DefaultBlockSize
        };

        var treeBufferController = new ChildBufferController(IndexFileName, masterController)
        {
            BlockSize = DefaultBlockSize
        };

        var recordIDGapHeap = InitializeHeap<int>(TableIDGapFileName);
        var treeIDGapHeap = InitializeHeap<int>(IndexIDGapFileName);
        
        return new(tableBufferController, treeBufferController, recordIDGapHeap, treeIDGapHeap);
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

    private static string GetGapFileName(string baseName) => $"{baseName}.gap";
    private static string PrependSubdirectory(string fileName) => $"db/{fileName}";
}
