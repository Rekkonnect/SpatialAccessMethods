namespace SpatialAccessMethods.Utilities;

public interface IAsyncEnumeratorLogger<T> : IAsyncEnumerator<T>
{
    public abstract T[] GetStoredValues();
}

