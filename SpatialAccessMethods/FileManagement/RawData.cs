namespace SpatialAccessMethods.FileManagement;

// Not a struct, dodging tons of problems
// at the tiny cost of a few more allocations of far less size than the general case
public sealed class RawData
{
    public Memory<byte> Data { get; }
    public Span<byte> Span => Data.Span;

    public bool Dirty { get; set; }

    public RawData(byte[]? array)
        : this(new Memory<byte>(array)) { }
    public RawData(byte[]? array, int start, int length)
        : this(new Memory<byte>(array, start, length)) { }

    public RawData(Memory<byte> data)
    {
        Data = data;
    }
}

public static class MemoryByteExtensions
{
    public static RawData AsRawData(this Memory<byte> memory) => new(memory);
}
