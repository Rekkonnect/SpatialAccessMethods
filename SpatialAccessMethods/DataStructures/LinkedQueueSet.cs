using System.Collections;

namespace SpatialAccessMethods.DataStructures;

public class LinkedQueueSet<T> : IEnumerable<T>
    where T : notnull
{
    private readonly LinkedList<T> list = new();
    private readonly Dictionary<T, LinkedListNode<T>> nodeDictionary;

    public int Count => list.Count;
    public bool IsEmpty => Count is 0;

    public LinkedQueueSet()
        : this(16) { }
    public LinkedQueueSet(int capacity)
    {
        nodeDictionary = new(capacity);
    }

    public bool Enqueue(T value)
    {
        if (nodeDictionary.ContainsKey(value))
            return false;

        var node = list.AddLast(value);
        nodeDictionary.Add(value, node);
        return true;
    }
    public T? Dequeue()
    {
        if (IsEmpty)
            return default!;

        var first = list.First!;
        var value = first.Value;
        list.RemoveFirst();
        nodeDictionary.Remove(value);
        return value;
    }

    public bool Remove(T value)
    {
        nodeDictionary.TryGetValue(value, out var node);
        if (node is null)
            return false;

        list.Remove(node);
        nodeDictionary.Remove(value);
        return true;
    }

    public void Clear()
    {
        list.Clear();
        nodeDictionary.Clear();
    }

    public bool Contains(T item)
    {
        return nodeDictionary.ContainsKey(item);
    }

    public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
