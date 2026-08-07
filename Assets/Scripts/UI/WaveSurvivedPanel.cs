using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Queues quests completed mid-run and shows one "Quest Complete" panel per
/// quest between wave end and the upgrade menu. Spawner drives the playback
/// via ShowPendingPanels().
/// </summary>
public class QuestCompletePanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    private VisualElement _root;
    private VisualElement _overlay;
    private Label _nameLabel;
    private Label _rewardLabel;
    private Button _continueButton;

    private readonly Queue<string> _pending = new();
    private bool _dismissed;

    public static bool IsVisible { get; private set; }

    public bool HasPending => _pending.Count > 0;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    private void OnEnable()
    {
        SetupUI();
        QuestProgress.OnQuestCompleted += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        QuestProgress.OnQuestCompleted -= HandleQuestCompleted;
        IsVisible = false;
    }

    private void HandleQuestCompleted(string questId)
    {
        _pending.Enqueue(questId);
    }

    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("QuestCompletePanel missing UIDocument reference.");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogWarning("QuestCompletePanel root visual element not found.");
            return;
        }

        if (styleSheet != null)
        {
            _root.styleSheets.Add(styleSheet);
        }

        _overlay = _root.Q<VisualElement>("quest-complete-screen");
        _nameLabel = _root.Q<Label>("qc-name");
        _rewardLabel = _root.Q<Label>("qc-reward-amount");
        _continueButton = _root.Q<Button>("qc-continue-button");

        if (_continueButton != null)
        {
            _continueButton.clicked -= OnContinueClicked;
            _continueButton.clicked += OnContinueClicked;
        }

        Hide();
    }

    private bool EnsureUI()
    {
        // rootVisualElement can be null in the first frames after a UXML/USS
        // reimport, so retry setup lazily instead of trusting OnEnable
        if (_root == null || _overlay == null)
        {
            SetupUI();
        }

        return _overlay != null;
    }

    /// <summary>
    /// Shows one panel per queued quest, waiting for Continue between each.
    /// Runs on unscaled frames, so the caller may freeze timeScale first.
    /// </summary>
    public IEnumerator ShowPendingPanels()
    {
        if (!EnsureUI())
        {
            _pending.Clear();
            yield break;
        }

        while (_pending.Count > 0)
        {
            var quest = QuestDatabase.Get(_pending.Dequeue());
            if (quest == null)
            {
                continue;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = $"{quest.Name} Completed";
            }

            if (_rewardLabel != null)
            {
                _rewardLabel.text = $"{quest.HonorReward} Honor";
            }

            _root.style.display = DisplayStyle.Flex;
            IsVisible = true;
            _dismissed = false;
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.questComplete);

            if (_continueButton != null)
            {
                _continueButton.Focus();
            }

            while (!_dismissed)
            {
                yield return null;
            }
        }

        Hide();
    }

    public void Hide()
    {
        IsVisible = false;

        if (_root != null)
        {
            _root.style.display = DisplayStyle.None;
        }
    }

    private void OnContinueClicked()
    {
        _dismissed = true;
    }

    private void OnDestroy()
    {
        IsVisible = false;

        if (_continueButton != null)
        {
            _continueButton.clicked -= OnContinueClicked;
        }
    }
}
