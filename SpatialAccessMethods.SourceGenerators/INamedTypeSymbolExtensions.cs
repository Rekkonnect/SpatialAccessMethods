using Microsoft.CodeAnalysis;
using RoseLynn;
using System.Linq;

namespace SpatialAccessMethods.SourceGenerators;

public static class INamedTypeSymbolExtensions
{
    public static bool InheritsInterfaceMatchingName(this INamedTypeSymbol named, FullSymbolName fullName, SymbolNameMatchingLevel symbolNameMatchingLevel = SymbolNameMatchingLevel.Namespace)
    {
        return named.AllInterfaces.Any(Matches);

        bool Matches(INamedTypeSymbol inherited)
        {
            return inherited.GetFullSymbolName().Matches(fullName, symbolNameMatchingLevel);
        }
    }
}
