using System.Runtime.InteropServices;
using System.Text;

namespace SpatialAccessMethods.Utilities;

public ref struct SpanStream
{
    private Span<byte> span;
    public Span<byte> Span => span;

    public SpanStream(Span<byte> span)
    {
        this.span = span;
    }

    public unsafe T ReadValue<T>(int offset = 0)
        where T : unmanaged
    {
        return span.ReadValueAdvance<T>(offset);
    }
    public unsafe void WriteValue<T>(T value, int offset = 0)
        where T : unmanaged
    {
        span.WriteValueAdvance(value, offset);
    }

    public void AdvanceValue<T>()
        where T : unmanaged
    {
        AdvanceValueRange<T>(1);
    }
    public unsafe void AdvanceValueRange<T>(int count)
        where T : unmanaged
    {
        int offset = count * sizeof(T);
        span.AfterValueRef<T>(offset);
    }

    public void WriteNullTerminatedStringUTF16(string? s)
    {
        if (s is not null)
        {
            var bytes = Encoding.Unicode.GetBytes(s);
            foreach (byte b in bytes)
                span.WriteValueAdvance(b);
        }
        span.WriteValueAdvance('\0');
    }
    public string ReadNullTerminatedStringUTF16(int maxChars)
    {
        Span<byte> bytes = stackalloc byte[maxChars * sizeof(char)];
        
        for (int i = 0; i < bytes.Length; i++)
        {
            span.ReadValueAdvance(out bytes[i]);
        }

        var charSpan = MemoryMarshal.Cast<byte, char>(bytes);
        int terminatorIndex = charSpan.IndexOf('\0');
        if (terminatorIndex > -1)
            charSpan = charSpan[..terminatorIndex];
        
        return new string(charSpan);
    }
}
