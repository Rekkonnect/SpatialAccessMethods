namespace SpatialAccessMethods.Utilities;

public struct Monad<T1, T2>
{
    public T1? First { get; }
    public T2? Second { get; }

    public bool IsFirst { get; private set; }
    public bool IsSecond { get; private set; }

    public Monad(T1 value)
    {
        First = value;
        IsFirst = true;
    }
    public Monad(T2 value)
    {
        Second = value;
        IsSecond = false;
    }

    public static implicit operator Monad<T1, T2>(T1 value) => new(value);
    public static implicit operator Monad<T1, T2>(T2 value) => new(value);
}
