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

    private void IncreaseEntryCount()
    {
        int newCount = HeaderBlock.RecordCount + 1;
        SetEntryCount(newCount);
    }
    private void DecreaseEntryCount()
    {
        int newCount = HeaderBlock.RecordCount - 1;
        SetEntryCount(newCount);
    }

    private void SetEntryCount(int newCount)
    {
        HeaderBlock.RecordCount = newCount;
        EntryBufferController.EnsureLengthForEntry(newCount);
    }

    public void Add(TValue entry)
    {
        int id = AllocateNextID();

        IncreaseEntryCount();
        WriteEntry(ref entry, id);
        tree.Insert(entry);
    }
    public void Remove(TValue entry)
    {
        FindPreviousMaxID();
        
        DeallocateEntry(entry);
        serialIDHandler.PreserveMaxCapableIDGaps();
        DecreaseEntryCount();
    }

    public bool VerifyIntegrity()
    {
        return tree.VerifyIntegrity();
    }

    public void Clear()
    {
        HeaderBlock.RecordCount = 0;
        HeaderBlock.MaxID = 0;
        EntryBufferController.ResizeForEntryCount(0);
        serialIDHandler.Clear();
        tree.Clear();
    }

    public void BulkLoad(IEnumerable<TValue> entries)
    {
        if (RecordCount is not 0)
            throw new InvalidOperationException("Cannot bulk load a table that already contains records.");

        var entryArray = entries.ToArray();
        HeaderBlock.MaxID = entryArray.Length;
        EntryBufferController.EnsureLengthForEntry(entryArray.Length);
        
        for (int index = 0; index < entryArray.Length; index++)
        {
            int id = index + 1;
            ref var entry = ref entryArray[index];
            WriteEntry(ref entry, id);
            entry.ID = id;
        }

        tree.BulkLoad(entryArray);
        HeaderBlock.RecordCount = entryArray.Length;
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

    private void WriteEntry(ref TValue entry, int id)
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
        return 62 - 4 * dimensionality;
    }

    // TODO: Perhaps extract those outside
    public interface IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table);
    }

    public sealed record SkylineQuery(Extremum DominatingExtremum) : IQuery
    {
        // More complex skyline query parameters are not defined for simplicity
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.tree.SkylineQuery(DominatingExtremum);
        }
    }
    public sealed record NearestNeighborQuery(Point Point, int Neighbors) : IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.tree.NearestNeighborQuery(Point, Neighbors);
        }
    }
    public sealed record RangeQuery<TShape>(TShape Range) : IQuery
        where TShape : IShape, IOverlappableWith<Rectangle>
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.tree.RangeQuery(Range);
        }
    }

    #region Naive Algorithms
    public IEnumerable<TValue> GetAllEntries()
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

    public sealed record SerialRangeQuery<TShape>(TShape Range) : IQuery
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

    public sealed record SerialNearestNeighborQuery(Point Point, int Neighbors) : IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.NearestNeighborQuerySerial(Point, Neighbors);
        }
    }

    private IEnumerable<TValue> SkylineQuerySerial(Extremum domatingExtremum)
    {
        var dominatingEntries = new Dictionary<Point, TValue>();
        foreach (var entry in GetAllEntries())
        {
            foreach (var dominatingLocation in dominatingEntries.Keys.ToList())
            {
                var domination = entry.Location.ResolveDomination(dominatingLocation, domatingExtremum);
                if (domination is Domination.Subordinate)
                    goto continueOuter;

                if (domination is Domination.Dominant)
                {
                    dominatingEntries.Remove(dominatingLocation);
                    // DO NOT BREAK to keep iterating through to remove all dominated points
                }
            }

            // Even if locations are not unique, this will avoid throwing
            dominatingEntries[entry.Location] = entry;

        continueOuter:
            continue;
        }
        return dominatingEntries.Values;
    }

    public sealed record SerialSkylineQuery(Extremum DomatingExtremum) : IQuery
    {
        public IEnumerable<TValue> Perform(SpatialDataTable<TValue> table)
        {
            return table.SkylineQuerySerial(DomatingExtremum);
        }
    }
    #endregion
}
