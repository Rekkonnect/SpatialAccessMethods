using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.QualityAssurance;

namespace SpatialAccessMethods.Tests;

public class MinHeapTests : FileManagementQAContainer, IMinHeapTests
{
    private MinHeap<int> heap;

    IMinHeap<int> IMinHeapTests.Heap => heap;

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
