namespace SpatialAccessMethods;

public class Ball : IOverlappableWith<Rectangle>, IOverlappableWith<Ball>
{
    public Point Center { get; }
    public double Radius { get; }

    public int Rank => Center.Rank;

    public Ball(Point center, double radius)
    {
        Center = center;
        Radius = radius;
    }

    public bool Contains(Point point)
    {
        var difference = Center - point;
        double distance = difference.DistanceFromCenter;
        return distance <= Radius;
    }
    public bool Overlaps(Rectangle rectangle)
    {
        if (rectangle.Contains(Center, true))
            return true;

        var closest = rectangle.ClosestVertexTo(Center);
        return Contains(closest);
    }
    public bool Overlaps(Ball ball)
    {
        return Contains(ball.Center);
    }
}
