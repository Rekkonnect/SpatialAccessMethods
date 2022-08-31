using Garyon.Objects;

namespace SpatialAccessMethods.Utilities;

public static class IComparerExtensions
{
    public static IComparer<T> Invert<T>(this IComparer<T> comparer)
    {
        return new InvertedComparer<T>(comparer);
    }

    public static ComparisonResult GetComparisonResult<T>(this IComparer<T> comparer, T left, T right)
    {
        var comparison = comparer.Compare(left, right);
        return comparison switch
        {
            0   => ComparisonResult.Equal,
            < 0 => ComparisonResult.Less,
            _   => ComparisonResult.Greater,
        };
    }
    public static bool MatchesComparisonResult<T>(this IComparer<T> comparer, T left, T right, ComparisonResult result)
    {
        return comparer.GetComparisonResult(left, right) == result;
    }
    public static bool SatisfiesComparison<T>(this IComparer<T> comparer, T left, T right, ComparisonKinds kinds)
    {
        return kinds.Matches(comparer.GetComparisonResult(left, right));
    }

    private sealed class InvertedComparer<T> : IComparer<T>
    {
        private readonly IComparer<T> original;

        public InvertedComparer(IComparer<T> original)
        {
            this.original = original;
        }

        public int Compare(T? x, T? y)
        {
            return original.Compare(y, x);
        }
    }
}
