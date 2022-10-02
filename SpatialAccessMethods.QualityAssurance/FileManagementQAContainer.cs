using SpatialAccessMethods.DataStructures;
using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using UnitsNet;

namespace SpatialAccessMethods.QualityAssurance;

public abstract class FileManagementQAContainer
{
    protected static MinHeap<T> CreateMinHeap<T>(Information blockSize, MasterBufferController masterController)
        where T : unmanaged, INumber<T>
    {
        var memoryStream = CreateMemoryStream(blockSize.BytesInt32());
        var bufferController = new ChildBufferController(memoryStream, masterController)
        {
            BlockSize = blockSize
        };
        return new MinHeap<T>(bufferController);
    }

    protected static MemoryStream CreateMemoryStream(int bytes)
    {
        return new(bytes);
    }
}

public sealed class ExpandableMemoryStream : Stream
{
    private byte[] contents;
    private long position;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;

    public override long Length => contents.Length;

    public override long Position
    {
        get
        {
            return position;
        }
        set
        {
            Seek(value, SeekOrigin.Begin);
        }
    }

    public ExpandableMemoryStream(int capacity)
    {
        contents = new byte[capacity];
    }

    private void Reallocate(long length)
    {
        var next = new byte[length];
        Array.Copy(contents, next, Math.Min(contents.Length, length));
        contents = next;
    }
    private void EnsureLength(int length)
    {
        if (Length < length)
        {
            Reallocate(length);
        }
    }

    public override void Flush()
    {
        // Do nothing; all our data is in memory
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int requiredLength = offset + count;
        EnsureLength(requiredLength);
        Array.Copy(contents, offset, buffer, 0, count);
        return requiredLength;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                position = Math.Min(Length, offset);
                return position;

            case SeekOrigin.Current:
                position = Math.Min(Length, position + offset);
                return position;

            case SeekOrigin.End:
                position = Math.Max(0, Length - offset);
                return position;
        }
        throw null!;
    }

    public override void SetLength(long value)
    {
        Reallocate(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        long remainingBytes = Length - position;
        count = (int)Math.Min(remainingBytes, count);
        Array.Copy(buffer, offset, contents, position, count);
        position += count;
    }
}