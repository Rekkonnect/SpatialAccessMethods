using Garyon.Objects;

namespace SpatialAccessMethods;

public interface IDominable<T>
{
    public Domination ResolveDomination(T other, Extremum dominatingExtremum);
}
