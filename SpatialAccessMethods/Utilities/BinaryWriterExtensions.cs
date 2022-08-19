namespace SpatialAccessMethods.Utilities;

public static class BinaryWriterExtensions
{
    public static void WriteNullTerminatedString(this BinaryWriter writer, string? s)
    {
        if (s is not null)
            writer.Write(s);
        writer.Write('\0');
    }
}
