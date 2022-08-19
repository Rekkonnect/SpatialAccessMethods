namespace SpatialAccessMethods.Main;

public sealed class RectangleRangeQueryCommand : RangeQueryCommand<Rectangle>
{
    private Point a, b;

    public override Rectangle Range => Rectangle.FromVertices(a, b);

    public RectangleRangeQueryCommand()
    {
        IsCommand("rangerect", "Performs a rectangular range query on the database");

        HasRequiredOption("a=", @"The one vertex point in the format (x, y, ...)", s => a = ParsePoint(s));
        HasRequiredOption("b=", @"The other vertex point in the format (x, y, ...)", s => b = ParsePoint(s));
    }
}
