using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Wick.Core;

namespace Wick.Providers.GDScript;

/// <summary>
/// GDScript tools for Wick.
/// Reimplements GDScript tooling concepts from GoPeak (MIT, © 2025 Solomon Elias / HaD0Yun).
/// </summary>
[McpServerToolType]
public static partial class GDScriptTools
{
    [McpServerTool, Description("Returns GDScript provider status and capabilities.")]
    public static string GDScriptStatus()
    {
        return """
            {
              "provider": "GDScript",
              "status": "active",
              "capabilities": ["parser", "lsp", "dap", "script_gen"],
              "lsp_port": 6005,
              "dap_port": 6006
            }
            """;
    }

    [McpServerTool, Description("Determines the language context for a given file path (GDScript, C#, or Godot resource).")]
    public static string DetectLanguage(string filePath)
    {
        var context = LanguageRouter.ResolveLanguage(filePath);
        return JsonSerializer.Serialize(new { file = filePath, language = context.ToString() }, JsonOptions);
    }

    [McpServerTool, Description("Parses a GDScript file and returns its structure: class_name, extends, functions, signals, variables, exports, constants, and enums.")]
    public static string ScriptInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return JsonSerializer.Serialize(new { error = $"File not found: {filePath}" });
        }

        var content = File.ReadAllText(filePath);
        var info = GDScriptParser.Parse(content);

        return JsonSerializer.Serialize(new
        {
            file = filePath,
            class_name = info.ClassName,
            extends = info.Extends,
            functions = info.Functions.Select(f => new
            {
                f.Name,
                f.Parameters,
                return_type = f.ReturnType,
                is_private = f.IsPrivate,
                is_override = f.IsOverride,
                f.Line,
            }),
            signals = info.Signals.Select(s => new { s.Name, s.Parameters, s.Line }),
            variables = info.Variables.Select(v => new
            {
                v.Name,
                v.Type,
                is_export = v.IsExport,
                is_onready = v.IsOnready,
                export_hint = v.ExportHint,
                v.Line,
            }),
            constants = info.Constants.Select(c => new { c.Name, c.Value, c.Line }),
            enums = info.Enums.Select(e => new { e.Name, e.Line }),
        }, JsonOptions);
    }

    [McpServerTool, Description("Generates a GDScript file template for a node type with proper structure: extends, class_name, lifecycle functions, and signals.")]
    public static string ScriptCreate(
        string className,
        string extends = "Node",
        bool includeReady = true,
        bool includeProcess = false,
        bool includePhysicsProcess = false)
    {
        if (!GdIdentifier().IsMatch(className))
            throw new ArgumentException($"Invalid GDScript identifier: '{className}'");
        if (!GdIdentifier().IsMatch(extends))
            throw new ArgumentException($"Invalid GDScript identifier: '{extends}'");

        var lines = new List<string>
        {
            $"class_name {className}",
            $"extends {extends}",
            "",
        };

        lines.Add("");
        lines.Add("");

        if (includeReady)
        {
            lines.Add("func _ready() -> void:");
            lines.Add("\tpass");
            lines.Add("");
            lines.Add("");
        }

        if (includeProcess)
        {
            lines.Add("func _process(delta: float) -> void:");
            lines.Add("\tpass");
            lines.Add("");
            lines.Add("");
        }

        if (includePhysicsProcess)
        {
            lines.Add("func _physics_process(delta: float) -> void:");
            lines.Add("\tpass");
            lines.Add("");
        }

        var script = string.Join("\n", lines);

        return JsonSerializer.Serialize(new
        {
            class_name = className,
            extends,
            content = script,
        }, JsonOptions);
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex GdIdentifier();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
