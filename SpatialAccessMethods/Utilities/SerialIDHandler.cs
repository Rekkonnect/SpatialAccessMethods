using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Utilities;

public class SerialIDHandler
{
    private readonly MinHeap<int> idGapHeap;

    public PropertyDelegates<int>? MaxIDAccessors { get; init; }
    public Func<int>? EntryCountGetter { get; init; }

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
    public int AllocateNextID(ref int maxID, EntryBufferController entryBufferController, out bool incrementedMaxID)
    {
        int nextID = AllocateNextID(ref maxID, out incrementedMaxID);
        if (incrementedMaxID)
            entryBufferController.EnsureLengthForEntry(nextID);

        return nextID;
    }

    public int AllocateNextID(EntryBufferController entryBufferController)
    {
        int maxID = MaxIDAccessors!.Getter();
        int nextID = AllocateNextID(ref maxID, entryBufferController, out bool incrementedMaxID);

        if (incrementedMaxID)
            MaxIDAccessors.Setter(maxID);

        return nextID;
    }

    public int CurrentGapCount() => MaxIDAccessors!.Getter() - EntryCountGetter!();

    public void PreserveMaxCapableIDGaps()
    {
        PreserveMaxCapableIDGaps(CurrentGapCount());
    }

    public void PreserveMaxCapableIDGaps(int maxGaps)
    {
        idGapHeap.PreserveMaxEntryCount(maxGaps);
    }

    public int ScanAssignPreviousMaxID(Predicate<int> validID)
    {
        int max = MaxIDAccessors!.Getter();
        FindPreviousMaxID(ref max, validID);
        MaxIDAccessors.Setter(max);
        return max;
    }

    public void Clear()
    {
        idGapHeap.Clear();
    }

    public static int FindPreviousMaxID(ref int max, Predicate<int> validID)
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
