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
}
