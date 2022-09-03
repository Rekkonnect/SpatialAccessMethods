using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.Tests;

public interface IMinHeapTests
{
    public IMinHeap<int> Heap { get; }

    [Test]
    public sealed void AddingPopping()
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
        {
            Heap.Add(value);
            Assert.That(Heap.ValidateStructure(), Is.True);
        }

        Assert.That(Heap.EntryCount, Is.EqualTo(values.Length));
        Assert.That(Heap.ValidateStructure(), Is.True);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.That(Heap.Pop(), Is.EqualTo(values[i]));
            Assert.That(Heap.ValidateStructure(), Is.True);
        }

        Assert.That(Heap.EntryCount, Is.EqualTo(0));
        Assert.That(Heap.ValidateStructure(), Is.True);
    }

    [Test]
    public sealed void MaxEntryCountReduction()
    {
        const int entryCount = 172;
        const int reducedEntryCount0 = (int)(entryCount * 0.72);
        const int reducedEntryCount1 = (int)(entryCount * 0.41);
        const int reducedEntryCount2 = (int)(entryCount * 0.36);

        var values = Enumerable.Range(0, entryCount * 3).RandomlyEnumerate().Take(entryCount).ToHashSet();

        // This could be shortened to adding a range of values
        // Or possibly bulk loading the heap with a batch of them
        // Didn't find the reason and time to implement that
        foreach (var value in values)
        {
            Heap.Add(value);
        }

        Assert.That(Heap.EntryCount, Is.EqualTo(entryCount));
        // Assume that the heap is properly structured

        var targetReductionSuperset = values;

        AssertReduction(reducedEntryCount0);
        AssertReduction(reducedEntryCount1);
        AssertReduction(reducedEntryCount2);

        void AssertReduction(int reducedCount)
        {
            Heap.PreserveMaxEntryCount(reducedCount);
            var reducedSubset = Heap.ToHashSet();
            Assert.That(Heap.EntryCount, Is.EqualTo(reducedCount));
            Assert.That(Heap.ValidateStructure(), Is.True);
            Assert.That(reducedSubset.IsProperSubsetOf(targetReductionSuperset), Is.True);

            targetReductionSuperset = reducedSubset;
        }
    }
}
