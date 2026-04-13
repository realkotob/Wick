using Wick.Providers.GDScript;

namespace Wick.Tests.Unit;

public class GDScriptParserTests
{
    private const string SampleScript = """
        class_name Player
        extends CharacterBody2D

        signal health_changed(new_health: int)
        signal died

        const MAX_HEALTH = 100
        const SPEED = 300.0

        enum State { IDLE, RUNNING, JUMPING, FALLING }

        @export var max_health: int = 100
        @export(0, 100) var speed: float
        @onready var sprite: Sprite2D = $Sprite2D
        var current_health: int
        var _internal_state: State

        func _ready() -> void:
            current_health = max_health

        func _process(delta: float) -> void:
            pass

        func _physics_process(delta: float) -> void:
            var velocity = Vector2.ZERO
            move_and_slide()

        func take_damage(amount: int) -> void:
            current_health -= amount
            health_changed.emit(current_health)
            if current_health <= 0:
                died.emit()

        func _apply_knockback(direction: Vector2) -> void:
            pass
        """;

    [Fact]
    public void Parse_ExtractsClassName()
    {
        var info = GDScriptParser.Parse(SampleScript);
        info.ClassName.Should().Be("Player");
    }

    [Fact]
    public void Parse_ExtractsExtends()
    {
        var info = GDScriptParser.Parse(SampleScript);
        info.Extends.Should().Be("CharacterBody2D");
    }

    [Fact]
    public void Parse_ExtractsSignals()
    {
        var info = GDScriptParser.Parse(SampleScript);
        info.Signals.Should().HaveCount(2);
        info.Signals[0].Name.Should().Be("health_changed");
        info.Signals[1].Name.Should().Be("died");
    }

    [Fact]
    public void Parse_ExtractsConstants()
    {
        var info = GDScriptParser.Parse(SampleScript);
        info.Constants.Should().HaveCount(2);
        info.Constants[0].Name.Should().Be("MAX_HEALTH");
        info.Constants[0].Value.Should().Be("100");
    }

    [Fact]
    public void Parse_ExtractsEnums()
    {
        var info = GDScriptParser.Parse(SampleScript);
        info.Enums.Should().HaveCount(1);
        info.Enums[0].Name.Should().Be("State");
    }

    [Fact]
    public void Parse_ExtractsExportVariables()
    {
        var info = GDScriptParser.Parse(SampleScript);
        var exports = info.Variables.Where(v => v.IsExport).ToList();
        exports.Should().HaveCount(2);
        exports[0].Name.Should().Be("max_health");
        exports[0].Type.Should().Be("int");
    }

    [Fact]
    public void Parse_ExtractsOnreadyVariables()
    {
        var info = GDScriptParser.Parse(SampleScript);
        var onready = info.Variables.Where(v => v.IsOnready).ToList();
        onready.Should().HaveCount(1);
        onready[0].Name.Should().Be("sprite");
        onready[0].Type.Should().Be("Sprite2D");
    }

    [Fact]
    public void Parse_ExtractsFunctions()
    {
        var info = GDScriptParser.Parse(SampleScript);
        info.Functions.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Parse_IdentifiesOverrideFunctions()
    {
        var info = GDScriptParser.Parse(SampleScript);
        var overrides = info.Functions.Where(f => f.IsOverride).ToList();
        overrides.Should().HaveCount(3); // _ready, _process, _physics_process
    }

    [Fact]
    public void Parse_IdentifiesPrivateFunctions()
    {
        var info = GDScriptParser.Parse(SampleScript);
        var privates = info.Functions.Where(f => f.IsPrivate).ToList();
        privates.Should().HaveCountGreaterThanOrEqualTo(3); // _ready, _process, _physics_process, _apply_knockback
    }

    [Fact]
    public void Parse_ExtractsReturnTypes()
    {
        var info = GDScriptParser.Parse(SampleScript);
        var readyFunc = info.Functions.First(f => f.Name == "_ready");
        readyFunc.ReturnType.Should().Be("void");
    }
}
