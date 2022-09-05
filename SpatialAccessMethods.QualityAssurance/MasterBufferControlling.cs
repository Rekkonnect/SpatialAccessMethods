using SpatialAccessMethods.FileManagement;
using UnitsNet.NumberExtensions.NumberToInformation;

namespace SpatialAccessMethods.QualityAssurance;

public static class MasterBufferControlling
{
    public static readonly MasterBufferController Ultimatum = new(256.Mebibytes()),
                                                  Annex = new(64.Mebibytes()),
                                                  Modern = new(16.Mebibytes()),
                                                  Relaxed = new(2.Mebibytes()),
                                                  Constrained = new(256.Kibibytes()),
                                                  Literally1984 = new(64.Kibibytes());
}
