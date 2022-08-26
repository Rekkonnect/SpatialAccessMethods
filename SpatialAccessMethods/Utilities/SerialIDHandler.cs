using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Utilities;

public class SerialIDHandler
{
    private readonly MinHeap<int> idGapHeap;

    public PropertyDelegates<int>? MaxIDAccessors { get; init; }
    public Func<int>? EntryCountGetter { get; init; }
    public Predicate<int>? ValidIDPredicate { get; init; }

    public SerialIDHandler(MinHeap<int> idGapHeap)
    {
        this.idGapHeap = idGapHeap;
    }

    // NOTE: The handler provides flexible options for when the getters and setters are unavailable,
    //       it would probably be preferrable to enforce their presence. A refactoring would take
    //       place in that scenario.

    private int AllocateNextID(ref int maxID, out bool incrementedMaxID)
    {
        if (idGapHeap.IsEmpty)
        {
            int id = ++maxID;
            incrementedMaxID = true;
            return id;
        }

        incrementedMaxID = false;
        return idGapHeap.Pop();
    }
    private int AllocateNextID(ref int maxID, EntryBufferController entryBufferController, out bool incrementedMaxID)
    {
        int nextID = AllocateNextID(ref maxID, out incrementedMaxID);
        if (incrementedMaxID)
            entryBufferController.EnsureLengthForEntry(nextID);

        return nextID;
    }

    public int AllocateNextID(EntryBufferController entryBufferController)
    {
        int maxID = GetMaxID();
        int nextID = AllocateNextID(ref maxID, entryBufferController, out bool incrementedMaxID);

        if (incrementedMaxID)
            SetMaxID(maxID);

        return nextID;
    }

    public void DeallocateID(EntryBufferController entryBufferController, int id)
    {
        int maxID = GetMaxID();
        if (id == maxID)
        {
            maxID = ScanAssignPreviousMaxID();
        }
        entryBufferController.ResizeForEntryCount(maxID);
    }

    public int CurrentGapCount() => GetMaxID() - GetEntryCount();

    public void PreserveMaxCapableIDGaps()
    {
        PreserveMaxCapableIDGaps(CurrentGapCount());
    }

    public void PreserveMaxCapableIDGaps(int maxGaps)
    {
        idGapHeap.PreserveMaxEntryCount(maxGaps);
    }

    public int ScanAssignPreviousMaxID(Predicate<int>? validID = null)
    {
        validID ??= ValidIDPredicate!;
        int max = GetMaxID();
        FindPreviousMaxID(ref max, validID);
        SetMaxID(max);
        return max;
    }

    public void Clear()
    {
        idGapHeap.Clear();
    }

    private int GetEntryCount() => EntryCountGetter!();
    private int GetMaxID() => MaxIDAccessors!.Getter();
    private void SetMaxID(int maxID) => MaxIDAccessors!.Setter(maxID);

    private static int FindPreviousMaxID(ref int max, Predicate<int> validID)
    {
        while (true)
        {
            max--;
            if (validID(max))
                break;
        }

        return max;
    }
}
