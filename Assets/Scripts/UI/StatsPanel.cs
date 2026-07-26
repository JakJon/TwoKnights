using UnityEngine;
using UnityEngine.UIElements;

public class StatsPanel : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private ScrollView _list;
    private Button _closeButton;

    public System.Action OnCloseRequested;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        BindUI();
        PlayerStats.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.clicked -= HandleCloseClicked;
        PlayerStats.OnStatChanged -= HandleStatChanged;
    }

    private void HandleStatChanged(string key, int value)
    {
        if (IsVisible) Populate();
    }

    private void BindUI()
    {
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("stats-panel");
        _list = _root.Q<ScrollView>("stats-list");
        _closeButton = _root.Q<Button>("stats-close-button");

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
    }

    private void Populate()
    {
        if (_list == null) return;
        _list.Clear();

        foreach (var def in StatsDatabase.Definitions)
        {
            int value = PlayerStats.Get(def.Key);
            AddRow(def.DisplayName, value.ToString());
        }

        AddRow("Honor Points",
            (KnightRankManager.Instance != null ? KnightRankManager.Instance.HonorPoints : SaveManager.Data.honorPoints).ToString());
        AddRow("Knight Rank",
            (KnightRankManager.Instance != null ? KnightRankManager.Instance.KnightRank : SaveManager.Data.knightRank).ToString());
        AddRow("Gold",
            (GoldManager.Instance != null ? GoldManager.Instance.Gold : SaveManager.Data.gold).ToString());
        AddRow("Furthest Wave", SaveManager.Data.furthestWave.ToString());
    }

    private void AddRow(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("stats-row");
        var labelEl = new Label(label);
        labelEl.AddToClassList("stats-label");
        var valueEl = new Label(value);
        valueEl.AddToClassList("stats-value");
        row.Add(labelEl);
        row.Add(valueEl);
        _list.Add(row);
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.Flex;
        Populate();
        if (_closeButton != null)
        {
            var toFocus = _closeButton;
            _panel.schedule.Execute(() => toFocus.Focus()).StartingIn(0);
        }
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
    }

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    public void Confirm() => HandleCloseClicked();

    private void HandleCloseClicked()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        Hide();
        OnCloseRequested?.Invoke();
    }
}
