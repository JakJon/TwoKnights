using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class QuestPanel : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private ScrollView _list;
    private Label _detailName;
    private Label _detailDescription;
    private Label _detailProgress;
    private Label _detailReward;
    private Button _closeButton;

    private readonly List<Button> _questButtons = new();
    private int _selectedIndex = -1;

    public System.Action OnCloseRequested;

    private const string SELECTED_CLASS = "quest-list-item--selected";
    private const string COMPLETED_CLASS = "quest-list-item--completed";
    private const string COMPLETED_CHECK = "<color=#F0CD78>✔</color> ";

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        BindUI();
        QuestProgress.OnQuestCompleted += HandleQuestCompleted;
        QuestProgress.OnQuestProgressChanged += HandleQuestProgressChanged;
    }

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.clicked -= HandleCloseClicked;
        QuestProgress.OnQuestCompleted -= HandleQuestCompleted;
        QuestProgress.OnQuestProgressChanged -= HandleQuestProgressChanged;
    }

    private void HandleQuestCompleted(string questId)
    {
        RefreshQuestButtons();
        if (_selectedIndex >= 0) SelectQuest(_selectedIndex);
    }

    private void HandleQuestProgressChanged(string questId)
    {
        if (_selectedIndex >= 0 && _selectedIndex < QuestDatabase.All.Count
            && QuestDatabase.All[_selectedIndex].Id == questId)
        {
            SelectQuest(_selectedIndex);
        }
    }

    private void BindUI()
    {
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("quest-panel");
        _list = _root.Q<ScrollView>("quest-list");
        _detailName = _root.Q<Label>("quest-detail-name");
        _detailDescription = _root.Q<Label>("quest-detail-description");
        _detailProgress = _root.Q<Label>("quest-detail-progress");
        _detailReward = _root.Q<Label>("quest-detail-reward");
        _closeButton = _root.Q<Button>("quest-close-button");

        if (_list != null)
        {
            _list.focusable = false;
            if (_list.contentContainer != null) _list.contentContainer.focusable = false;
        }

        if (_closeButton != null)
        {
            _closeButton.clicked -= HandleCloseClicked;
            _closeButton.clicked += HandleCloseClicked;
        }

        PopulateQuestList();
    }

    private void PopulateQuestList()
    {
        if (_list == null) return;
        _list.Clear();
        _questButtons.Clear();

        var quests = QuestDatabase.All;
        for (int i = 0; i < quests.Count; i++)
        {
            int index = i;
            var quest = quests[i];
            var button = new Button(() => SelectQuest(index))
            {
                name = $"quest-item-{quest.Id}"
            };
            button.enableRichText = true;
            button.AddToClassList("quest-list-item");
            button.focusable = true;
            button.RegisterCallback<FocusInEvent>(_ => SelectQuest(index));
            _list.Add(button);
            _questButtons.Add(button);
        }

        RefreshQuestButtons();

        if (quests.Count > 0) SelectQuest(0);
        else ShowEmptyDetails();
    }

    private void RefreshQuestButtons()
    {
        var quests = QuestDatabase.All;
        for (int i = 0; i < _questButtons.Count && i < quests.Count; i++)
        {
            var quest = quests[i];
            var button = _questButtons[i];
            bool completed = QuestProgress.IsCompleted(quest.Id);
            button.text = completed ? COMPLETED_CHECK + quest.Name : quest.Name;
            if (completed) button.AddToClassList(COMPLETED_CLASS);
            else button.RemoveFromClassList(COMPLETED_CLASS);
        }
    }

    private void SelectQuest(int index)
    {
        var quests = QuestDatabase.All;
        if (index < 0 || index >= quests.Count) return;

        _selectedIndex = index;
        for (int i = 0; i < _questButtons.Count; i++)
        {
            if (i == index)
            {
                _questButtons[i].AddToClassList(SELECTED_CLASS);
                _questButtons[i].Focus();
            }
            else
            {
                _questButtons[i].RemoveFromClassList(SELECTED_CLASS);
            }
        }

        var quest = quests[index];
        if (_detailName != null) _detailName.text = quest.Name;
        if (_detailDescription != null) _detailDescription.text = quest.Description;
        if (_detailProgress != null)
        {
            if (quest.HasObjective)
            {
                int current = QuestProgress.GetProgress(quest);
                string label = StatsDatabase.GetShortLabel(quest.ObjectiveStatKey);
                _detailProgress.text = $"{current}/{quest.ObjectiveTarget} {label}";
                _detailProgress.style.display = DisplayStyle.Flex;
            }
            else
            {
                _detailProgress.text = string.Empty;
                _detailProgress.style.display = DisplayStyle.None;
            }
        }
        if (_detailReward != null)
        {
            if (QuestProgress.IsCompleted(quest.Id))
            {
                var date = QuestProgress.GetCompletionDate(quest.Id);
                _detailReward.text = $"COMPLETED {date}";
                _detailReward.RemoveFromClassList("quest-detail-reward--active");
                _detailReward.AddToClassList("quest-detail-reward--completed");
            }
            else
            {
                _detailReward.text = $"Reward: {quest.HonorReward} Honor";
                _detailReward.RemoveFromClassList("quest-detail-reward--completed");
                _detailReward.AddToClassList("quest-detail-reward--active");
            }
        }
    }

    public void NavigateUp()
    {
        if (_questButtons.Count == 0) return;
        int next = _selectedIndex <= 0 ? _questButtons.Count - 1 : _selectedIndex - 1;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectQuest(next);
    }

    public void NavigateDown()
    {
        if (_questButtons.Count == 0) return;
        int next = (_selectedIndex + 1) % _questButtons.Count;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectQuest(next);
    }

    public void Confirm()
    {
        HandleCloseClicked();
    }

    private void ShowEmptyDetails()
    {
        if (_detailName != null) _detailName.text = "No quests available";
        if (_detailDescription != null) _detailDescription.text = string.Empty;
        if (_detailProgress != null) _detailProgress.text = string.Empty;
        if (_detailReward != null) _detailReward.text = string.Empty;
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.Flex;
        if (_questButtons.Count > 0)
        {
            int target = Mathf.Clamp(_selectedIndex < 0 ? 0 : _selectedIndex, 0, _questButtons.Count - 1);
            SelectQuest(target);
            var toFocus = _questButtons[target];
            _panel.schedule.Execute(() => toFocus.Focus()).StartingIn(0);
        }
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
    }

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    private void HandleCloseClicked()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        Hide();
        OnCloseRequested?.Invoke();
    }
}
