namespace SpatialAccessMethods.Utilities;

public static class ArrayExtensions
{
    public static IEnumerable<ArraySegment<T>> ToChunks<T>(this T[] source, int chunkCount)
    {
        var (chunkSize, remainder) = Math.DivRem(source.Length, chunkCount);

        int offset = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            int iterationSize = chunkSize;
            if (i < remainder)
                iterationSize++;

            yield return new(source, offset, iterationSize);
            offset += iterationSize;
        }
    }
}
