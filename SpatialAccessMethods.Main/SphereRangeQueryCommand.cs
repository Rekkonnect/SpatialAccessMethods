namespace SpatialAccessMethods.Main;

public sealed class SphereRangeQueryCommand : RangeQueryCommand<Sphere>
{
    private Point center;
    private double radius;

    public override Sphere Range => new(center, radius);

    public SphereRangeQueryCommand()
    {
        IsCommand("rangesphere", "Performs a sphere range query on the database");

        HasRequiredOption("c|center=", @"The center point in the format (x, y, ...)", s => center = ParsePoint(s));
        HasRequiredOption("r|radius=", @"The radius of the sphere", s => radius = double.Parse(s));
    }
}
