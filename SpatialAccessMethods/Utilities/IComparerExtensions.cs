namespace SpatialAccessMethods.Utilities;

public static class IComparerExtensions
{
    public static IComparer<T> Invert<T>(this IComparer<T> comparer)
    {
        return new InvertedComparer<T>(comparer);
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
