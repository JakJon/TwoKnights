using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private HealthBar healthBar;
    private int _currentHealth;

    void Start()
    {
        _currentHealth = maxHealth;
        healthBar.Initialize(maxHealth);
        healthBar.SetValue(_currentHealth);
    }

    public void TakeDamage(int damage)
    {
        //AudioManager.Instance.PlaySFX(AudioManager.Instance.playerHurt);
        _currentHealth -= damage;
        healthBar.SetValue(_currentHealth);

        // reset special
        PlayerSpecial playerSpecial = GetComponent<PlayerSpecial>();
        if (playerSpecial != null)
        {
            playerSpecial.ResetSpecialStreak();
        }

        if (_currentHealth <= 0)
        {
            Time.timeScale = 0; // Stop the game
        }
    }

    public void Heal(int amount)
    {
        _currentHealth += amount;
        if (_currentHealth > maxHealth)
            _currentHealth = maxHealth;
        healthBar.SetValue(_currentHealth);
    }
}