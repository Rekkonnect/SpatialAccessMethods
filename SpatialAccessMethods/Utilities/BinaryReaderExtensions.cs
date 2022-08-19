namespace SpatialAccessMethods.Utilities;

public static class BinaryReaderExtensions
{
    public static string ReadNullTerminatedString(this BinaryReader reader, int charCount)
    {
        var chars = reader.ReadChars(charCount);
        int index = Array.IndexOf(chars, '\0');
        int length = index is -1 ? charCount : index / 2;
        return new string(chars, 0, length);
    }
}
