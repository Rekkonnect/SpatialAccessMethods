using Microsoft.CodeAnalysis;
using RoseLynn;

namespace SpatialAccessMethods.SourceGenerators;

#nullable enable

public static class ITypeSymbolExtensions
{
    public static int GetPredefinedTypeSize(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind is TypeKind.Enum)
        {
            var underlying = (typeSymbol as INamedTypeSymbol)!.EnumUnderlyingType!;
            return GetPredefinedTypeSize(underlying.SpecialType);
        }

        return GetPredefinedTypeSize(typeSymbol.SpecialType);
    }

    private static int GetPredefinedTypeSize(SpecialType specialType)
    {
        return specialType switch
        {
            SpecialType.System_Byte => sizeof(byte),
            SpecialType.System_Int16 => sizeof(short),
            SpecialType.System_Int32 => sizeof(int),
            SpecialType.System_Int64 => sizeof(long),

            SpecialType.System_SByte => sizeof(sbyte),
            SpecialType.System_UInt16 => sizeof(ushort),
            SpecialType.System_UInt32 => sizeof(uint),
            SpecialType.System_UInt64 => sizeof(ulong),

            SpecialType.System_Boolean => sizeof(bool),
            SpecialType.System_Single => sizeof(float),
            SpecialType.System_Double => sizeof(double),
            SpecialType.System_Decimal => sizeof(decimal),

            SpecialType.System_Char => sizeof(char),

            _ => 0,
        };
    }

    public static string GetAliasKeywordOrFullName(this ITypeSymbol typeSymbol)
    {
        // This method does not handle special types like pointers, ref-like types, and arrays
        // Which is not a concern in this implementation
        var specialType = typeSymbol.SpecialType;
        return GetAliasKeyword(specialType) ?? typeSymbol.GetFullSymbolName()!.FullNameString;
    }

    private static string? GetAliasKeyword(SpecialType specialType)
    {
        return specialType switch
        {
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_Int32 => "int",
            SpecialType.System_Int64 => "long",

            SpecialType.System_SByte => "sbyte",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_UInt64 => "ulong",

            SpecialType.System_Boolean => "bool",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",

            SpecialType.System_Char => "char",
            SpecialType.System_String => "string",

            _ => null,
        };
    }
}
