using Garyon.Extensions;
using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Main;

public sealed class NearestNeighborQueryCommand : QueryCommand
{
    public int Neighbors { get; set; }

    public NearestNeighborQueryCommand()
    {
        IsCommand("nn", "Performs a k-nearest neighbor query on the database");

        HasRequiredOption("n|nn=", "The number of nearest neighbors", p => Neighbors = p.ParseInt32());
    }

    protected override SpatialDataTable<T>.IQuery GetQuery<T>()
    {
        return new SpatialDataTable<T>.NearestNeighborQuery(Neighbors);
    }
}
