using RoseLynn;

namespace SpatialAccessMethods.SourceGenerators;

public static class KnownFullSymbolNames
{
    private static readonly string[] BaseNamespaces = new[] { nameof(SpatialAccessMethods) };
    private static readonly string[] BaseFileManagementNamespaces = new[] { nameof(SpatialAccessMethods), "FileManagement" };

    public static readonly FullSymbolName IHeaderBlock =
        new(KnownSymbolNames.IHeaderBlock, BaseFileManagementNamespaces);

    public static readonly FullSymbolName HeaderPropertyAttribute1 =
        new(new IdentifierWithArity(KnownSymbolNames.HeaderPropertyAttribute, 1), BaseFileManagementNamespaces);
    public static readonly FullSymbolName HeaderPropertyAttribute2 =
        new(new IdentifierWithArity(KnownSymbolNames.HeaderPropertyAttribute, 2), BaseFileManagementNamespaces);
}
