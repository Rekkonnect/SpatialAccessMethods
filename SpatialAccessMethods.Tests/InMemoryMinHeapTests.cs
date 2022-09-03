using SpatialAccessMethods.DataStructures;

namespace SpatialAccessMethods.Tests;

public class InMemoryMinHeapTests : IMinHeapTests
{
    private DataStructures.InMemory.MinHeap<int> heap = new();

    IMinHeap<int> IMinHeapTests.Heap => heap;

    [Test]
    public void AddingPopping()
    {
        (this as IMinHeapTests).AddingPopping();
    }
    [Test]
    public void MaxEntryCountReduction()
    {
        (this as IMinHeapTests).MaxEntryCountReduction();
    }
}
