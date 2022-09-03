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

    /// <summary>Calculates the square of the given number.</summary>
    /// <param name="number">The number whose square to get.</param>
    /// <returns>The square of the number.</returns>
    public static TNumber Square<TNumber>(this TNumber number)
        where TNumber : INumber<TNumber>
    {
        return number * number;
    }
}
