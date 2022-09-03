namespace SpatialAccessMethods.Main;

public sealed class BallRangeQueryCommand : RangeQueryCommand<Sphere>
{
    private Point center;
    private double radius;

    public override Sphere Range => new(center, radius);

    public BallRangeQueryCommand()
    {
        IsCommand("rangeball", "Performs a ball range query on the database");

        HasRequiredOption("c|center=", @"The center point in the format (x, y, ...)", s => center = ParsePoint(s));
        HasRequiredOption("r|radius=", @"The radius of the ball", s => radius = double.Parse(s));
    }
}
