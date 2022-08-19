using SpatialAccessMethods.FileManagement;
using System.Text.RegularExpressions;

namespace SpatialAccessMethods.Main;

public abstract class RangeQueryCommand<TShape> : QueryCommand
    where TShape : IShape, IOverlappableWith<Rectangle>
{
    private static readonly Regex pointFormat = new(@"\((?'coordinates'.*)\)");

    public abstract TShape Range { get; }

    protected override SpatialDataTable<T>.IQuery GetQuery<T>()
    {
        return new SpatialDataTable<T>.RangeQuery<TShape>(Range);
    }

    protected static Point ParsePoint(string argument)
    {
        // Normalize whitespace
        argument = argument.Replace(" ", "");

        var match = pointFormat.Match(argument);
        var coordinates = ParseCoordinates(match);

        return new Point(coordinates);

        static double[] ParseCoordinates(Match match)
        {
            return match.Groups["coordinates"].Value.Split(',').Select(double.Parse).ToArray();
        }
    }
}
