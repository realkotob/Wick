namespace SampleProject;

/// <summary>
/// Controls the player character. Handles health, damage, and death.
/// </summary>
public class PlayerController
{
    private int _health = 100;

    /// <summary>
    /// Reduces health by the given amount. Triggers death if health reaches zero.
    /// </summary>
    public void TakeDamage(int amount)
    {
        _health -= amount;
        if (_health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Game over logic
    }
}
