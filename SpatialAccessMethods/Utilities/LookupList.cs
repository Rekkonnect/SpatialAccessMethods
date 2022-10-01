using System.Reflection;

namespace SpatialAccessMethods.Utilities;

public sealed class LookupList<T>
{
    private readonly List<T> list;

    public int MaxIndex => list.Count - 1;

    public LookupList(int capacity = 16)
    {
        list = new(capacity);
    }

    private void EnsureCapacity(int index)
    {
        int requestedLength = index + 1;
        if (requestedLength <= list.Count)
            return;

        list.EnsureCapacity(requestedLength);
        int missingCount = requestedLength - list.Count;
        var repeatedElements = Enumerable.Repeat<T>(default!, missingCount);

        // Arbitrary number used based on estimation of the performance balance
        if (missingCount < 16)
        {
            list.AddRange(repeatedElements);
        }
        else
        {
            CatastrophicallyDangerousListHelpers.Instance.SetSize(list, requestedLength);
        }
    }

    public T this[int index]
    {
        get
        {
            EnsureCapacity(index);
            return list[index];
        }
        set
        {
            EnsureCapacity(index);
            list[index] = value;
        }
    }

    private class CatastrophicallyDangerousListHelpers
    {
        public static CatastrophicallyDangerousListHelpers Instance { get; } = new();

        private const BindingFlags InstaceNonPublic = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string SizeFieldName = "_size";

        private readonly Type type = typeof(List<T>);

        private readonly FieldInfo sizeField;

        private CatastrophicallyDangerousListHelpers()
        {
            sizeField = type.GetField(SizeFieldName, InstaceNonPublic)!;
        }

        // This has to be so fucking slow
        public void SetSize(List<T> list, int size)
        {
            sizeField.SetValue(list, size);
        }
    }
}
