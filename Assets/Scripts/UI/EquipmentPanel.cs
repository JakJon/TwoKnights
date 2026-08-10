using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// What each knight carries: one column per knight, a row per equipment slot
/// plus a row for their special.
///
/// Polls its own input, like MapSelectPanel and TestModePanel, because it needs
/// left/right to switch knights and the camp's action set is only
/// up/down/confirm/cancel. CampMenuController goes quiet while this is up —
/// it must be listed in PanelPollsOwnInput or both react to the same press.
/// </summary>
public class EquipmentPanel : MonoBehaviour
{
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    private const string SLOT_CLASS = "equip-slot";
    private const string SLOT_SELECTED = "equip-slot--selected";
    private const string SLOT_EMPTY = "equip-slot--empty";
    // Specials are a different kind of thing from equipment, so their rows are a
    // different surface — at two of each, a column of identical rows stopped
    // reading as two groups at all
    private const string SLOT_SPECIAL = "equip-slot--special";
    private const string PICK_CLASS = "equip-pick";
    private const string PICK_SELECTED = "equip-pick--selected";

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private VisualElement _body;
    private ScrollView _picker;
    private Label _detail;
    private Label _hint;
    private Button _closeButton;

    // Rows are rebuilt on every change, so this holds only what the current
    // layout needs. Column 0 is the left knight, 1 the right.
    private readonly List<VisualElement>[] _slotRows = { new List<VisualElement>(), new List<VisualElement>() };
    private int _knight;
    private int _slot;

    private bool _pickerOpen;
    private readonly List<string> _candidates = new List<string>(); // "" = the empty choice
    private readonly List<VisualElement> _candidateRows = new List<VisualElement>();
    private int _pickIndex;

    private float _inputReadyTime;
    private int _heldHorizontal, _heldVertical;
    private float _nextHorizontalRepeat, _nextVerticalRepeat;

    public Action OnCloseRequested;

    /// <summary>
    /// Show what is carried without letting it be changed. The pause screen sets
    /// this: Spawner applies the loadout once, at the start of the run, so a swap
    /// made mid-run would write the save and change nothing until the next one.
    /// Better to show no picker than to offer a change that does not happen.
    /// </summary>
    public bool ReadOnly { get; set; }

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    /// <summary>Rows per knight: the equipment slots, then the special slots.</summary>
    private int RowCount => Loadout.SlotCount + Loadout.SpecialSlotCount;
    private bool OnSpecialRow => _slot >= Loadout.SlotCount;
    /// <summary>Which special slot the cursor is on. Meaningless off a special row.</summary>
    private int SpecialSlot => _slot - Loadout.SlotCount;
    private string KnightId => _knight == 0 ? Loadout.LeftKnight : Loadout.RightKnight;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable() { BindUI(); }

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.clicked -= Close;
    }

    private void BindUI()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("equipment-panel");
        _body = _root.Q<VisualElement>("equipment-body");
        _picker = _root.Q<ScrollView>("equipment-picker");
        _detail = _root.Q<Label>("equipment-detail");
        if (_detail != null) _detail.enableRichText = true;
        _hint = _root.Q<Label>("equipment-hint");
        _closeButton = _root.Q<Button>("equipment-close-button");

        if (_picker != null)
        {
            _picker.focusable = false;
            if (_picker.contentContainer != null) _picker.contentContainer.focusable = false;
        }

        if (_closeButton != null)
        {
            _closeButton.clicked -= Close;
            _closeButton.clicked += Close;
        }
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null)
        {
            Debug.LogWarning("EquipmentPanel: no 'equipment-panel' element in the camp UXML.");
            return;
        }

        _pickerOpen = false;
        _slot = Mathf.Clamp(_slot, 0, RowCount - 1);
        Rebuild();
        _panel.style.display = DisplayStyle.Flex;
        // Swallow the button press that opened this
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldHorizontal = 0;
        _heldVertical = 0;
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
        if (_picker != null) _picker.style.display = DisplayStyle.None;
        _pickerOpen = false;
    }

    // ---------- layout ----------

    private void Rebuild()
    {
        if (_body == null) return;
        _body.Clear();
        _slotRows[0].Clear();
        _slotRows[1].Clear();

        BuildColumn(0, "Left Knight", Loadout.LeftKnight);
        BuildColumn(1, "Right Knight", Loadout.RightKnight);

        RefreshSelection();
    }

    private void BuildColumn(int column, string title, string knightId)
    {
        var col = new VisualElement();
        col.AddToClassList("equip-column");

        var header = new Label(title);
        header.AddToClassList("equip-column-title");
        col.Add(header);

        int slots = Loadout.SlotCount;
        for (int slot = 0; slot < slots; slot++)
        {
            var def = Loadout.EquippedAt(knightId, slot);
            col.Add(BuildRow(column, slot, "EQUIPMENT",
                             def != null ? def.DisplayName : "(empty)", def == null,
                             def != null ? def.Icon : null, false));
        }

        int specialSlots = Loadout.SpecialSlotCount;
        for (int slot = 0; slot < specialSlots; slot++)
        {
            var special = Loadout.ResolveSpecial(knightId, null, slot);
            col.Add(BuildRow(column, slots + slot, "SPECIAL",
                             special != null ? special.DisplayName : "(none)", special == null,
                             special != null ? special.Icon : null, true));
        }

        _body.Add(col);
    }

    private VisualElement BuildRow(int column, int slot, string kind, string value, bool empty, Sprite icon, bool special)
    {
        var row = new VisualElement();
        row.AddToClassList(SLOT_CLASS);
        if (special) row.AddToClassList(SLOT_SPECIAL);
        if (empty) row.AddToClassList(SLOT_EMPTY);

        // Art plus the item name on the left, the slot's kind flush right —
        // an empty slot still shows the frame so the row height never jumps
        var left = new VisualElement();
        left.AddToClassList("equip-slot-left");

        var iconElement = new VisualElement();
        iconElement.AddToClassList("row-icon");
        if (icon != null) iconElement.style.backgroundImage = new StyleBackground(icon);
        left.Add(iconElement);

        var valueLabel = new Label(value);
        valueLabel.AddToClassList("equip-slot-value");
        left.Add(valueLabel);
        row.Add(left);

        var kindLabel = new Label(kind);
        kindLabel.AddToClassList("equip-slot-kind");
        row.Add(kindLabel);

        // PointerMove, not MouseEnter: a controller scrolling under a stationary
        // cursor otherwise steals the selection back every frame
        row.RegisterCallback<PointerMoveEvent>(_ =>
        {
            if (_pickerOpen) return;
            _knight = column; _slot = slot; RefreshSelection();
        });
        row.RegisterCallback<ClickEvent>(_ =>
        {
            if (_pickerOpen) return;
            _knight = column; _slot = slot; RefreshSelection(); OpenPicker();
        });

        _slotRows[column].Add(row);
        return row;
    }

    private void RefreshSelection()
    {
        _slot = Mathf.Clamp(_slot, 0, RowCount - 1);
        for (int c = 0; c < 2; c++)
        {
            for (int r = 0; r < _slotRows[c].Count; r++)
            {
                bool on = !_pickerOpen && c == _knight && r == _slot;
                if (on) _slotRows[c][r].AddToClassList(SLOT_SELECTED);
                else _slotRows[c][r].RemoveFromClassList(SLOT_SELECTED);
            }
        }
        UpdateDetail();
        UpdateHint();
    }

    private void UpdateDetail()
    {
        if (_detail == null) return;

        if (_pickerOpen)
        {
            _detail.text = DescribeCandidate(_pickIndex);
            return;
        }

        if (OnSpecialRow)
        {
            var special = Loadout.ResolveSpecial(KnightId, null, SpecialSlot);
            _detail.text = special != null ? Describe(special.Effect, special.Description)
                                           : "Nothing in this special slot.";
        }
        else
        {
            var def = Loadout.EquippedAt(KnightId, _slot);
            _detail.text = def != null ? Describe(def.Effect, def.Description)
                                       : "Nothing carried in this slot.";
        }
    }

    // What it does, then what it feels like — the same order the shop uses
    private static string Describe(string effect, string flavor)
    {
        if (string.IsNullOrEmpty(effect)) return flavor;
        if (string.IsNullOrEmpty(flavor)) return effect;
        return effect + "\n" + flavor;
    }

    private void UpdateHint()
    {
        if (_hint == null) return;
        if (ReadOnly)
        {
            _hint.text = "◄► knight   ▲▼ slot   B back   ·   change this in camp";
            return;
        }
        _hint.text = _pickerOpen
            ? "▲▼ choose   A equip   B cancel"
            : "◄► knight   ▲▼ slot   A change   B back";
    }

    // ---------- the picker ----------

    private void OpenPicker()
    {
        if (_picker == null || ReadOnly) return;

        _candidates.Clear();
        _candidateRows.Clear();
        _picker.Clear();

        if (OnSpecialRow)
        {
            // Carrying no special at all is a legitimate loadout, and two forest
            // quests ask for exactly it
            _candidates.Add(Loadout.NoSpecial);
            foreach (var special in Loadout.OwnedSpecials()) _candidates.Add(special.Id);
        }
        else
        {
            _candidates.Add(""); // taking the slot back off is a real choice
            foreach (var def in Loadout.OwnedEquipment())
            {
                // Anything the OTHER knight holds is still offered: picking it
                // moves it across, which is the whole point of one shared item
                _candidates.Add(def.Id);
            }
        }

        if (_candidates.Count == 0)
        {
            _detail.text = "Nothing to put here yet.";
            return;
        }

        for (int i = 0; i < _candidates.Count; i++)
        {
            int index = i;
            var row = new VisualElement();
            row.AddToClassList(PICK_CLASS);

            var left = new VisualElement();
            left.AddToClassList("equip-pick-left");

            var pickIcon = new VisualElement();
            pickIcon.AddToClassList("row-icon");
            var candidateSprite = CandidateIcon(i);
            if (candidateSprite != null) pickIcon.style.backgroundImage = new StyleBackground(candidateSprite);
            left.Add(pickIcon);

            var name = new Label(CandidateName(i));
            name.AddToClassList("equip-pick-name");
            left.Add(name);
            row.Add(left);

            string note = CandidateNote(i);
            if (!string.IsNullOrEmpty(note))
            {
                var noteLabel = new Label(note);
                noteLabel.AddToClassList("equip-pick-note");
                row.Add(noteLabel);
            }

            row.RegisterCallback<PointerMoveEvent>(_ => { _pickIndex = index; RefreshPicker(); });
            row.RegisterCallback<ClickEvent>(_ => { _pickIndex = index; CommitPick(); });

            _picker.Add(row);
            _candidateRows.Add(row);
        }

        _pickIndex = CurrentCandidateIndex();
        _pickerOpen = true;
        _picker.style.display = DisplayStyle.Flex;
        RefreshPicker();
        RefreshSelection();
    }

    private int CurrentCandidateIndex()
    {
        string current = OnSpecialRow
            ? SpecialIdOrEmpty()
            : (Loadout.EquippedAt(KnightId, _slot) != null ? Loadout.EquippedAt(KnightId, _slot).Id : "");

        int found = _candidates.IndexOf(current);
        return found >= 0 ? found : 0;
    }

    private string SpecialIdOrEmpty()
    {
        if (Loadout.IsSpecialSlotEmpty(KnightId, SpecialSlot)) return Loadout.NoSpecial;
        var special = Loadout.ResolveSpecial(KnightId, null, SpecialSlot);
        return special != null ? special.Id : "";
    }

    private string CandidateName(int index)
    {
        string id = _candidates[index];
        if (id == Loadout.NoSpecial) return "(none)";
        if (string.IsNullOrEmpty(id)) return "(empty)";
        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) return id;
        if (OnSpecialRow)
        {
            var special = catalog.FindSpecial(id);
            return special != null ? special.DisplayName : id;
        }
        var def = catalog.Find(id);
        return def != null ? def.DisplayName : id;
    }

    private Sprite CandidateIcon(int index)
    {
        string id = _candidates[index];
        if (string.IsNullOrEmpty(id) || id == Loadout.NoSpecial) return null;
        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) return null;
        if (OnSpecialRow)
        {
            var special = catalog.FindSpecial(id);
            return special != null ? special.Icon : null;
        }
        var def = catalog.Find(id);
        return def != null ? def.Icon : null;
    }

    // "carried by the other knight" is the note that explains why picking this
    // will quietly empty a slot across the screen
    private string CandidateNote(int index)
    {
        string id = _candidates[index];
        if (string.IsNullOrEmpty(id)) return null;
        if (OnSpecialRow)
        {
            // Picking a special the knight already holds swaps the two slots
            // rather than duplicating it, so say so before they press A
            if (id == Loadout.NoSpecial) return null;
            int slots = Loadout.SpecialSlotCount;
            for (int slot = 0; slot < slots; slot++)
            {
                if (slot != SpecialSlot && Loadout.SpecialIdFor(KnightId, slot) == id) return "other slot";
            }
            return null;
        }
        string holder = Loadout.KnightHolding(id);
        if (holder == null || holder == KnightId) return null;
        return holder == Loadout.LeftKnight ? "left knight" : "right knight";
    }

    private string DescribeCandidate(int index)
    {
        if (index < 0 || index >= _candidates.Count) return "";
        string id = _candidates[index];
        if (id == Loadout.NoSpecial)
        {
            // With a second slot open, emptying one is no longer the same
            // statement as carrying nothing — the other slot still fires
            return Loadout.SpecialSlotCount > 1
                ? "Leave this slot empty."
                : "Fire no special at all. The bar still fills; there is simply nothing to spend it on.";
        }
        if (string.IsNullOrEmpty(id)) return "Carry nothing in this slot.";
        var catalog = EquipmentCatalog.Instance;
        if (catalog == null) return "";
        if (OnSpecialRow)
        {
            var special = catalog.FindSpecial(id);
            return special != null ? Describe(special.Effect, special.Description) : "";
        }
        var def = catalog.Find(id);
        return def != null ? Describe(def.Effect, def.Description) : "";
    }

    private void RefreshPicker()
    {
        for (int i = 0; i < _candidateRows.Count; i++)
        {
            if (i == _pickIndex) _candidateRows[i].AddToClassList(PICK_SELECTED);
            else _candidateRows[i].RemoveFromClassList(PICK_SELECTED);
        }
        // Keep the highlighted candidate on screen — the picker scrolls once a
        // knight owns more than a handful of items
        if (_picker != null && _pickIndex >= 0 && _pickIndex < _candidateRows.Count)
        {
            _picker.ScrollTo(_candidateRows[_pickIndex]);
        }
        UpdateDetail();
    }

    private void CommitPick()
    {
        if (_pickIndex < 0 || _pickIndex >= _candidates.Count) { ClosePicker(); return; }
        string id = _candidates[_pickIndex];

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);

        if (OnSpecialRow)
        {
            Loadout.SetSpecial(KnightId, SpecialSlot, id);
        }
        else if (string.IsNullOrEmpty(id))
        {
            Loadout.Unequip(KnightId, _slot);
        }
        else
        {
            Loadout.Equip(KnightId, _slot, id);
        }

        ClosePicker();
        Rebuild();
    }

    private void ClosePicker()
    {
        _pickerOpen = false;
        if (_picker != null) _picker.style.display = DisplayStyle.None;
        RefreshSelection();
    }

    private void Close()
    {
        Hide();
        OnCloseRequested?.Invoke();
    }

    // ---------- input ----------

    private void Update()
    {
        if (!IsVisible) return;
        if (Time.unscaledTime < _inputReadyTime) return;

        var gp = Gamepad.current;
        var kb = Keyboard.current;

        bool leftHeld = (gp != null && (gp.dpad.left.isPressed || gp.leftStick.left.isPressed))
                        || (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed));
        bool rightHeld = (gp != null && (gp.dpad.right.isPressed || gp.leftStick.right.isPressed))
                         || (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed));
        bool upHeld = (gp != null && (gp.dpad.up.isPressed || gp.leftStick.up.isPressed))
                      || (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed));
        bool downHeld = (gp != null && (gp.dpad.down.isPressed || gp.leftStick.down.isPressed))
                        || (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed));

        int horizontal = ApplyRepeat((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0), ref _heldHorizontal, ref _nextHorizontalRepeat);
        int vertical = ApplyRepeat((downHeld ? 1 : 0) - (upHeld ? 1 : 0), ref _heldVertical, ref _nextVerticalRepeat);

        bool confirm = MenuGamepad.SubmitPressed(gp)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = MenuGamepad.CancelPressed(gp)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (_pickerOpen)
        {
            if (vertical != 0 && _candidateRows.Count > 0)
            {
                AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
                _pickIndex = (_pickIndex + vertical + _candidateRows.Count) % _candidateRows.Count;
                RefreshPicker();
            }
            else if (confirm) CommitPick();
            else if (cancel)
            {
                AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
                ClosePicker();
            }
            return;
        }

        if (horizontal != 0)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
            _knight = (_knight + horizontal + 2) % 2;
            RefreshSelection();
        }
        else if (vertical != 0)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
            _slot = (_slot + vertical + RowCount) % RowCount;
            RefreshSelection();
        }
        else if (confirm) OpenPicker();
        else if (cancel)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
            Close();
        }
    }

    private int ApplyRepeat(int held, ref int lastHeld, ref float nextRepeat)
    {
        if (held == 0) { lastHeld = 0; return 0; }
        if (held != lastHeld)
        {
            lastHeld = held;
            nextRepeat = Time.unscaledTime + RepeatDelay;
            return held;
        }
        if (Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + RepeatInterval;
            return held;
        }
        return 0;
    }
}
