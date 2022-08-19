using Microsoft.CodeAnalysis;
using System.Linq;

namespace SpatialAccessMethods.SourceGenerators;

public static class AttributeDataExtensions
{
    public static TypedConstant NamedArgument(this AttributeData attributeData, string name)
    {
        var argumentPair = attributeData.NamedArguments.FirstOrDefault(kvp => kvp.Key == name);
        if (argumentPair.Key != name)
            return default;

        return argumentPair.Value;
    }
}
