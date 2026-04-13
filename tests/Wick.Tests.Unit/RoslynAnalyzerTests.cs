using Wick.Providers.CSharp;

namespace Wick.Tests.Unit;

public class RoslynAnalyzerTests
{
    private const string SampleClass = """
        using System;
        using System.Collections.Generic;

        namespace MyGame.Entities;

        [Serializable]
        public partial class Player : CharacterBody2D, IDamageable
        {
            private int _health;
            private readonly float _speed = 300f;

            [Export]
            public int MaxHealth { get; set; } = 100;

            public float Speed { get; init; }

            public void TakeDamage(int amount)
            {
                _health -= amount;
            }

            public async Task<bool> SaveAsync(string path, CancellationToken ct)
            {
                return true;
            }

            private void ApplyKnockback(Vector2 direction, float force)
            {
            }
        }

        public enum PlayerState
        {
            Idle,
            Running,
            Jumping
        }
        """;

    [Fact]
    public void Analyze_ExtractsNamespace()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        info.Namespace.Should().Be("MyGame.Entities");
    }

    [Fact]
    public void Analyze_ExtractsUsings()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        info.Usings.Should().Contain("System");
        info.Usings.Should().Contain("System.Collections.Generic");
    }

    [Fact]
    public void Analyze_ExtractsClassInfo()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        playerClass.Kind.Should().Be("class");
        playerClass.Modifiers.Should().Contain("public");
        playerClass.Modifiers.Should().Contain("partial");
    }

    [Fact]
    public void Analyze_ExtractsBaseTypes()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        playerClass.BaseTypes.Should().Contain("CharacterBody2D");
        playerClass.BaseTypes.Should().Contain("IDamageable");
    }

    [Fact]
    public void Analyze_ExtractsAttributes()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        playerClass.Attributes.Should().Contain("Serializable");
    }

    [Fact]
    public void Analyze_ExtractsMethods()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        playerClass.Methods.Should().HaveCount(3);
        playerClass.Methods.Should().Contain(m => m.Name == "TakeDamage");
        playerClass.Methods.Should().Contain(m => m.Name == "SaveAsync");
    }

    [Fact]
    public void Analyze_ExtractsProperties()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        playerClass.Properties.Should().HaveCount(2);
        playerClass.Properties.Should().Contain(p => p.Name == "MaxHealth" && p.Type == "int");
    }

    [Fact]
    public void Analyze_ExtractsFields()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        playerClass.Fields.Should().HaveCount(2);
        playerClass.Fields.Should().Contain(f => f.Name == "_health");
    }

    [Fact]
    public void Analyze_ExtractsEnums()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var enumType = info.Types.First(t => t.Name == "PlayerState");
        enumType.Kind.Should().Be("enum");
        enumType.Members.Should().Contain("Idle");
        enumType.Members.Should().Contain("Running");
        enumType.Members.Should().Contain("Jumping");
    }

    [Fact]
    public void Analyze_ExtractsPropertyAttributes()
    {
        var info = RoslynAnalyzer.Analyze(SampleClass);
        var playerClass = info.Types.First(t => t.Name == "Player");
        var maxHealth = playerClass.Properties.First(p => p.Name == "MaxHealth");
        maxHealth.Attributes.Should().Contain("Export");
    }
}
