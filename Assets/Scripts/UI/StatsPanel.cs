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

        // Count + icon, like every other crystal amount — never a bare number that
        // leans on the row label to say what it counts
        AddRow("Crystals", CrystalText.Build(CrystalBank.Balance, 14f));
        AddRow("Gold",
            (GoldManager.Instance != null ? GoldManager.Instance.Gold : SaveManager.Data.gold).ToString());
        AddRow("Furthest Wave", SaveManager.Data.furthestWave.ToString());
    }

    private void AddRow(string label, string value)
    {
        var valueEl = new Label(value);
        AddRow(label, valueEl);
    }

    /// <summary>
    /// Same row, but with a built element as the value — for anything that is not
    /// plain text, such as a crystal amount.
    /// </summary>
    private void AddRow(string label, VisualElement value)
    {
        var row = new VisualElement();
        row.AddToClassList("stats-row");
        var labelEl = new Label(label);
        labelEl.AddToClassList("stats-label");
        value.AddToClassList("stats-value");
        row.Add(labelEl);
        row.Add(value);
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

    // The stats list is longer than the screen and has no selectable rows, so
    // without these a controller could open it and never reach the bottom.
    private const float ScrollStep = 48f;

    public void ScrollUp() => Scroll(-ScrollStep);
    public void ScrollDown() => Scroll(ScrollStep);

    private void Scroll(float delta)
    {
        if (_list == null) return;
        var offset = _list.scrollOffset;
        float max = Mathf.Max(0f, _list.contentContainer.layout.height - _list.contentViewport.layout.height);
        offset.y = Mathf.Clamp(offset.y + delta, 0f, max);
        _list.scrollOffset = offset;
    }

    private void HandleCloseClicked()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        Hide();
        OnCloseRequested?.Invoke();
    }
}
