namespace SpatialAccessMethods.Utilities;

public static class IAsyncEnumeratorExtensions
{
    public static IAsyncEnumeratorLogger<T> WithStoringEnumerated<T>(this IAsyncEnumerator<T> enumerator)
    {
        return new AsyncEnumeratorLogger<T>(enumerator);
    }

    private sealed class AsyncEnumeratorLogger<T> : IAsyncEnumeratorLogger<T>
    {
        private readonly IAsyncEnumerator<T> enumerator;
        private readonly List<T> storedValues = new();

        public T Current => enumerator.Current;

        public AsyncEnumeratorLogger(IAsyncEnumerator<T> enumerator)
        {
            this.enumerator = enumerator;
        }

        public ValueTask DisposeAsync()
        {
            return enumerator.DisposeAsync();
        }
        
        public async ValueTask<bool> MoveNextAsync()
        {
            bool value = await enumerator.MoveNextAsync();
            storedValues.Add(Current);
            return value;
        }

        public T[] GetStoredValues() => storedValues.ToArray();
    }
}
