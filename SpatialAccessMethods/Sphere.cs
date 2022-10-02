using System.Diagnostics;

namespace SpatialAccessMethods;

public struct Sphere : IOverlappableWith<Rectangle>, IOverlappableWith<Sphere>
{
    public Point Center { get; }
    public double Radius { get; }

    public int Rank => Center.Rank;

    public double Volume => VolumeCalculation.CalculateVolume(Rank, Radius);

    public Sphere(Point center, double radius)
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
        // The shapes overlap if:
        // - the center of the sphere is contained in the rectangle
        if (rectangle.Contains(Center, true))
            return true;

        // - the center of the rectangle is contained in the sphere
        if (Contains(rectangle.Center))
            return true;

        // - the MBR of the sphere overlaps with the given rectangle
        var sphereMBR = MBR();
        if (!sphereMBR.Overlaps(rectangle))
            return false;

        // - the closest vertex of the rectangle ins contained in the sphere
        var closest = rectangle.ClosestVertexTo(Center);
        if (Contains(closest))
            return true;

        // - the rectangle contains the edge of the sphere contained
        //   in the line between the two shapes' centers
        var centerDistance = Center.DistanceFrom(rectangle.Center, out var centerCoordinateDistance);
        double scale = Radius / centerDistance;
        Debug.Assert(scale < 1, "The sphere's radius must be less than the center distance, as the centers being contained was already evaluated");
        var edgeOffset = centerCoordinateDistance * scale;
        var edge = Center - edgeOffset;
        bool rectangleContainsEdge = rectangle.Contains(edge);
        if (rectangleContainsEdge)
            return true;

        return false;
        // Phew I've not touched geometry in a while
    }
    public bool Overlaps(Sphere sphere)
    {
        return Contains(sphere.Center);
    }

    public Rectangle MBR()
    {
        var manhattanDifference = new Point(Rank, Radius);
        var min = Center - manhattanDifference;
        var max = Center + manhattanDifference;
        return Rectangle.FromVertices(min, max);
    }

    public override string ToString()
    {
        return $"{Center} | Radius: {Radius}";
    }

    public static Sphere SphereForVolume(Point center, int rank, double targetVolume)
    {
        var radius = VolumeCalculation.CalculateRadiusForVolume(rank, targetVolume);
        return new(center, radius);
    }

    private static class VolumeCalculation
    {
        private static readonly double PiSquare;
        private static readonly double PiCube;
        private static readonly double PiQuad;

        static VolumeCalculation()
        {
            PiSquare = Math.PI * Math.PI;
            PiCube = PiSquare * Math.PI;
            PiQuad = PiCube * Math.PI;
        }

        public static double CalculateRadiusForVolume(int rank, double targetVolume)
        {
            double volumeCoefficient = 1 / GetRadiusCoefficient(rank);
            return Math.Pow(volumeCoefficient * targetVolume, 1D / rank);
        }

        public static double CalculateVolume(int rank, double radius)
        {
            return GetRadiusCoefficient(rank) * Math.Pow(radius, rank);
        }
        private static double GetRadiusCoefficient(int rank) => rank switch
        {
            0 => 1,
            1 => 2,
            2 => Math.PI,
            3 => Math.PI * 4 / 3,
            4 => PiSquare / 2,
            5 => PiSquare * 8 / 15,
            6 => PiCube / 6,
            7 => PiCube * 16 / 105,
            8 => PiQuad / 24,
            9 => PiQuad * 32 / 945,

            _ => 0, // Implement this some other time
        };
    }
}
