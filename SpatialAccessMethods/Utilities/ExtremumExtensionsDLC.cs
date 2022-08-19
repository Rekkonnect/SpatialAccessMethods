using Garyon.Objects;

namespace SpatialAccessMethods.Utilities;

public static class ExtremumExtensionsDLC
{
    public static Extremum Opposing(this Extremum extremum)
    {
        return extremum switch
        {
            Extremum.Minimum => Extremum.Maximum,
            Extremum.Maximum => Extremum.Minimum,
            
            _ => ~extremum,
        };
    }
}
