using Garyon.Extensions;
using Garyon.Objects;

namespace SpatialAccessMethods.Utilities;

public static class ComparisonResultExtensions
{
    public static TValue HighestPriorityValue<TValue>(this ComparisonResult result, TValue left, TValue right)
        where TValue : IComparable<TValue>
    {
        if (left.MatchesComparisonResult(right, result))
            return left;

        return right;
    }
}
