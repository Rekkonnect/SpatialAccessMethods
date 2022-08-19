namespace SpatialAccessMethods.Utilities;

public static class NumericExtensions
{
    /// <summary>Calculates the product of all the given values.</summary>
    /// <param name="values">The collection of values whose product to return.</param>
    /// <returns>The product of the values, or 1 if the collection contains no values.</returns>
    public static TNumber Product<TNumber>(this IEnumerable<TNumber> values)
        where TNumber : INumber<TNumber>
    {
        TNumber result = TNumber.One;
        foreach (TNumber value in values)
            result *= value;
        return result;
    }
}
