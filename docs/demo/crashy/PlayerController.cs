using Godot;

/// <summary>
/// Demo crash fixture — deliberately crashes with a NullReferenceException
/// when TakeDamage is called before the HealthBar node is wired up.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    [Export] public NodePath HealthBarPath { get; set; }

    private int _health = 100;
    private ProgressBar _healthBar; // Intentionally null — not wired up in the scene

    public override void _Ready()
    {
        if (HealthBarPath is not null)
            _healthBar = GetNode<ProgressBar>(HealthBarPath);

        GD.Print("PlayerController: _Ready called");
        if (_healthBar is null)
            GD.PushWarning("PlayerController: WARNING: _healthBar node path not found");
    }

    /// <summary>
    /// Applies damage to the player and updates the health bar UI.
    /// </summary>
    public void TakeDamage(int amount)
    {
        _health -= amount;
        _healthBar.Value = _health; // NullReferenceException — _healthBar is null!
        if (_health <= 0)
            Die();
    }

    private void Die()
    {
        GD.Print("PlayerController: Player died");
        QueueFree();
    }
}
