using UnitsNet;

namespace SpatialAccessMethods.Utilities;

public static class InformationExtensions
{
    public static int BytesInt32(this Information information) => (int)information.Bytes;
    public static long BytesInt64(this Information information) => (long)information.Bytes;
}
