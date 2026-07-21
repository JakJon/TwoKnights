#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Dev-only Test Mode panel on the camp: pick a starting wave, pre-assign
// upgrades to each knight within the budget a real run would have earned
// (wave N = N-1 picks, alternating knights), then launch the run.
// Added at runtime by CampMenuController; compiled out of release builds.
public class TestModePanel : MonoBehaviour
{
    private const int MinWave = 1;
    private const int MaxWave = 99;
    private const float EntryGrace = 0.25f;  // swallow the press that opened the panel
    private const float RepeatDelay = 0.35f; // held direction: delay before first repeat
    private const float RepeatInterval = 0.12f;

    private const string ROW_SELECTED_CLASS = "test-row--selected";
    private const string TOGGLE_ON_CLASS = "test-toggle--on";
    private const string BUDGET_OVER_CLASS = "test-budget-line--over";
    private const string START_BLOCKED_CLASS = "test-start-row--blocked";

    private UIDocument _uiDocument;
    private UpgradeManager _upgradeManager;

    private VisualElement _root;
    private VisualElement _panel;
    private ScrollView _list;
    private Label _waveValue;
    private Label _budgetLine;
    private Button _closeButton;

    private enum RowKind { Wave, Randomize, Section, Upgrade, Start }

    private class Row
    {
        public RowKind Kind;
        public VisualElement Element;
        public BaseUpgrade Upgrade;
        public Label LeftToggle;
        public Label RightToggle;
        // Section rows: collapsed by default, expanding reveals Children
        public bool Expanded;
        public string HeaderTitle;
        public readonly List<Row> Children = new();
        // Upgrade rows: the section header they live under
        public Row Section;
    }

    private readonly List<Row> _rows = new();
    private readonly HashSet<BaseUpgrade> _leftPicked = new();
    private readonly HashSet<BaseUpgrade> _rightPicked = new();
    private int _selectedIndex;
    private int _startWave = 1;
    private float _inputReadyTime;
    private int _heldVertical;
    private int _heldHorizontal;
    private float _nextVerticalRepeat;
    private float _nextHorizontalRepeat;

    public Action OnCloseRequested;

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.Flex;
        _inputReadyTime = Time.unscaledTime + EntryGrace;
        SetSelectedIndex(_selectedIndex);
        RefreshAll();
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
    }

    private void BindUI()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("test-panel");
        _list = _root.Q<ScrollView>("test-upgrade-list");
        _waveValue = _root.Q<Label>("test-wave-value");
        _budgetLine = _root.Q<Label>("test-budget-line");
        _closeButton = _root.Q<Button>("test-close-button");
        if (_panel == null || _list == null)
        {
            Debug.LogWarning("TestModePanel: test-panel markup not found in the camp UXML.");
            _panel = null;
            return;
        }

        if (_closeButton != null)
        {
            _closeButton.clicked -= Close;
            _closeButton.clicked += Close;
        }

        if (_upgradeManager == null)
        {
            _upgradeManager = Resources.Load<UpgradeManager>("UpgradeManager");
        }
        if (_upgradeManager == null)
        {
            Debug.LogWarning("TestModePanel: could not load UpgradeManager from Resources.");
        }

        BuildRows();
    }

    private void BuildRows()
    {
        _rows.Clear();
        _list.Clear();

        var waveRow = _root.Q<VisualElement>("test-wave-row");
        if (waveRow != null)
        {
            _rows.Add(new Row { Kind = RowKind.Wave, Element = waveRow });
        }

        // Sits at the top of the upgrade list: one press fills both knights'
        // budgets with random (prerequisite-legal) picks; press again to reroll
        var randomizeRow = new VisualElement();
        randomizeRow.AddToClassList("test-row");
        var randomizeLabel = new Label("Randomize Loadout — fill all slots");
        randomizeLabel.AddToClassList("test-row-name");
        randomizeRow.Add(randomizeLabel);
        var randomizeHint = new Label("A");
        randomizeHint.AddToClassList("test-row-value");
        randomizeRow.Add(randomizeHint);
        int randomizeIndex = _rows.Count;
        randomizeRow.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(randomizeIndex));
        randomizeRow.RegisterCallback<ClickEvent>(_ => RandomizeLoadout());
        _rows.Add(new Row { Kind = RowKind.Randomize, Element = randomizeRow });
        _list.Add(randomizeRow);

        if (_upgradeManager != null)
        {
            // One collapsible section per chain family, one row per upgrade
            // asset inside it. Asset names ("Shadow 3", "Venom Tip 2") are the
            // dev-facing labels.
            var families = _upgradeManager.AllUpgrades
                .Where(u => u != null)
                .GroupBy(u => u.GetType())
                .OrderBy(g => g.First().ChainName);

            foreach (var family in families)
            {
                var header = new Label();
                header.AddToClassList("test-section-header");
                var sectionRow = new Row
                {
                    Kind = RowKind.Section,
                    Element = header,
                    HeaderTitle = family.First().ChainName.ToUpperInvariant()
                };
                int headerIndex = _rows.Count;
                header.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(headerIndex));
                header.RegisterCallback<ClickEvent>(_ => ToggleSection(sectionRow));
                _rows.Add(sectionRow);
                _list.Add(header);

                var tiers = family
                    .OrderBy(u => _upgradeManager.GetChainInfo(u, KnightTarget.LeftKnight).Position)
                    .ThenBy(u => u.name);
                foreach (var upgrade in tiers)
                {
                    _list.Add(CreateUpgradeRow(upgrade, sectionRow));
                }

                UpdateSectionHeader(sectionRow);
            }
        }

        var startRow = _root.Q<VisualElement>("test-start-row");
        if (startRow != null)
        {
            int index = _rows.Count;
            startRow.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
            startRow.RegisterCallback<ClickEvent>(_ => TryStartRun());
            _rows.Add(new Row { Kind = RowKind.Start, Element = startRow });
        }
    }

    private VisualElement CreateUpgradeRow(BaseUpgrade upgrade, Row section)
    {
        var rowElement = new VisualElement();
        rowElement.AddToClassList("test-row");
        rowElement.style.display = DisplayStyle.None; // sections start collapsed

        var nameLabel = new Label(upgrade.name);
        nameLabel.AddToClassList("test-row-name");
        rowElement.Add(nameLabel);

        var toggles = new VisualElement();
        toggles.style.flexDirection = FlexDirection.Row;
        var leftToggle = new Label("L");
        leftToggle.AddToClassList("test-toggle");
        var rightToggle = new Label("R");
        rightToggle.AddToClassList("test-toggle");
        toggles.Add(leftToggle);
        toggles.Add(rightToggle);
        rowElement.Add(toggles);

        int index = _rows.Count;
        rowElement.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
        leftToggle.RegisterCallback<ClickEvent>(_ => ToggleUpgrade(upgrade, KnightTarget.LeftKnight));
        rightToggle.RegisterCallback<ClickEvent>(_ => ToggleUpgrade(upgrade, KnightTarget.RightKnight));

        var row = new Row
        {
            Kind = RowKind.Upgrade,
            Element = rowElement,
            Upgrade = upgrade,
            LeftToggle = leftToggle,
            RightToggle = rightToggle,
            Section = section
        };
        _rows.Add(row);
        section.Children.Add(row);
        return rowElement;
    }

    private void ToggleSection(Row section)
    {
        SetSectionExpanded(section, !section.Expanded);
    }

    private void SetSectionExpanded(Row section, bool expanded)
    {
        if (section.Expanded == expanded) return;
        section.Expanded = expanded;
        foreach (var child in section.Children)
        {
            child.Element.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }
        UpdateSectionHeader(section);

        // Collapsing while a child row is highlighted moves focus to the header
        if (!expanded && _rows[_selectedIndex].Section == section)
        {
            SetSelectedIndex(_rows.IndexOf(section));
        }
    }

    // Collapsed sections still show what's picked inside them
    private void UpdateSectionHeader(Row section)
    {
        if (section.Element is not Label label) return;
        int left = section.Children.Count(c => _leftPicked.Contains(c.Upgrade));
        int right = section.Children.Count(c => _rightPicked.Contains(c.Upgrade));
        string picks = (left > 0 || right > 0) ? $"   —   L {left} · R {right}" : string.Empty;
        label.text = (section.Expanded ? "▾ " : "▸ ") + section.HeaderTitle + picks;
    }

    // The panel polls its own input (dpad/stick/keyboard) so it can offer
    // left/right without new InputActionReferences; CampMenuController swallows
    // its own actions while the panel is visible.
    private void Update()
    {
        if (!IsVisible) return;
        if (Time.unscaledTime < _inputReadyTime) return;

        var gp = Gamepad.current;
        var kb = Keyboard.current;

        bool upHeld = (gp != null && (gp.dpad.up.isPressed || gp.leftStick.up.isPressed))
                      || (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed));
        bool downHeld = (gp != null && (gp.dpad.down.isPressed || gp.leftStick.down.isPressed))
                        || (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed));
        bool leftHeld = (gp != null && (gp.dpad.left.isPressed || gp.leftStick.left.isPressed))
                        || (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed));
        bool rightHeld = (gp != null && (gp.dpad.right.isPressed || gp.leftStick.right.isPressed))
                         || (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed));

        int rawVertical = (downHeld ? 1 : 0) - (upHeld ? 1 : 0);
        int rawHorizontal = (rightHeld ? 1 : 0) - (leftHeld ? 1 : 0);

        int vertical = ApplyRepeat(rawVertical, ref _heldVertical, ref _nextVerticalRepeat, out _);
        int horizontal = ApplyRepeat(rawHorizontal, ref _heldHorizontal, ref _nextHorizontalRepeat, out bool horizontalFirstPress);

        bool confirm = (gp != null && gp.buttonSouth.wasPressedThisFrame)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = (gp != null && gp.buttonEast.wasPressedThisFrame)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);
        // Start (or Tab) skips the list and lands on Begin Run
        bool jumpToStart = (gp != null && gp.startButton.wasPressedThisFrame)
                           || (kb != null && kb.tabKey.wasPressedThisFrame);

        if (vertical != 0) Navigate(vertical);
        else if (horizontal != 0) AdjustCurrentRow(horizontal, horizontalFirstPress);
        else if (jumpToStart) SetSelectedIndex(_rows.FindIndex(r => r.Kind == RowKind.Start));
        else if (confirm) ConfirmCurrentRow();
        else if (cancel) Close();
    }

    // Edge-trigger with hold-to-repeat: fires on the initial press, then again
    // after RepeatDelay, then every RepeatInterval while held
    private int ApplyRepeat(int held, ref int lastHeld, ref float nextRepeat, out bool firstPress)
    {
        firstPress = false;
        if (held == 0)
        {
            lastHeld = 0;
            return 0;
        }
        float now = Time.unscaledTime;
        if (held != lastHeld)
        {
            lastHeld = held;
            nextRepeat = now + RepeatDelay;
            firstPress = true;
            return held;
        }
        if (now >= nextRepeat)
        {
            nextRepeat = now + RepeatInterval;
            return held;
        }
        return 0;
    }

    // Upgrade rows inside a collapsed section are hidden and unnavigable
    private bool IsRowNavigable(Row row)
    {
        return row.Kind != RowKind.Upgrade || (row.Section != null && row.Section.Expanded);
    }

    private void Navigate(int direction)
    {
        if (_rows.Count == 0) return;
        int index = _selectedIndex;
        for (int step = 0; step < _rows.Count; step++)
        {
            index = (index + direction + _rows.Count) % _rows.Count;
            if (IsRowNavigable(_rows[index])) break;
        }
        SetSelectedIndex(index);
    }

    private void SetSelectedIndex(int index)
    {
        if (_rows.Count == 0) return;
        _selectedIndex = Mathf.Clamp(index, 0, _rows.Count - 1);
        for (int i = 0; i < _rows.Count; i++)
        {
            if (i == _selectedIndex) _rows[i].Element.AddToClassList(ROW_SELECTED_CLASS);
            else _rows[i].Element.RemoveFromClassList(ROW_SELECTED_CLASS);
        }

        var selected = _rows[_selectedIndex];
        if (selected.Kind != RowKind.Wave && selected.Kind != RowKind.Start && _list != null)
        {
            _list.ScrollTo(selected.Element);
        }
    }

    private void AdjustCurrentRow(int direction, bool firstPress)
    {
        var row = _rows[_selectedIndex];
        switch (row.Kind)
        {
            case RowKind.Wave:
                _startWave = Mathf.Clamp(_startWave + direction, MinWave, MaxWave);
                RefreshAll();
                break;
            case RowKind.Section:
                // Tree-view convention: right expands, left collapses
                if (firstPress)
                {
                    SetSectionExpanded(row, direction > 0);
                }
                break;
            case RowKind.Upgrade:
                // Toggles must not machine-gun while the stick is held
                if (firstPress)
                {
                    ToggleUpgrade(row.Upgrade, direction < 0 ? KnightTarget.LeftKnight : KnightTarget.RightKnight);
                }
                break;
        }
    }

    private void ConfirmCurrentRow()
    {
        var row = _rows[_selectedIndex];
        switch (row.Kind)
        {
            case RowKind.Start:
                TryStartRun();
                break;
            case RowKind.Section:
                ToggleSection(row);
                break;
            case RowKind.Randomize:
                RandomizeLoadout();
                break;
        }
    }

    // Fill both knights to their exact budget with random picks; prerequisite
    // chains are pulled in whole (and costed as such), mirroring manual toggles
    private void RandomizeLoadout()
    {
        _leftPicked.Clear();
        _rightPicked.Clear();
        FillRandom(_leftPicked, LeftCap);
        FillRandom(_rightPicked, RightCap);
        RefreshAll();
    }

    private void FillRandom(HashSet<BaseUpgrade> picked, int cap)
    {
        var all = new List<BaseUpgrade>();
        foreach (var row in _rows)
        {
            if (row.Kind == RowKind.Upgrade && row.Upgrade != null) all.Add(row.Upgrade);
        }

        int guard = 0;
        while (picked.Count < cap && guard++ < 500)
        {
            var affordable = new List<BaseUpgrade>();
            foreach (var upgrade in all)
            {
                if (picked.Contains(upgrade)) continue;
                int cost = ChainCost(upgrade, picked, new HashSet<BaseUpgrade>());
                if (cost > 0 && picked.Count + cost <= cap) affordable.Add(upgrade);
            }
            if (affordable.Count == 0) break;

            AddWithPrerequisites(affordable[UnityEngine.Random.Range(0, affordable.Count)], picked, new HashSet<BaseUpgrade>());
        }
    }

    // Levels AddWithPrerequisites would spend to pick this upgrade right now
    private int ChainCost(BaseUpgrade upgrade, HashSet<BaseUpgrade> picked, HashSet<BaseUpgrade> visiting)
    {
        if (upgrade == null || picked.Contains(upgrade) || !visiting.Add(upgrade)) return 0;

        int cost = 1;
        var prereqs = upgrade.UnlockedBy;
        if (prereqs != null && prereqs.Count > 0 && !prereqs.Any(p => p != null && picked.Contains(p)))
        {
            var chosen = prereqs.FirstOrDefault(p => p != null && p.GetType() == upgrade.GetType())
                         ?? prereqs.FirstOrDefault(p => p != null);
            cost += ChainCost(chosen, picked, visiting);
        }
        return cost;
    }

    private void ToggleUpgrade(BaseUpgrade upgrade, KnightTarget knight)
    {
        var picked = knight == KnightTarget.LeftKnight ? _leftPicked : _rightPicked;
        if (picked.Remove(upgrade))
        {
            CascadeRemoveOrphans(picked);
        }
        else
        {
            AddWithPrerequisites(upgrade, picked, new HashSet<BaseUpgrade>());
        }
        RefreshAll();
    }

    // A real run can only own an upgrade after one of its unlockedBy entries;
    // mirror that by pulling in a prerequisite chain (preferring the same
    // family, so picking "Shadow 3" costs its whole chain) when none is picked.
    private void AddWithPrerequisites(BaseUpgrade upgrade, HashSet<BaseUpgrade> picked, HashSet<BaseUpgrade> visiting)
    {
        if (upgrade == null || !visiting.Add(upgrade)) return;
        picked.Add(upgrade);

        var prereqs = upgrade.UnlockedBy;
        if (prereqs == null || prereqs.Count == 0) return;
        if (prereqs.Any(p => p != null && picked.Contains(p))) return;

        var chosen = prereqs.FirstOrDefault(p => p != null && p.GetType() == upgrade.GetType())
                     ?? prereqs.FirstOrDefault(p => p != null);
        AddWithPrerequisites(chosen, picked, visiting);
    }

    // Deselecting a prerequisite strands its dependents; drop them until the
    // remaining picks are all reachable again
    private void CascadeRemoveOrphans(HashSet<BaseUpgrade> picked)
    {
        bool removed = true;
        while (removed)
        {
            removed = false;
            foreach (var upgrade in picked.ToList())
            {
                var prereqs = upgrade.UnlockedBy;
                if (prereqs == null || prereqs.Count == 0) continue;
                if (prereqs.Any(p => p != null && picked.Contains(p))) continue;
                picked.Remove(upgrade);
                removed = true;
            }
        }
    }

    private int TotalBudget => _startWave - 1;
    // Picks alternate starting with the left knight, so left gets the odd one
    private int LeftCap => (TotalBudget + 1) / 2;
    private int RightCap => TotalBudget / 2;
    private bool OverBudget => _leftPicked.Count > LeftCap || _rightPicked.Count > RightCap;

    private void RefreshAll()
    {
        if (_waveValue != null)
        {
            _waveValue.text = $"◄ {_startWave} ►";
        }

        foreach (var row in _rows)
        {
            if (row.Kind == RowKind.Section)
            {
                UpdateSectionHeader(row);
            }
            if (row.Kind != RowKind.Upgrade) continue;
            SetToggle(row.LeftToggle, _leftPicked.Contains(row.Upgrade));
            SetToggle(row.RightToggle, _rightPicked.Contains(row.Upgrade));
        }

        if (_budgetLine != null)
        {
            _budgetLine.text = $"Wave {_startWave} grants {TotalBudget} upgrade levels — " +
                               $"Left {_leftPicked.Count}/{LeftCap} · Right {_rightPicked.Count}/{RightCap}";
            if (OverBudget) _budgetLine.AddToClassList(BUDGET_OVER_CLASS);
            else _budgetLine.RemoveFromClassList(BUDGET_OVER_CLASS);
        }

        var startRow = _rows.FirstOrDefault(r => r.Kind == RowKind.Start);
        if (startRow != null)
        {
            if (OverBudget) startRow.Element.AddToClassList(START_BLOCKED_CLASS);
            else startRow.Element.RemoveFromClassList(START_BLOCKED_CLASS);
        }
    }

    private static void SetToggle(Label toggle, bool on)
    {
        if (toggle == null) return;
        if (on) toggle.AddToClassList(TOGGLE_ON_CLASS);
        else toggle.RemoveFromClassList(TOGGLE_ON_CLASS);
    }

    private void TryStartRun()
    {
        if (OverBudget) return;

        TestRunConfig.Set(_startWave, OrderForApplication(_leftPicked), OrderForApplication(_rightPicked));

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadGameScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main");
        }
    }

    // Lower tiers must apply before the upgrades they unlock (boost components
    // build on each other), so order by chain depth
    private IEnumerable<BaseUpgrade> OrderForApplication(HashSet<BaseUpgrade> picked)
    {
        return picked
            .OrderBy(u => _upgradeManager != null ? _upgradeManager.GetChainInfo(u, KnightTarget.LeftKnight).Position : 0)
            .ThenBy(u => u.name)
            .ToList();
    }

    private void Close()
    {
        Hide();
        OnCloseRequested?.Invoke();
    }
}
#endif
