using Godot;

/// <summary>
/// Demo crash fixture — calls PlayerController.TakeDamage from _PhysicsProcess,
/// triggering the NullReferenceException in the player's health bar update.
/// </summary>
public partial class EnemyAI : CharacterBody3D
{
    [Export] public float AttackRange { get; set; } = 5.0f;
    [Export] public int Damage { get; set; } = 15;
    [Export] public float AttackCooldown { get; set; } = 2.0f;

    private PlayerController _target;
    private double _cooldownTimer;

    public override void _Ready()
    {
        _target = GetTree().GetFirstNodeInGroup("player") as PlayerController;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target is null) return;

        _cooldownTimer -= delta;
        if (_cooldownTimer > 0) return;

        var distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
        if (distance > AttackRange) return;

        GD.Print($"EnemyAI: Acquired target 'Player' at distance {distance:F1}");
        GD.Print("EnemyAI: Attack cooldown expired, attacking");
        Attack(_target);
        _cooldownTimer = AttackCooldown;
    }

    public void Attack(PlayerController target)
    {
        target.TakeDamage(Damage);
    }
}
