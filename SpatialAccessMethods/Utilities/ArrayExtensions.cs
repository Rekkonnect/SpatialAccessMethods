using Microsoft.CodeAnalysis.CSharp.Syntax;

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

    public static void CopyToSafe<T>(this T[] array, Span<T> target)
    {
        if (array is null)
            return;

        if (target.Length < array.Length)
        {
            var arraySpan = new Span<T>(array, 0, target.Length);
            arraySpan.CopyTo(target);
        }
        else
        {
            array.CopyTo(target);
        }
    }
}
