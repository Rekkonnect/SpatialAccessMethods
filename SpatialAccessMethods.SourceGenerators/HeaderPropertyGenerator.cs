using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoseLynn;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpatialAccessMethods.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class HeaderPropertyGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var receiver = context.SyntaxContextReceiver as SyntaxContextReceiver;
        var requested = receiver.RequestedHeaderProperties;
        foreach (var requestedEntry in requested)
        {
            var type = requestedEntry.Key;
            var properties = requestedEntry.Value;

            if (properties.Count is 0)
                continue;

            var builder = new StringBuilder();

            var typeNamespace = type.GetFullSymbolName().FullNamespaceString;
            var declarationSyntax = type.DeclaringSyntaxReferences.First().GetSyntax() as TypeDeclarationSyntax;

            builder.Append($$"""
                             using SpatialAccessMethods.FileManagement;
                             
                             namespace {{typeNamespace}};
                             
                             partial {{declarationSyntax.Keyword.WithoutTrivia()}} {{type.Name}}
                             {
                             """);

            int currentOffset = 0;

            foreach (var property in properties)
            {
                var code = GetPropertyCode(property, ref currentOffset);
                builder.Append(code);
            }

            builder.AppendLine();
            builder.AppendLine("}");

            var source = builder.ToString();
            context.AddSource($"{type.Name}.HeaderProperties.g.cs", source);

            static string GetPropertyCode(HeaderPropertyInfo propertyInfo, ref int currentOffset)
            {
                // This does not handle bools
                var storedTypeName = propertyInfo.StoredType.GetAliasKeywordOrFullName();
                var propertyTypeName = propertyInfo.PropertyType.GetAliasKeywordOrFullName();
                int offset = propertyInfo.CustomOffset;
                if (offset < 0)
                {
                    offset = currentOffset;
                    currentOffset += propertyInfo.StoredType.GetPredefinedTypeSize();
                }

                var getterCast = $"({propertyInfo.PropertyType})";
                var setterCast = $"({propertyInfo.StoredType})";
                if (propertyInfo.MatchingTypes)
                {
                    getterCast = string.Empty;
                    setterCast = string.Empty;
                }

                // Always assume that the stored type's size is less than the property type's
                // This implies that the numeric types will be implicitly castable between each other
                return $$""""
                         
                             public {{propertyTypeName}} {{propertyInfo.Name}}
                             {
                                 get => {{getterCast}}(this as IHeaderBlock).GetHeaderProperty<{{storedTypeName}}>({{offset}});
                                 set => (this as IHeaderBlock).SetHeaderProperty({{offset}}, {{setterCast}}value);
                             }
                         """";
            }
        }
    }

    void ISourceGenerator.Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxContextReceiver());
    }

    private sealed class SyntaxContextReceiver : ISyntaxContextReceiver
    {
        private readonly HeaderPropertyRequestDictionary requestedHeaderProperties = new();
        
        public IReadOnlyDictionary<INamedTypeSymbol, IReadOnlyList<HeaderPropertyInfo>> RequestedHeaderProperties => requestedHeaderProperties.Requested;

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            var declared = context.SemanticModel.GetDeclaredSymbol(context.Node);
            if (declared is not INamedTypeSymbol namedDeclared)
                return;

            var headerProperties1 = namedDeclared.GetAttributesNamed(KnownFullSymbolNames.HeaderPropertyAttribute1).Select(HeaderPropertyInfo.FromAttribute1);
            var headerProperties2 = namedDeclared.GetAttributesNamed(KnownFullSymbolNames.HeaderPropertyAttribute2).Select(HeaderPropertyInfo.FromAttribute2);
            var allHeaderProperties = headerProperties1.Concat(headerProperties2).ToArray();
            if (allHeaderProperties.Length is 0)
                return;
            
            requestedHeaderProperties.AddRange(namedDeclared, allHeaderProperties);

            if (!namedDeclared.InheritsInterfaceMatchingName(KnownFullSymbolNames.IHeaderBlock))
            {
                // TODO: Emit diagnostic
                return;
            }
        }

        public sealed class HeaderPropertyRequestDictionary
        {
            private readonly Dictionary<INamedTypeSymbol, IReadOnlyList<HeaderPropertyInfo>> inner = new(SymbolEqualityComparer.Default);

            public IReadOnlyDictionary<INamedTypeSymbol, IReadOnlyList<HeaderPropertyInfo>> Requested => inner;

            public void Add(INamedTypeSymbol type, HeaderPropertyInfo propertyInfo)
            {
                var list = GetOrInitForType(type);
                list.Add(propertyInfo);
            }
            public void AddRange(INamedTypeSymbol type, IEnumerable<HeaderPropertyInfo> propertyInfos)
            {
                var list = GetOrInitForType(type);
                list.AddRange(propertyInfos);
            }

            private List<HeaderPropertyInfo> GetOrInitForType(INamedTypeSymbol type)
            {
                inner.TryGetValue(type, out var list);
                if (list is null)
                    inner[type] = list = new List<HeaderPropertyInfo>();

                return list as List<HeaderPropertyInfo>;
            }
        }
    }
    private sealed record class HeaderPropertyInfo(ITypeSymbol PropertyType, ITypeSymbol StoredType, string Name, int CustomOffset)
    {
        public bool MatchingTypes => PropertyType.Equals(StoredType, SymbolEqualityComparer.Default);

        public HeaderPropertyInfo(ITypeSymbol commonType, string name, int customOffset)
            : this(commonType, commonType, name, customOffset) { }

        public static HeaderPropertyInfo FromAttribute1(AttributeData attribute)
        {
            ParseCommonArguments(attribute, out var typeArgument, out var name, out int customOffset);
            return new(typeArgument, name, customOffset);
        }
        public static HeaderPropertyInfo FromAttribute2(AttributeData attribute)
        {
            var storedType = attribute.AttributeClass.TypeArguments[1];
            ParseCommonArguments(attribute, out var propertyType, out var name, out int customOffset);
            return new(propertyType, storedType, name, customOffset);
        }

        private static void ParseCommonArguments(AttributeData attribute, out ITypeSymbol propertyType, out string name, out int customOffset)
        {
            propertyType = attribute.AttributeClass.TypeArguments[0];
            name = attribute.ConstructorArguments[0].Value as string;

            customOffset = -1;
            if (attribute.ConstructorArguments.Length > 1)
                customOffset = (int)attribute.ConstructorArguments[1].Value;
        }
    }
}
