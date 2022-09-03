using SpatialAccessMethods.FileManagement;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.QualityAssurance;

public static class MasterBufferControlling
{
    public static readonly MasterBufferController Ultimatum = new(256.Mebibytes());
    public static readonly MasterBufferController Annex = new(64.Mebibytes());
    public static readonly MasterBufferController Modern = new(16.Mebibytes());
    public static readonly MasterBufferController Relaxed = new(2.Mebibytes());
    public static readonly MasterBufferController Constrained = new(256.Kibibytes());
    public static readonly MasterBufferController Literally1984 = new(64.Kibibytes());
}
