using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.Tests;

public class MinHeapTests : FileManagementTestContainer
{
    private MinHeap<int> heap;

    [SetUp]
    public void Setup()
    {
        var blockSize = DatabaseController.DefaultBlockSize;
        heap = CreateMinHeap<int>(blockSize, MasterBufferControlling.Constrained);
    }

    [Test]
    public void AddingValues()
    {
        int[] values = new int[10];
        int previous = 0;
        for (int i = 0; i < values.Length; i++)
        {
            int next = Random.Shared.Next(previous + 1, previous + 5);
            values[i] = next;
            previous = next;
        }

        foreach (var value in values.RandomlyEnumerate())
            heap.Add(value);

        Assert.That(heap.EntryCount, Is.EqualTo(values.Length));
        
        for (int i = 0; i < values.Length; i++)
            Assert.That(heap.Pop(), Is.EqualTo(values[i]));
        
        Assert.That(heap.EntryCount, Is.EqualTo(0));
    }
}
