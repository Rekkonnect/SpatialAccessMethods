using Microsoft.CodeAnalysis;
using RoseLynn;
using System.Collections.Generic;
using System.Linq;

namespace SpatialAccessMethods.SourceGenerators;

public static class ISymbolExtensions
{
    public static IEnumerable<AttributeData> GetAttributesNamed(this ISymbol symbol, FullSymbolName attributeName, SymbolNameMatchingLevel symbolNameMatchingLevel = SymbolNameMatchingLevel.Namespace)
    {
        return symbol.GetAttributes().Where(Matches);

        bool Matches(AttributeData attribute)
        {
            return attribute.AttributeClass.GetFullSymbolName().Matches(attributeName, symbolNameMatchingLevel);
        }
    }
}
