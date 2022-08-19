using Microsoft.CodeAnalysis;
using RoseLynn;
using SpatialAccessMethods.FileManagement;
using System.Collections.Immutable;

namespace SpatialAccessMethods.SourceGenerators.Tests;

public static class SpatialAccessMethodsMetadataReferences
{
    public static readonly ImmutableArray<MetadataReference> BaseReferences;

    static SpatialAccessMethodsMetadataReferences()
    {
        BaseReferences = ImmutableArray.Create(new MetadataReference[]
        {
            MetadataReferenceFactory.CreateFromType(typeof(HeaderPropertyAttribute<>)),
        });
    }
}
