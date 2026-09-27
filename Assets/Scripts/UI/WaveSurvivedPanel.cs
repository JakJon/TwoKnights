using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The beat between a cleared wave and whatever comes next: two words over the
/// arena, and nothing else.
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
    /// Shows the words and returns when the run should move on. Runs on unscaled
    /// time, so the caller may freeze timeScale first.
    /// </summary>
    public IEnumerator ShowWaveSurvived()
    {
        if (!EnsureUI()) yield break;

        _root.style.display = DisplayStyle.Flex;
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
