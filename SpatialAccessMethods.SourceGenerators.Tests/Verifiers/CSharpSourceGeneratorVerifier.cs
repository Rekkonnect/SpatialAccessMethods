using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using RoseLynn.Generators.Testing;
using System.Collections.Generic;

namespace SpatialAccessMethods.SourceGenerators.Tests.Verifiers;

public static class CSharpSourceGeneratorVerifier<TSourceGenerator>
    where TSourceGenerator : ISourceGenerator, new()
{
    public class Test : CSharpSourceGeneratorTestEx<TSourceGenerator, NUnitVerifier>
    {
        public override IEnumerable<MetadataReference> AdditionalReferences => SpatialAccessMethodsMetadataReferences.BaseReferences;
        
        public Test()
        {
            ReferenceAssemblies = RuntimeReferences.NET6_0Reference;
        }
    }
}