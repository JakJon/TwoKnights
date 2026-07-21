#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// Dev-only Test Mode: shown by the Spawner before every wave of a test run so
// the tester chooses exactly which wave comes next. Builds its whole UI in
// code on a runtime UIDocument (PanelSettings borrowed from any scene panel),
// so the Main scene needs no wiring. Compiled out of release builds.
public class TestWavePicker : MonoBehaviour
{
    private const float EntryGrace = 0.25f;  // swallow the press that closed the upgrade menu
    private const float RepeatDelay = 0.35f;
    private const float RepeatInterval = 0.12f;

    // Gilded Vigil palette (inline — this document has no stylesheet)
    private static readonly Color Ground = new Color32(22, 18, 12, 235);
    private static readonly Color Panel = new Color32(28, 22, 14, 255);
    private static readonly Color Card = new Color32(33, 26, 16, 255);
    private static readonly Color Lifted = new Color32(42, 33, 19, 255);
    private static readonly Color Gold = new Color32(212, 162, 74, 255);
    private static readonly Color Text = new Color32(217, 207, 184, 255);
    private static readonly Color Dim = new Color32(141, 130, 102, 255);
    private static readonly Color Bright = new Color32(242, 234, 217, 255);

    private class Entry
    {
        public BaseWave Wave; // null = "Random" row or the locked-section header
        public bool IsLockedHeader;
        public bool IsLockedWave;
        public VisualElement Element;
        public Label Name;
    }

    private static TestWavePicker _instance;

    private UIDocument _uiDocument;
    private VisualElement _overlay;
    private ScrollView _scroll;
    private readonly List<Entry> _entries = new();
    private int _selectedIndex;
    private bool _lockedExpanded;
    private int _lockedCount;
    private Action<BaseWave> _onChosen;
    private float _inputReadyTime;
    private int _heldVertical;
    private float _nextVerticalRepeat;

    public bool IsVisible => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

    // Piggybacks on an existing UIDocument's PanelSettings; returns null when
    // the scene has no UI at all (caller falls back to normal selection)
    public static TestWavePicker GetOrCreate()
    {
        if (_instance != null) return _instance;

        var host = FindFirstObjectByType<UIDocument>(FindObjectsInactive.Include);
        if (host == null || host.panelSettings == null) return null;

        var go = new GameObject("TestWavePicker");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = host.panelSettings;
        doc.sortingOrder = 1000; // above every gameplay panel
        _instance = go.AddComponent<TestWavePicker>();
        _instance._uiDocument = doc;
        return _instance;
    }

    public void Show(BaseWave scheduledBoss, List<BaseWave> pool, List<BaseWave> locked, int waveNumber, Action<BaseWave> onChosen)
    {
        _onChosen = onChosen;
        _lockedExpanded = false; // locked section always starts collapsed
        BuildUI(scheduledBoss, pool, locked, waveNumber);
        _inputReadyTime = Time.unscaledTime + EntryGrace;
        SetSelectedIndex(0);
    }

    private void BuildUI(BaseWave scheduledBoss, List<BaseWave> pool, List<BaseWave> locked, int waveNumber)
    {
        var root = _uiDocument.rootVisualElement;
        root.Clear();
        _entries.Clear();

        _overlay = new VisualElement();
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.right = 0;
        _overlay.style.top = 0;
        _overlay.style.bottom = 0;
        _overlay.style.backgroundColor = Ground;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.justifyContent = Justify.Center;
        root.Add(_overlay);

        var panel = new VisualElement();
        panel.style.width = 560;
        panel.style.maxHeight = Length.Percent(88);
        panel.style.backgroundColor = Panel;
        panel.style.borderTopLeftRadius = 6;
        panel.style.borderTopRightRadius = 6;
        panel.style.borderBottomLeftRadius = 6;
        panel.style.borderBottomRightRadius = 6;
        panel.style.paddingTop = 18;
        panel.style.paddingBottom = 14;
        panel.style.paddingLeft = 22;
        panel.style.paddingRight = 22;
        _overlay.Add(panel);

        var title = new Label($"TEST MODE — WAVE {waveNumber}");
        title.style.fontSize = 20;
        title.style.color = Gold;
        title.style.letterSpacing = 2;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(title);

        string sub = scheduledBoss != null
            ? "Choose the next wave — normal play would force the boss"
            : "Choose the next wave";
        var subtitle = new Label(sub);
        subtitle.style.fontSize = 12;
        subtitle.style.color = Dim;
        subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitle.style.marginBottom = 12;
        panel.Add(subtitle);

        _scroll = new ScrollView();
        _scroll.style.flexGrow = 1;
        panel.Add(_scroll);

        if (scheduledBoss != null)
        {
            AddEntry(scheduledBoss, $"{scheduledBoss.WaveName}", "scheduled boss");
        }

        float totalWeight = 0f;
        foreach (var wave in pool) totalWeight += wave.Weight;
        foreach (var wave in pool)
        {
            string odds = totalWeight > 0f ? $"{wave.Weight / totalWeight * 100f:0}%" : "";
            // Asset name in brackets — display names repeat across difficulty tiers
            AddEntry(wave, $"{wave.WaveName}  <color=#8D8266>[{wave.name}]</color>", odds);
        }

        // Waves outside their unlock window: collapsed by default, but a dev
        // can expand the section and force one anyway
        _lockedCount = locked != null ? locked.Count : 0;
        if (_lockedCount > 0)
        {
            var header = AddEntry(null, "", "");
            header.IsLockedHeader = true;
            foreach (var wave in locked)
            {
                var entry = AddEntry(wave, $"{wave.WaveName}  <color=#8D8266>[{wave.name}]</color>", "locked");
                entry.IsLockedWave = true;
                entry.Element.style.display = DisplayStyle.None;
                entry.Name.style.color = Dim;
            }
            UpdateLockedHeader(header);
        }

        AddEntry(null, "Random (normal selection)", "");

        var hint = new Label("▲▼ move   A choose   B random");
        hint.style.fontSize = 11;
        hint.style.color = Dim;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.style.marginTop = 10;
        panel.Add(hint);

        _overlay.style.display = DisplayStyle.Flex;
    }

    private Entry AddEntry(BaseWave wave, string label, string detail)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 9;
        row.style.paddingBottom = 9;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 12;
        row.style.marginBottom = 2;
        row.style.backgroundColor = Card;
        row.style.borderLeftWidth = 3;
        row.style.borderLeftColor = Color.clear;

        var name = new Label(label);
        name.enableRichText = true;
        name.style.fontSize = 14;
        name.style.color = Text;
        row.Add(name);

        if (!string.IsNullOrEmpty(detail))
        {
            var detailLabel = new Label(detail);
            detailLabel.style.fontSize = 12;
            detailLabel.style.color = wave != null && detail == "scheduled boss" ? Gold : Dim;
            detailLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(detailLabel);
        }

        var entry = new Entry { Wave = wave, Element = row, Name = name };
        int index = _entries.Count;
        row.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
        row.RegisterCallback<ClickEvent>(_ => Activate(entry));
        _entries.Add(entry);
        _scroll.Add(row);
        return entry;
    }

    private void Activate(Entry entry)
    {
        if (entry.IsLockedHeader) ToggleLocked();
        else Choose(entry.Wave);
    }

    private void ToggleLocked()
    {
        _lockedExpanded = !_lockedExpanded;
        foreach (var entry in _entries)
        {
            if (entry.IsLockedWave)
            {
                entry.Element.style.display = _lockedExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else if (entry.IsLockedHeader)
            {
                UpdateLockedHeader(entry);
            }
        }

        // Collapsing while a locked row is highlighted moves focus to the header
        if (!_lockedExpanded && _entries[_selectedIndex].IsLockedWave)
        {
            SetSelectedIndex(_entries.FindIndex(e => e.IsLockedHeader));
        }
    }

    private void UpdateLockedHeader(Entry header)
    {
        header.Name.text = (_lockedExpanded ? "▾" : "▸") + $" LOCKED WAVES ({_lockedCount})";
    }

    private bool IsNavigable(Entry entry)
    {
        return !entry.IsLockedWave || _lockedExpanded;
    }

    private void SetSelectedIndex(int index)
    {
        if (_entries.Count == 0) return;
        _selectedIndex = Mathf.Clamp(index, 0, _entries.Count - 1);
        for (int i = 0; i < _entries.Count; i++)
        {
            bool selected = i == _selectedIndex;
            _entries[i].Element.style.backgroundColor = selected ? Lifted : Card;
            _entries[i].Element.style.borderLeftColor = selected ? Gold : Color.clear;
            _entries[i].Name.style.color = selected ? Bright : (_entries[i].IsLockedWave ? Dim : Text);
        }
        _scroll?.ScrollTo(_entries[_selectedIndex].Element);
    }

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
        int rawVertical = (downHeld ? 1 : 0) - (upHeld ? 1 : 0);

        int vertical = ApplyRepeat(rawVertical, ref _heldVertical, ref _nextVerticalRepeat);

        bool confirm = (gp != null && gp.buttonSouth.wasPressedThisFrame)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = (gp != null && gp.buttonEast.wasPressedThisFrame)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (vertical != 0 && _entries.Count > 0)
        {
            int index = _selectedIndex;
            for (int step = 0; step < _entries.Count; step++)
            {
                index = (index + vertical + _entries.Count) % _entries.Count;
                if (IsNavigable(_entries[index])) break;
            }
            SetSelectedIndex(index);
        }
        else if (confirm && _entries.Count > 0)
        {
            Activate(_entries[_selectedIndex]);
        }
        else if (cancel)
        {
            Choose(null); // B = let the normal weighted draw decide
        }
    }

    private int ApplyRepeat(int held, ref int lastHeld, ref float nextRepeat)
    {
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
            return held;
        }
        if (now >= nextRepeat)
        {
            nextRepeat = now + RepeatInterval;
            return held;
        }
        return 0;
    }

    private void Choose(BaseWave wave)
    {
        if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        var callback = _onChosen;
        _onChosen = null;
        callback?.Invoke(wave);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
#endif
