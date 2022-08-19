using System.Runtime.InteropServices;

namespace SpatialAccessMethods.Utilities;

public static class SpanExtensions
{
    public static unsafe T ReadValue<T>(this Span<byte> byteSpan, int offset = 0)
        where T : unmanaged
    {
        return byteSpan.ValueRef<T>(offset);
    }
    public static unsafe void WriteValue<T>(this Span<byte> byteSpan, T value, int offset = 0)
        where T : unmanaged
    {
        byteSpan.ValueRef<T>(offset) = value;
    }

    public static unsafe void ReadValueAdvance<T>(this ref Span<byte> byteSpan, out T value, int offset = 0)
        where T : unmanaged
    {
        value = ReadValueAdvance<T>(ref byteSpan, offset);
    }
    public static unsafe T ReadValueAdvance<T>(this ref Span<byte> byteSpan, int offset = 0)
        where T : unmanaged
    {
        var value = byteSpan.ReadValue<T>(offset);
        byteSpan.AfterValueRef<T>(offset);
        return value;
    }
    public static unsafe void WriteValueAdvance<T>(this ref Span<byte> byteSpan, T value, int offset = 0)
        where T : unmanaged
    {
        byteSpan.WriteValue(value, offset);
        byteSpan.AfterValueRef<T>(offset);
    }
    public static unsafe void AfterValueRef<T>(this ref Span<byte> byteSpan, int offset = 0)
        where T : unmanaged
    {
        byteSpan = byteSpan.AfterValue<T>(offset);
    }
    public static unsafe Span<byte> AfterValue<T>(this Span<byte> byteSpan, int offset = 0)
        where T : unmanaged
    {
        return byteSpan[(offset + sizeof(T))..];
    }

    public static unsafe Span<byte> ForValue<T>(this Span<byte> byteSpan, int offset = 0)
        where T : unmanaged
    {
        if (offset > 0)
            byteSpan = byteSpan[offset..];
        return byteSpan[..sizeof(T)];
    }
    public static unsafe ref T ValueRef<T>(this Span<byte> byteSpan, int offset = 0)
        where T : unmanaged
    {
        return ref MemoryMarshal.Cast<byte, T>(byteSpan.ForValue<T>(offset))[0];
    }
    public static unsafe ref T IndexedValueRef<T>(this Span<byte> byteSpan, int index = 0)
        where T : unmanaged
    {
        return ref byteSpan.ValueRef<T>(index * sizeof(T));
    }
}
