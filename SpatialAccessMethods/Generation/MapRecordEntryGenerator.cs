using Garyon.Objects.Advanced;
using SpatialAccessMethods.Utilities;

namespace SpatialAccessMethods.Generation;

public record MapRecordEntryGenerator(AdvancedRandom Random)
    : Generator(Random)
{
    public static MapRecordEntryGenerator Shared { get; } = new(AdvancedRandomDLC.Shared);

    public MapRecordEntry Next(int dimensionality, double coordinateMin, double coordinateMax, IReadOnlyList<string> nameList)
    {
        var name = nameList.GetRandomOrNull(Random);
        var point = PointGenerator.Shared.Next(dimensionality, coordinateMin, coordinateMax);
        return new(point, name);
    }
    public MapRecordEntry NextWithinRectangle(Rectangle rectangle, IReadOnlyList<string> nameList)
    {
        var name = nameList.GetRandomOrNull(Random);
        var point = PointGenerator.Shared.NextWithinRectangle(rectangle);
        return new(point, name);
    }
}
