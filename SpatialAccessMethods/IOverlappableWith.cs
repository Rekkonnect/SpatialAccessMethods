namespace SpatialAccessMethods;

public interface IOverlappableWith<TShape> : IShape
    where TShape : IShape
{
    public bool Overlaps(TShape shape);
}

public static class IOverlappableWithExtensions
{
    public static bool Overlaps<TShapeA, TShapeB>(this TShapeA shapeA, TShapeB shapeB)
        where TShapeA : IShape
        where TShapeB : IOverlappableWith<TShapeA>
    {
        return shapeB.Overlaps(shapeA);
    }
}
