namespace Wick.Core;

/// <summary>
/// Routes operations to the appropriate provider based on file extension.
/// Files ending in .gd are routed to the GDScript provider.
/// Files ending in .cs are routed to the C#/.NET provider.
/// Other operations (scene, project, runtime) route to the Godot Engine provider.
/// </summary>
public static class LanguageRouter
{
    /// <summary>
    /// Determines the language context from a file path.
    /// </summary>
    public static LanguageContext ResolveLanguage(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        return extension.ToLowerInvariant() switch
        {
            ".gd" => LanguageContext.GDScript,
            ".cs" => LanguageContext.CSharp,
            ".tscn" or ".tres" or ".godot" => LanguageContext.GodotResource,
            _ => LanguageContext.Unknown,
        };
    }
}

/// <summary>
/// Represents the language context for routing tool operations.
/// </summary>
public enum LanguageContext
{
    /// <summary>GDScript file (.gd)</summary>
    GDScript,

    /// <summary>C# file (.cs)</summary>
    CSharp,

    /// <summary>Godot resource file (.tscn, .tres, .godot)</summary>
    GodotResource,

    /// <summary>Unknown or unsupported file type</summary>
    Unknown,
}
