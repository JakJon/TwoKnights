using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The beat between a cleared wave and whatever comes next: a second's pause and
/// the wave-complete sound. It used to put the words "Wave Survived" over the
/// arena for that second; the owner took them off (2026-10-13) and kept the
/// pause and the sound. The overlay and its style sheet are still wired, so
/// showing the words again is the one line marked in ShowWaveSurvived.
///
/// What follows is the history of the words themselves.
///
/// It used to carry the quest news as well — headings, one row per objective, a
/// reward line, and a wait for input whenever a quest finished or opened. All of
/// that is now a scene with a person in it (see QuestScene), so what is left here is
/// the acknowledgement it was always meant to be: one second, no panel, no button.
///
/// The panel went with the list. Type alone had needed a raised plate to compete
/// with fire fields and lit ground for every letter; two words held for a second can
/// carry a shadow instead and let the field the player just cleared stay visible
/// behind them.
/// </summary>
public class WaveSurvivedPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    [Tooltip("How long the words linger before the run moves on.")]
    [SerializeField] private float autoAdvanceSeconds = 1f;

    private VisualElement _root;
    private VisualElement _overlay;

    public static bool IsVisible { get; private set; }

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        SetupUI();
    }

    private void OnDisable()
    {
        IsVisible = false;
    }

    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("WaveSurvivedPanel missing UIDocument reference.");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        if (styleSheet != null) _root.styleSheets.Add(styleSheet);
        _overlay = _root.Q<VisualElement>("wave-survived-screen");

        Hide();
    }

    private bool EnsureUI()
    {
        // rootVisualElement can be null in the first frames after a UXML/USS
        // reimport, so retry setup lazily instead of trusting OnEnable
        if (_root == null || _overlay == null) SetupUI();
        return _overlay != null;
    }

    /// <summary>
    /// Holds the beat and returns when the run should move on. Runs on unscaled
    /// time, so the caller may freeze timeScale first. IsVisible is still up for
    /// the length of it, which is what keeps the pause menu from opening inside it.
    /// </summary>
    public IEnumerator ShowWaveSurvived()
    {
        // Still called, though nothing is shown: it retries the set-up that hides
        // the overlay, for the case where OnEnable ran before the document was ready.
        // To bring the words back: if (EnsureUI()) _root.style.display = DisplayStyle.Flex;
        EnsureUI();
        IsVisible = true;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.waveComplete);

        yield return new WaitForSecondsRealtime(autoAdvanceSeconds);
        Hide();
    }

    public void Hide()
    {
        IsVisible = false;
        if (_root != null) _root.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        IsVisible = false;
    }
}
