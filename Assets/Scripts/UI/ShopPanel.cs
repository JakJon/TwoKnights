using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The camp shop, split into an Equipment tab and a Specials tab.
///
/// Polls its own input, like the quest log and the equipment screen: the tabs
/// need left/right and the camp's action set is only up/down/confirm/cancel.
/// It must be listed in CampMenuController.PanelPollsOwnInput or both react to
/// the same press.
/// </summary>
public class ShopPanel : MonoBehaviour
{
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    private const string ROW_CLASS = "shop-row";
    private const string ROW_SELECTED = "shop-row--selected";
    private const string ROW_OWNED = "shop-row--owned";
    private const string ROW_UNAFFORDABLE = "shop-row--unaffordable";
    private const string TAB_ACTIVE = "shop-tab--active";

    private enum Tab { Equipment = 0, Specials = 1 }

    /// <summary>Which of the two slot purchases a row is, or neither.</summary>
    private enum SlotKind { None = 0, Equipment = 1, Special = 2 }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private ScrollView _list;
    // A row (count + icon), not a Label — see CrystalText
    private VisualElement _crystalLine;
    private Label _detail;
    private Label _tabEquipment;
    private Label _tabSpecials;
    private Button _closeButton;

    // A row is an equipment item, a special, or one of the two slot purchases —
    // exactly one of the three is set. Slots have no definition asset to read
    // from (they are not objects), so their text is stated here.
    private struct Entry
    {
        public VisualElement Row;
        public EquipmentDefinition Equipment;
        public SpecialDefinition Special;
        public SlotKind Slot;

        public string Name =>
            Slot == SlotKind.Equipment ? "Equipment Slot" :
            Slot == SlotKind.Special ? "Special Slot" :
            Equipment != null ? Equipment.DisplayName : (Special != null ? Special.DisplayName : "");

        public string Description =>
            Slot == SlotKind.Equipment
                ? "The quartermaster can be talked into a second belt loop, given a reason."
                : Slot == SlotKind.Special
                    ? "Whatever it is you do when the bar fills, you can learn to do two of them at once."
                    : Equipment != null ? Equipment.Description : (Special != null ? Special.Description : "");

        public string Effect =>
            Slot == SlotKind.Equipment
                ? "+1 equipment slot on both knights."
                : Slot == SlotKind.Special
                    ? "+1 special slot on both knights. Every slot fires on one full bar."
                    : Equipment != null ? Equipment.Effect : (Special != null ? Special.Effect : "");

        public int Cost =>
            Slot == SlotKind.Equipment ? SlotShop.EquipmentSlotPrice :
            Slot == SlotKind.Special ? SlotShop.SpecialSlotPrice :
            Equipment != null ? Equipment.CrystalCost : (Special != null ? Special.CrystalCost : 0);

        public Sprite Icon
        {
            get
            {
                if (Slot == SlotKind.None)
                    return Equipment != null ? Equipment.Icon : (Special != null ? Special.Icon : null);
                var catalog = EquipmentCatalog.Instance;
                if (catalog == null) return null;
                return Slot == SlotKind.Equipment ? catalog.EquipmentSlotIcon : catalog.SpecialSlotIcon;
            }
        }

        /// <summary>
        /// The one place a row's "you already have this" is decided, so the list,
        /// the detail line and the purchase cannot disagree about it. Three
        /// different questions underneath: a slot was bought, a stock special was
        /// never for sale, an item is in the owned list.
        /// </summary>
        public bool Owned
        {
            get
            {
                if (Slot == SlotKind.Equipment) return SlotShop.EquipmentSlotBought;
                if (Slot == SlotKind.Special) return SlotShop.SpecialSlotBought;
                if (Special != null && Special.OwnedFromTheStart) return true;
                return Loadout.IsOwned(Equipment != null ? Equipment.Id : (Special != null ? Special.Id : null));
            }
        }

        /// <summary>A slot is bought, not owned — you cannot carry it.</summary>
        public string OwnedWord => Slot == SlotKind.None ? "OWNED" : "BOUGHT";

        public string OwnedTail => Slot == SlotKind.None
            ? "\nAlready yours — set it in Equipment."
            : "\nAlready bought.";
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private Tab _tab = Tab.Equipment;
    // Selection is remembered per tab, so flicking across and back does not
    // dump the player at the top of a list they were halfway down
    private readonly int[] _indexPerTab = { 0, 0 };

    private float _inputReadyTime;
    private int _heldVertical, _heldHorizontal;
    private float _nextVerticalRepeat, _nextHorizontalRepeat;

    public Action OnCloseRequested;

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    private int Index
    {
        get { return _indexPerTab[(int)_tab]; }
        set { _indexPerTab[(int)_tab] = value; }
    }

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        BindUI();
        CrystalBank.OnCrystalsChanged += HandleCrystalsChanged;
    }

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.clicked -= Close;
        CrystalBank.OnCrystalsChanged -= HandleCrystalsChanged;
    }

    private void HandleCrystalsChanged(int balance)
    {
        if (IsVisible) Rebuild();
    }

    private void BindUI()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("shop-panel");
        _list = _root.Q<ScrollView>("shop-list");
        _crystalLine = _root.Q<VisualElement>("shop-crystals");
        _detail = _root.Q<Label>("shop-detail");
        if (_detail != null) _detail.enableRichText = true;
        _tabEquipment = _root.Q<Label>("shop-tab-equipment");
        _tabSpecials = _root.Q<Label>("shop-tab-specials");
        _closeButton = _root.Q<Button>("shop-close-button");

        if (_tabEquipment != null) _tabEquipment.RegisterCallback<ClickEvent>(_ => SetTab(Tab.Equipment));
        if (_tabSpecials != null) _tabSpecials.RegisterCallback<ClickEvent>(_ => SetTab(Tab.Specials));

        if (_list != null)
        {
            _list.focusable = false;
            if (_list.contentContainer != null) _list.contentContainer.focusable = false;
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
            Debug.LogWarning("ShopPanel: no 'shop-panel' element in the camp UXML.");
            return;
        }
        Rebuild();
        _panel.style.display = DisplayStyle.Flex;
        // Swallow the button press that opened this
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldVertical = 0;
        _heldHorizontal = 0;
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    // ---------- tabs ----------

    private void SetTab(Tab tab)
    {
        if (_tab == tab) return;
        _tab = tab;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        Rebuild();
    }

    private void RefreshTabs()
    {
        if (_tabEquipment != null)
        {
            if (_tab == Tab.Equipment) _tabEquipment.AddToClassList(TAB_ACTIVE);
            else _tabEquipment.RemoveFromClassList(TAB_ACTIVE);
        }
        if (_tabSpecials != null)
        {
            if (_tab == Tab.Specials) _tabSpecials.AddToClassList(TAB_ACTIVE);
            else _tabSpecials.RemoveFromClassList(TAB_ACTIVE);
        }
    }

    // ---------- layout ----------

    private void Rebuild()
    {
        if (_list == null) return;
        _list.Clear();
        _entries.Clear();
        RefreshTabs();

        var catalog = EquipmentCatalog.Instance;
        if (catalog != null)
        {
            // The slot heads its own tab. Top rather than bottom because it is
            // the dearest thing on either list and the one a player saving up is
            // saving for — a purchase you have to scroll to find is one nobody
            // plans around.
            if (_tab == Tab.Equipment)
            {
                AddRow(new Entry { Slot = SlotKind.Equipment });
                foreach (var def in catalog.ShopStock) AddRow(new Entry { Equipment = def });
            }
            else
            {
                AddRow(new Entry { Slot = SlotKind.Special });
                foreach (var special in catalog.SpecialStock) AddRow(new Entry { Special = special });
            }
        }

        CrystalText.Fill(_crystalLine, CrystalBank.Balance, 20f);   // header states it larger

        if (_entries.Count == 0)
        {
            if (_detail != null)
            {
                _detail.text = _tab == Tab.Equipment
                    ? "The shop has no equipment for you yet."
                    : "The shop has no specials for you yet.";
            }
            return;
        }

        SelectIndex(Mathf.Clamp(Index, 0, _entries.Count - 1));
    }

    private void AddRow(Entry entry)
    {
        bool owned = entry.Owned;
        bool affordable = CrystalBank.CanAfford(entry.Cost);

        var row = new VisualElement();
        row.AddToClassList(ROW_CLASS);
        if (owned) row.AddToClassList(ROW_OWNED);
        else if (!affordable) row.AddToClassList(ROW_UNAFFORDABLE);

        // Art and name travel together on the left so the price stays flush right
        var left = new VisualElement();
        left.AddToClassList("shop-row-left");

        var icon = new VisualElement();
        icon.AddToClassList("row-icon");
        if (entry.Icon != null) icon.style.backgroundImage = new StyleBackground(entry.Icon);
        left.Add(icon);

        var name = new Label(entry.Name);
        name.AddToClassList("shop-row-name");
        left.Add(name);
        row.Add(left);

        // Owned is a word; a price is always a count plus the crystal icon
        VisualElement price;
        if (owned)
        {
            var ownedLabel = new Label(entry.OwnedWord);
            ownedLabel.AddToClassList("shop-row-owned-label");
            price = ownedLabel;
        }
        else
        {
            price = CrystalText.Build(entry.Cost, 14f);
        }
        price.AddToClassList("shop-row-price");
        row.Add(price);

        int index = _entries.Count;
        row.RegisterCallback<PointerMoveEvent>(_ => SelectIndex(index));
        row.RegisterCallback<ClickEvent>(_ => { SelectIndex(index); Buy(); });

        _list.Add(row);
        entry.Row = row;
        _entries.Add(entry);
    }

    private void SelectIndex(int index)
    {
        if (_entries.Count == 0) return;
        Index = Mathf.Clamp(index, 0, _entries.Count - 1);

        for (int i = 0; i < _entries.Count; i++)
        {
            if (i == Index) _entries[i].Row.AddToClassList(ROW_SELECTED);
            else _entries[i].Row.RemoveFromClassList(ROW_SELECTED);
        }

        // Rows are plain VisualElements, so nothing scrolls them into view on
        // its own — without this a controller can select a row it cannot see
        if (_list != null) _list.ScrollTo(_entries[Index].Row);

        if (_detail != null)
        {
            var entry = _entries[Index];
            string tail = entry.Owned
                ? entry.OwnedTail
                : (CrystalBank.CanAfford(entry.Cost) ? "" : "\nNot enough crystals.");
            _detail.text = ItemText.Detail(entry.Effect, entry.Description) + tail;
        }
    }

    private void Buy()
    {
        if (Index < 0 || Index >= _entries.Count) return;
        var entry = _entries[Index];

        if (entry.Owned) return;

        // A slot pays for itself inside SlotShop — it has to bank the purchase
        // and raise the count under one gate, and there is nothing to Own().
        if (entry.Slot != SlotKind.None)
        {
            bool bought = entry.Slot == SlotKind.Equipment
                ? SlotShop.TryBuyEquipmentSlot()
                : SlotShop.TryBuySpecialSlot();
            AudioManager.Instance?.PlaySFX(bought
                ? AudioManager.Instance.uiConfirm
                : AudioManager.Instance.uiCancel);
            if (bought) Rebuild();
            return;
        }

        // TrySpend is the gate: it refuses and changes nothing when short, so
        // there is no window where the item is granted but unpaid
        if (!CrystalBank.TrySpend(entry.Cost))
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
            return;
        }

        Loadout.Own(entry.Equipment != null ? entry.Equipment.Id : entry.Special.Id);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        // Buying never auto-equips — what a knight carries stays the player's call
        Rebuild();
    }

    private void Close()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        Hide();
        OnCloseRequested?.Invoke();
    }

    // ---------- input ----------

    public void NavigateUp()
    {
        if (_entries.Count == 0) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectIndex(Index <= 0 ? _entries.Count - 1 : Index - 1);
    }

    public void NavigateDown()
    {
        if (_entries.Count == 0) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectIndex((Index + 1) % _entries.Count);
    }

    public void Confirm() { Buy(); }

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

        int vertical = ApplyRepeat((downHeld ? 1 : 0) - (upHeld ? 1 : 0), ref _heldVertical, ref _nextVerticalRepeat);
        int horizontal = ApplyRepeat((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0), ref _heldHorizontal, ref _nextHorizontalRepeat);

        // Shoulder buttons are the other habit players bring to tabs
        bool tabLeft = gp != null && gp.leftShoulder.wasPressedThisFrame;
        bool tabRight = gp != null && gp.rightShoulder.wasPressedThisFrame;

        bool confirm = MenuGamepad.SubmitPressed(gp)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = MenuGamepad.CancelPressed(gp)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (vertical > 0) NavigateDown();
        else if (vertical < 0) NavigateUp();
        else if (horizontal < 0 || tabLeft) SetTab(Tab.Equipment);
        else if (horizontal > 0 || tabRight) SetTab(Tab.Specials);
        else if (confirm) Buy();
        else if (cancel) Close();
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
