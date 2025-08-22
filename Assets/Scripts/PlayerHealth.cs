using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private HealthBar healthBar;

    private void Start()
    {
        currentHealth = maxHealth;
        healthBar.Initialize(maxHealth);
        healthBar.SetValue(currentHealth);
    }

    // Read-only accessors for UI/status
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        healthBar.SetValue(currentHealth);

        // reset special
        PlayerSpecial playerSpecial = GetComponent<PlayerSpecial>();
        if (playerSpecial != null)
        {
            playerSpecial.ResetSpecialStreak();
        }

        if (currentHealth <= 0)
        {
            Time.timeScale = 0; // Stop the game
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        healthBar.SetValue(currentHealth);
    }

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth += amount; // Also heal the player by the same amount
        healthBar.Initialize(maxHealth);
        healthBar.SetValue(currentHealth);
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
}