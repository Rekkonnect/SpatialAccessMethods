using SpatialAccessMethods.FileManagement;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.Tests;

public static class MasterBufferControlling
{
    public static readonly MasterBufferController Ultimatum = new(256.Mebibytes());
    public static readonly MasterBufferController Relaxed = new(2.Mebibytes());
    public static readonly MasterBufferController Constrained = new(256.Kibibytes());
}
