using Garyon.Objects;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Main;

public sealed class SkylineQueryCommand : QueryCommand
{
    public Extremum DominantExtremum { get; set; } = Extremum.Minimum;

    public SkylineQueryCommand()
    {
        IsCommand("skyline", "Performs a skyline query on the database");
        
        HasRequiredOption("e|extremum=", "The dominant extremum (min = minimum, max = maximum)", ParseDominantExtremum);
    }

    private void ParseDominantExtremum(string argument)
    {
        DominantExtremum = argument switch
        {
            "min" => Extremum.Minimum,
            "max" => Extremum.Maximum,
            _ => throw new ArgumentException(@"The argument must be ""min"" or ""max""."),
        };
    }

    protected override SpatialDataTable<T>.IQuery GetQuery<T>()
    {
        return new SpatialDataTable<T>.SkylineQuery(DominantExtremum);
    }
}
