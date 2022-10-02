namespace SpatialAccessMethods.DataStructures;

public class OperationReferenceFactory
{
    private object? current;

    public void DisposeCurrent()
    {
        current = null;
    }
    public object ForceNext()
    {
        return current = new();
    }
    public object CurrentOrNext()
    {
        return current ?? ForceNext();
    }
}