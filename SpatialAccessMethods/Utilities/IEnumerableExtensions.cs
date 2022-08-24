namespace SpatialAccessMethods.Utilities;

public static class IEnumerableExtensions
{
    /// <summary>Returns the given collection as an array containing the given values, if it already is an array of that type, otherwise a new array is created.</summary>
    /// <typeparam name="T">The type of values stored in the collection.</typeparam>
    /// <param name="values">The values that are to be contained in the resulting array.</param>
    /// <returns>The existing collection instance as an array, if it originally was, otherwise a newly created array containing the given elements.</returns>
    public static T[] ToExistingOrNewArray<T>(this IEnumerable<T> values)
    {
        return values as T[] ?? values.ToArray();
    }

    /// <summary>Converts the given collection to a list with the specified initial capacity.</summary>
    /// <typeparam name="T">The type of values stored in the collection.</typeparam>
    /// <param name="values">The values that are to be contained in the resulting list.</param>
    /// <param name="capacity">The initial capacity of the resulting list.</param>
    public static List<T> ToList<T>(this IEnumerable<T> values, int capacity)
    {
        var list = new List<T>(capacity);
        list.AddRange(values);
        return list;
    }
}
