using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Tests;

public class MinHeapTests : FileManagementTestContainer, IMinHeapTests
{
    private MinHeap<int> heap;

    IBinaryHeap<int> IMinHeapTests.Heap => heap;

    [SetUp]
    public void Setup()
    {
        var blockSize = DatabaseController.DefaultBlockSize;
        heap = CreateMinHeap<int>(blockSize, MasterBufferControlling.Constrained);
    }

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
