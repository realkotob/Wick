namespace SampleProject;

/// <summary>
/// Controls enemy behavior. Attacks the player on sight.
/// </summary>
public class EnemyAI
{
    private readonly PlayerController _target;

    public EnemyAI(PlayerController target)
    {
        _target = target;
    }

    public void Attack(int damage)
    {
        _target.TakeDamage(damage);
    }
}
