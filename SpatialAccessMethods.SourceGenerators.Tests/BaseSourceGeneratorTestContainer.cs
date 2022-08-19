using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using RoseLynn.Testing;
using SpatialAccessMethods.SourceGenerators.Tests.Verifiers;
using System.Collections.Generic;

namespace SpatialAccessMethods.SourceGenerators.Tests;

public abstract class BaseSourceGeneratorTestContainer<TSourceGenerator>
    : BaseSourceGeneratorTestContainer<TSourceGenerator, NUnitVerifier, CSharpSourceGeneratorVerifier<TSourceGenerator>.Test>

    where TSourceGenerator : ISourceGenerator, new()
{
    protected override IEnumerable<MetadataReference> DefaultMetadataReferences => SpatialAccessMethodsMetadataReferences.BaseReferences;

    protected override LanguageVersion LanguageVersion => LanguageVersion.Preview;
}
