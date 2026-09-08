using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GoldUI : MonoBehaviour
{
    // Gold is a camp concern: it is what the shop spends, and mid-run the number
    // is something the player can watch but not act on. So the counter belongs to
    // the camp and appears nowhere else.
    //
    // Enforced here rather than by leaving the object switched off in the arena
    // scene, because a checkbox is one stray click from being back on and this is
    // a rule, not a scene-authoring accident. Matches GameSceneManager's own
    // default camp scene name.
    private const string CampSceneName = "Camp";

    [SerializeField] private TMP_Text goldText;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != CampSceneName)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        GoldManager.OnGoldChanged += HandleGoldChanged;
        if (GoldManager.Instance != null)
        {
            HandleGoldChanged(GoldManager.Instance.Gold);
        }
    }

    private void OnDisable()
    {
        GoldManager.OnGoldChanged -= HandleGoldChanged;
    }

    private void HandleGoldChanged(int newGold)
    {
        if (goldText != null)
        {
            goldText.text = newGold.ToString();
        }
    }
}
