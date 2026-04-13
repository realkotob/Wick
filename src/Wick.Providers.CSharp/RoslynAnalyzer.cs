using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wick.Providers.CSharp;

/// <summary>
/// Uses Roslyn to analyze C# source files and extract structural information:
/// classes, methods, properties, fields, attributes, inheritance, etc.
/// </summary>
public static class RoslynAnalyzer
{
    /// <summary>
    /// Analyzes a C# source file and returns its structural information.
    /// </summary>
    public static CSharpFileInfo Analyze(string content, string? filePath = null)
    {
        var tree = CSharpSyntaxTree.ParseText(content, path: filePath ?? "");
        var root = tree.GetCompilationUnitRoot();
        var info = new CSharpFileInfo { FilePath = filePath };

        // Usings
        foreach (var u in root.Usings)
        {
            info.Usings.Add(u.Name?.ToString() ?? "");
        }

        // Namespace
        var nsDecl = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (nsDecl is not null)
        {
            info.Namespace = nsDecl.Name.ToString();
        }

        // Types (classes, structs, interfaces, records, enums)
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var typeInfo = AnalyzeType(typeDecl);
            info.Types.Add(typeInfo);
        }

        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            info.Types.Add(new CSharpTypeInfo
            {
                Name = enumDecl.Identifier.Text,
                Kind = "enum",
                Modifiers = enumDecl.Modifiers.ToString(),
                Line = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Members = enumDecl.Members.Select(m => m.Identifier.Text).ToList(),
            });
        }

        return info;
    }

    private static CSharpTypeInfo AnalyzeType(TypeDeclarationSyntax typeDecl)
    {
        var typeInfo = new CSharpTypeInfo
        {
            Name = typeDecl.Identifier.Text,
            Kind = typeDecl switch
            {
                ClassDeclarationSyntax => "class",
                StructDeclarationSyntax => "struct",
                InterfaceDeclarationSyntax => "interface",
                RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
                _ => "type",
            },
            Modifiers = typeDecl.Modifiers.ToString(),
            Line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        };

        // Base types
        if (typeDecl.BaseList is not null)
        {
            typeInfo.BaseTypes = typeDecl.BaseList.Types.Select(t => t.Type.ToString()).ToList();
        }

        // Attributes
        foreach (var attrList in typeDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                typeInfo.Attributes.Add(attr.Name.ToString());
            }
        }

        // Methods
        foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            typeInfo.Methods.Add(new CSharpMethodInfo
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                Parameters = method.ParameterList.Parameters.Select(p =>
                    $"{p.Type} {p.Identifier}").ToList(),
                Modifiers = method.Modifiers.ToString(),
                Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Attributes = method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Select(a => a.Name.ToString())
                    .ToList(),
            });
        }

        // Properties
        foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            typeInfo.Properties.Add(new CSharpPropertyInfo
            {
                Name = prop.Identifier.Text,
                Type = prop.Type.ToString(),
                Modifiers = prop.Modifiers.ToString(),
                Line = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Attributes = prop.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Select(a => a.Name.ToString())
                    .ToList(),
            });
        }

        // Fields
        foreach (var field in typeDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
            {
                typeInfo.Fields.Add(new CSharpFieldInfo
                {
                    Name = variable.Identifier.Text,
                    Type = field.Declaration.Type.ToString(),
                    Modifiers = field.Modifiers.ToString(),
                    Line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                });
            }
        }

        return typeInfo;
    }
}

public sealed class CSharpFileInfo
{
    public string? FilePath { get; set; }
    public string? Namespace { get; set; }
    public List<string> Usings { get; } = [];
    public List<CSharpTypeInfo> Types { get; } = [];
}

public sealed class CSharpTypeInfo
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public string? Modifiers { get; init; }
    public int Line { get; init; }
    public List<string> BaseTypes { get; set; } = [];
    public List<string> Attributes { get; set; } = [];
    public List<CSharpMethodInfo> Methods { get; set; } = [];
    public List<CSharpPropertyInfo> Properties { get; set; } = [];
    public List<CSharpFieldInfo> Fields { get; set; } = [];
    public List<string> Members { get; set; } = [];
}

public sealed class CSharpMethodInfo
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public List<string> Parameters { get; init; } = [];
    public string? Modifiers { get; init; }
    public int Line { get; init; }
    public List<string> Attributes { get; init; } = [];
}

public sealed class CSharpPropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Modifiers { get; init; }
    public int Line { get; init; }
    public List<string> Attributes { get; init; } = [];
}

public sealed class CSharpFieldInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Modifiers { get; init; }
    public int Line { get; init; }
}
