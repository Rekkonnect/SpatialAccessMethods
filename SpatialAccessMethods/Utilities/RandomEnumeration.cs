namespace SpatialAccessMethods.Utilities;

public static class RandomEnumeration
{
    /// <summary>
    /// Randomly enumerates the contents of a collection of instances.
    /// During the random enumeration, the collection is fully enumerated, and its contents are locally stored.
    /// </summary>
    /// <typeparam name="T">The type of values to be enumerated.</typeparam>
    /// <param name="origin">The origin collection of values to randomly enumerate.</param>
    /// <param name="random">
    /// A custom instance of <seealso cref="Random"/> to use when randomly picking the values to enumerate.
    /// If <see langword="null"/> is specified, <seealso cref="Random.Shared"/> is used instead.
    /// </param>
    /// <returns>The same collection of values enumerated randomly, based on the provided <seealso cref="Random"/> instance.</returns>
    public static IEnumerable<T> RandomlyEnumerate<T>(this IEnumerable<T> origin, Random? random = null)
    {
        random ??= Random.Shared;

        var remaining = origin.ToList();
        while (remaining.Any())
        {
            int index = random.Next(0, remaining.Count);
            var value = remaining[index];
            remaining.RemoveAt(index);
            yield return value;
        }
    }

    public static T GetRandom<T>(this IReadOnlyList<T> origin, Random? random = null)
    {
        random ??= Random.Shared;

        int index = random.Next(0, origin.Count);
        return origin[index];
    }
}
