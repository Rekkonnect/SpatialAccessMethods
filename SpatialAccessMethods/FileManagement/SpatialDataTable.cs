using Garyon.Extensions;
using Garyon.Objects;
using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.FileManagement;

public sealed class SpatialDataTable<TValue>
    where TValue : ILocated, IID, IRecordSerializable<TValue>
{
    private readonly RStarTree<TValue> tree;
    private readonly SerialIDHandler serialIDHandler; 

    public readonly RecordEntryBufferController EntryBufferController;

    public ref DataHeaderBlock HeaderBlock => ref EntryBufferController.HeaderBlock;

    public int RecordCount => HeaderBlock.RecordCount;

    public RStarTree<TValue> IndexTree => tree;

    public SpatialDataTable(RecordEntryBufferController entryBufferController, ChildBufferController treeBufferController, MinHeap<int> recordIDGapHeap, MinHeap<int> treeIDGapHeap)
    {
        int dimensionality = entryBufferController.HeaderBlock.Dimensionality;
        int treeOrder = TreeOrder(dimensionality);

        EntryBufferController = entryBufferController;
        tree = new(treeOrder, treeBufferController, entryBufferController, treeIDGapHeap);

        serialIDHandler = new(recordIDGapHeap)
        {
            MaxIDAccessors = new(GetMaxID, SetMaxID),
            EntryCountGetter = GetEntryCount,
            ValidIDPredicate = IsValidEntry,
        };
    }

    private int GetMaxID() => HeaderBlock.MaxID;
    private void SetMaxID(int maxID) => HeaderBlock.MaxID = maxID;
    private int GetEntryCount() => HeaderBlock.RecordCount;

    public void Add(TValue entry)
    {
        int id = AllocateNextID();

        WriteEntry(entry, id);
        tree.Insert(entry);
    }
    public void Remove(TValue entry)
    {
        FindPreviousMaxID();
        
        DeallocateEntry(entry);
        serialIDHandler.PreserveMaxCapableIDGaps();
    }

    public bool VerifyIntegrity()
    {
        return tree.VerifyIntegrity();
    }

    public void Clear()
    {
        tree.Clear();
    }

    public void BulkLoad(IEnumerable<TValue> entries)
    {
        if (RecordCount is not 0)
            throw new InvalidOperationException("Cannot bulk load a table that already contains records.");

        var entryArray = entries.ToArray();
        HeaderBlock.MaxID = entryArray.Length;
        EntryBufferController.EnsureLengthForEntry(entryArray.Length - 1);
        
        foreach (var indexedEntry in entryArray.WithIndex())
        {
            // Do not further complicate the query; for unexpected behavior would cause unnecessary wasted debugging hours
            WriteEntry(indexedEntry.Current, indexedEntry.Index + 1);
        }

        tree.BulkLoad(entryArray);
        HeaderBlock.RecordCount = entryArray.Length;
    }
    // Fancy, would be good to use somehow
    public async Task BulkLoadAsync(IAsyncEnumerable<TValue> entries)
    {
        if (RecordCount is not 0)
            throw new InvalidOperationException("Cannot bulk load a table that already contains records.");

        var loggable = entries.GetAsyncEnumerator().WithStoringEnumerated();
        int index = 0;
        // Ugly because of miscommuncation between the responsibilities of an enumerable and its enumerator
        while (await loggable.MoveNextAsync())
        {
            // Could be improved, minding when to ensure the length for the entry
            EntryBufferController.EnsureLengthForEntry(index - 1);
            WriteEntry(loggable.Current, index);
            index++;
        }

        HeaderBlock.MaxID = index;

        tree.BulkLoad(loggable.GetStoredValues());
    }

    private int AllocateNextID()
    {
        return serialIDHandler.AllocateNextID(EntryBufferController);
    }

    private void FindPreviousMaxID()
    {
        serialIDHandler.ScanAssignPreviousMaxID(IsValidEntry);
    }

    private bool IsValidEntry(int id)
    {
        return GetEntry(id).IsAlive;
    }

    public TValue GetEntry(int id)
    {
        var span = EntryBufferController.LoadDataSpan(id - 1);
        return IRecordSerializable<TValue>.Parse(span, HeaderBlock, id);
    }

    private void DeallocateEntry(TValue entry)
    {
        var span = GetDataSpan(entry, out var dataBlock);
        EntryBufferController.MarkDirty(dataBlock);
        dataBlock.Data.Dirty = true;
        entry.Deallocate(span);
    }

    private void WriteEntry(TValue entry, int id)
    {
        entry.ID = id;
        WriteEntry(entry);
    }

    private void WriteEntry(TValue entry)
    {
        var span = GetDataSpan(entry, out var dataBlock);
        EntryBufferController.MarkDirty(dataBlock);
        entry.Write(span, HeaderBlock);
    }

    private Span<byte> GetDataSpan(TValue entry, out DataBlock dataBlock)
    {
        return EntryBufferController.LoadDataSpan(entry.ID - 1, out dataBlock);
    }

    private static int TreeOrder(int dimensionality)
    {
        return 62 - 2 * dimensionality;
    }

    // TODO: Perhaps extract those outside
    public interface IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table);
    }

    public record struct SkylineQuery(Extremum DominatingExtremum) : IQuery
    {
        // More complex skyline query parameters are not defined for simplicity
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.tree.SkylineQuery(DominatingExtremum);
        }
    }
    public record struct NearestNeighborQuery(Point Point, int Neighbors) : IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.tree.NearestNeighborQuery(Point, Neighbors);
        }
    }
    public record struct RangeQuery<TShape>(TShape Range) : IQuery
        where TShape : IShape, IOverlappableWith<Rectangle>
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.tree.RangeQuery(Range);
        }
    }

    #region Naive Algorithms
    private IEnumerable<TValue> GetAllEntries()
    {
        int maxID = HeaderBlock.MaxID;
        for (int id = 1; id <= maxID; id++)
        {
            var entry = GetEntry(id);
            if (entry.IsValid)
                yield return entry;
        }
    }

    private IEnumerable<TValue> EntriesInRangeSerial<TShape>(TShape range)
        where TShape : IShape
    {
        return GetAllEntries().Where(entry => range.Contains(entry.Location));
    }

    public record struct SerialRangeQuery<TShape>(TShape Range) : IQuery
        where TShape : IShape, IOverlappableWith<Rectangle>
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.EntriesInRangeSerial(Range);
        }
    }

    private IEnumerable<TValue> NearestNeighborQuerySerial(Point center, int neighbors)
    {
        var comparer = new ILocated.ClosestDistanceComparer<TValue>(center);
        return GetAllEntries()
            .ToArray()
            .SortBy(comparer)
            .Take(neighbors);
    }

    public record struct SerialNearestNeighborQuery(Point Point, int Neighbors) : IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.NearestNeighborQuerySerial(Point, Neighbors);
        }
    }
    #endregion
}
