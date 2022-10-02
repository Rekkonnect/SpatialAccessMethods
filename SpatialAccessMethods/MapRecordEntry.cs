using SpatialAccessMethods.FileManagement;
using SpatialAccessMethods.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace SpatialAccessMethods;

public struct MapRecordEntry : ILocated, IID, IRecordSerializable<MapRecordEntry>, IEquatable<MapRecordEntry>
{
    public static readonly MapRecordEntry Invalid = new();
    
    public bool IsAlive { get; private set; }

    public int ID { get; set; }

    public Point Location { get; private set; }
    public string? Name { get; private set; }

    public double Latitude => (double)Location.GetCoordinate(0);
    public double Longitude => (double)Location.GetCoordinate(1);

    public const int RecordSize = 256;
    public const int RecordCharSize = RecordSize / sizeof(char) - 1; 
    public int MaxNameChars => RecordCharSize - sizeof(double) / 2 * Location.Rank;

    public MapRecordEntry(Point location, string? name = null)
        : this(0, location, name) { }
    
    public MapRecordEntry(int id, Point location, string? name = null)
    {
        ID = id;
        Location = location;
        Name = name;
        IsAlive = true;

        if (name?.Length > MaxNameChars)
        {
            throw new ArgumentException($"Name is too long. Max length is {MaxNameChars} characters.");
        }
    }
    
    public void Deallocate(Span<byte> span)
    {
        // The spec declares that the first byte simply denotes whether the record is allocated or not
        // The rest of the record's information is left untouched
        span.WriteValue((byte)0);
    }

    public override string ToString()
    {
        return $"Location: {Location} | Name: {Name ?? "null"}";
    }

    public static bool operator ==(MapRecordEntry left, MapRecordEntry right)
    {
        return left.Equals(right);
    }
    public static bool operator !=(MapRecordEntry left, MapRecordEntry right)
    {
        return !(left == right);
    }

    public bool EqualsData(MapRecordEntry other)
    {
        return Location == other.Location
            && EqualsName(other);
    }
    private bool EqualsName(MapRecordEntry other)
    {
        return (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(other.Name))
            || Name == other.Name;
    }

    public bool Equals(MapRecordEntry other)
    {
        return ID == other.ID
            && EqualsData(other);
    }
    public override int GetHashCode()
    {
        return ID ^ Location.GetHashCode();
    }

    public void Write(Span<byte> span, IHeaderBlock headerInformation)
    {
        var dataHeaderInformation = (DataHeaderBlock)headerInformation;
        
        var spanStream = new SpanStream(span);

        spanStream.WriteValue((byte)1);
        int dimensions = dataHeaderInformation.Dimensionality;
        for (int i = 0; i < dimensions; i++)
            spanStream.WriteValue((double)Location.GetCoordinate(i));
        spanStream.WriteNullTerminatedStringUTF16(Name);
    }
    public static MapRecordEntry Parse(Span<byte> span, IHeaderBlock headerInformation)
    {
        var dataHeaderInformation = (DataHeaderBlock)headerInformation;
        
        var spanStream = new SpanStream(span);
        
        int flag = spanStream.ReadValue<byte>();
        // The flag is 0, meaning the record is not alive
        if (flag is 0)
            return default;

        var result = new MapRecordEntry();

        int dimensions = dataHeaderInformation.Dimensionality;
        double[] coordinates = new double[dimensions];
        for (int i = 0; i < dimensions; i++)
            coordinates[i] = spanStream.ReadValue<double>();

        result.Location = new Point(coordinates);
        result.Name = spanStream.ReadNullTerminatedStringUTF16(result.MaxNameChars);
        result.IsAlive = true;

        return result;
    }

    public sealed class DataEqualityComparer : IEqualityComparer<MapRecordEntry>
    {
        public static DataEqualityComparer Instance { get; } = new();
        private DataEqualityComparer() { }

        public bool Equals(MapRecordEntry x, MapRecordEntry y)
        {
            return x.EqualsData(y);
        }

        public int GetHashCode(MapRecordEntry entry)
        {
            return entry.Location.GetHashCode();
        }
    }
}
