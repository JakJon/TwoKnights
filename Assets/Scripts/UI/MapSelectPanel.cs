using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// The level select: the maps as a stack of cards, the chosen one held up in the
// middle and the rest tucked in behind it to either side. Navigated with left
// and right; sits between the camp's Start button and the game scene.
//
// The stack is built from absolutely positioned slots rather than a flex row.
// A row can only push cards apart, and the whole point of the shape is that the
// cards OVERLAP — the one in focus covers its neighbours, which cover theirs —
// so each slot is placed by hand at a measured offset from the middle and then
// raised into the right paint order. Paint order is document order in UI
// Toolkit, so the deepest card on each side is added first and the focused card
// last.
//
// Like TestModePanel this polls its own input rather than adding left/right
// InputActionReferences — the camp's action set is up/down/confirm/cancel, and
// CampMenuController goes quiet while this panel is up.
public class MapSelectPanel : MonoBehaviour
{
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    // ---- stack geometry ----
    // Widths and offsets are in pixels, measured against the card in focus.
    // Written as a rule rather than a table so a fourth or fifth map tucks in
    // behind the others without anyone having to extend an array.
    private const float FocusWidth = 360f;
    private const float MinWidth = 220f;
    private const float FocusHeightPct = 100f;
    private const float MinHeightPct = 46f;
    // How far the first card behind sits from the middle, and how much further
    // each one after that peeks out. The first is a little under half a card, so
    // a neighbour still shows its name and its art; the rest are slivers.
    private const float FirstOffset = 173f;
    private const float OffsetStep = 58f;

    private const string CARD_CLASS = "map-card";
    private const string FOCUSED_CLASS = "map-card--focused";
    private const string LOCKED_CLASS = "map-card--locked";

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private VisualElement _stage;
    private Button _closeButton;

    private readonly List<MapDefinition> _maps = new List<MapDefinition>();
    private readonly List<Card> _cards = new List<Card>();
    private int _index;

    private float _inputReadyTime;
    private int _heldHorizontal;
    private float _nextHorizontalRepeat;

    /// <summary>Raised when the player commits to a map.</summary>
    public Action<MapDefinition> OnMapChosen;

    /// <summary>Raised on back/close, so the camp menu can take input again.</summary>
    public Action OnCloseRequested;

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    // One map's card plus the few parts that change when it takes focus or loses
    // it. Held rather than re-queried so moving along the stack is a handful of
    // style writes instead of rebuilding three cards' worth of elements.
    private sealed class Card
    {
        public VisualElement Slot;
        public VisualElement Body;
        public VisualElement Frame;
        public VisualElement Detail;
        public VisualElement Stars;
    }

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        BindUI();
    }

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

        _panel = _root.Q<VisualElement>("map-select-panel");
        _stage = _root.Q<VisualElement>("map-card-stage");
        _closeButton = _root.Q<Button>("map-close-button");

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
            Debug.LogWarning("MapSelectPanel: no 'map-select-panel' element in the camp UXML.");
            return;
        }

        Populate();
        _panel.style.display = DisplayStyle.Flex;
        // Swallow the button that opened this panel
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldHorizontal = 0;
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void Populate()
    {
        _maps.Clear();
        _cards.Clear();
        if (_stage == null) return;
        _stage.Clear();

        var catalog = MapCatalog.Instance;
        if (catalog != null)
        {
            foreach (var map in catalog.Maps)
            {
                if (map != null) _maps.Add(map);
            }
        }

        if (_maps.Count == 0)
        {
            var empty = new Label("No maps in the catalog.");
            empty.AddToClassList("map-empty-line");
            _stage.Add(empty);
            return;
        }

        for (int i = 0; i < _maps.Count; i++)
        {
            _cards.Add(BuildCard(_maps[i], i));
        }

        SetSelectedIndex(IndexOfRemembered());
    }

    // Reopen where the player last played, when that map is still selectable
    private int IndexOfRemembered()
    {
        var remembered = MapSelection.Selected;
        string rememberedId = remembered != null ? remembered.MapId : SaveManager.Data.lastPlayedMapId;

        for (int i = 0; i < _maps.Count; i++)
        {
            if (_maps[i].MapId == rememberedId && MapProgressStore.IsUnlocked(_maps[i])) return i;
        }
        for (int i = 0; i < _maps.Count; i++)
        {
            if (MapProgressStore.IsUnlocked(_maps[i])) return i;
        }
        return 0;
    }

    private Card BuildCard(MapDefinition map, int index)
    {
        bool unlocked = MapProgressStore.IsUnlocked(map);

        // The slot is the thing that moves. It is pinned to the middle of the
        // stage and then walked left or right by its own margin, so a card's
        // width can change without dragging its centre off the line.
        var slot = new VisualElement();
        slot.AddToClassList("map-card-slot");

        // The body is the card's actual rectangle inside the full-height slot,
        // and it exists so the shadows have something card-shaped to bleed out
        // from. Inset against the slot they would stretch the height of the
        // screen, because a card tucked behind is much shorter than the stage.
        var body = new VisualElement();
        body.AddToClassList("map-card-body");
        slot.Add(body);

        // Two soft rings under the card standing in for a blurred drop shadow:
        // UI Toolkit has no box-shadow, and a single hard rectangle behind a
        // card reads as another card rather than as a shadow. They are added
        // before the frame, and document order is paint order, so they land
        // under their own card and over whatever sits behind it in the stack.
        body.Add(NewShadow("map-card-shadow--outer"));
        body.Add(NewShadow("map-card-shadow--inner"));

        var frame = new VisualElement();
        frame.AddToClassList(CARD_CLASS);
        if (!unlocked) frame.AddToClassList(LOCKED_CLASS);
        body.Add(frame);

        var title = new Label(unlocked ? map.DisplayName : "???");
        title.AddToClassList("map-card-title");
        frame.Add(title);

        var art = new VisualElement();
        art.AddToClassList("map-card-art");
        if (map.PreviewImage != null)
        {
            art.style.backgroundImage = new StyleBackground(map.PreviewImage);
        }
        // A locked map keeps its picture — drained, not hidden. The padlock is a
        // CHILD of the art well so it tracks the artwork's bounds for free, and
        // the draining is a background tint rather than opacity: opacity would
        // cascade onto the padlock and leave it as faint as what it covers.
        if (!unlocked) art.Add(BuildLockBadge());
        frame.Add(art);

        var rule = new VisualElement();
        rule.AddToClassList("map-card-rule");
        frame.Add(rule);

        // Everything below the rule belongs to the card in focus. A card tucked
        // behind is both narrower and shorter, and text squeezed into one would
        // be unreadable rather than merely small.
        var detail = new VisualElement();
        detail.AddToClassList("map-card-detail");
        frame.Add(detail);

        if (unlocked)
        {
            if (!string.IsNullOrEmpty(map.Description))
            {
                var description = new Label(map.Description);
                description.AddToClassList("map-card-description");
                detail.Add(description);
            }

            var spacer = new VisualElement();
            spacer.AddToClassList("map-card-spacer");
            detail.Add(spacer);

            detail.Add(BuildStatRow("Deepest wave visited", DepthText(map)));
            detail.Add(BuildStatRow("Quests completed", QuestText(map)));
            detail.Add(BuildStatRow("Wave types found", WaveTypeText(map)));
        }

        // The stars straddle the card's top edge, so they hang off the frame
        // rather than taking a row inside it — and they go on LAST, because
        // paint order is document order and they dip far enough down to clip
        // the top of the map's name.
        var stars = BuildStars(map, unlocked);
        frame.Add(stars);

        // Hover does not select: the cards overlap, so a card sliding under the
        // pointer would grab focus from the one the player is reaching for.
        // Click a card behind to bring it forward, click the front one to go.
        slot.RegisterCallback<ClickEvent>(_ =>
        {
            if (index == _index) Confirm();
            else SetSelectedIndex(index);
        });

        _stage.Add(slot);
        return new Card { Slot = slot, Body = body, Frame = frame, Detail = detail, Stars = stars };
    }

    private static VisualElement NewShadow(string className)
    {
        var shadow = new VisualElement();
        shadow.AddToClassList("map-card-shadow");
        shadow.AddToClassList(className);
        return shadow;
    }

    private static VisualElement BuildStatRow(string caption, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("map-card-stat");

        var label = new Label(caption);
        label.AddToClassList("map-card-stat-label");
        row.Add(label);

        var number = new Label(value);
        number.AddToClassList("map-card-stat-value");
        row.Add(number);

        return row;
    }

    // Waves cleared against the map's authored depth. The total stays a question
    // mark until the player has actually been that far: how deep a map goes is
    // something the map tells you by being played, not a number handed over on
    // the way in.
    private static string DepthText(MapDefinition map)
    {
        int furthest = MapProgressStore.FurthestWave(map.MapId);
        int total = map.FinalWaveNumber;
        return furthest >= total ? furthest + " / " + total : furthest + " / ?";
    }

    // The quest total is always the real number, never hidden. Unlike the depth,
    // knowing how much work a map holds is the point of showing it at all.
    private static string QuestText(MapDefinition map)
    {
        MapProgressStore.QuestTally(map.MapId, out int completed, out int total);
        return completed + " / " + total;
    }

    // Also a real total, for the same reason as the quests: this is the counter
    // the third star is chasing, and a target you cannot see is not a target.
    private static string WaveTypeText(MapDefinition map)
    {
        MapProgressStore.WaveTypeTally(map.MapId, out int found, out int total);
        return found + " / " + total;
    }

    // Three stars, earned one for reaching the bottom of the map, one for
    // finishing its quests and one for meeting every wave type it has — but the
    // crest does not say WHICH. It is a count, and it fills left, then right,
    // then the middle, so the shape grows into the crest rather than sprouting a
    // peak first.
    //
    // A star not yet earned is absent, not dimmed: an empty socket invites
    // reading it as a thing that is broken rather than a thing not yet done. The
    // three places stay put all the same — the slots hold their ground and only
    // their contents are hidden — so a map's stars never shuffle sideways as it
    // earns them.
    private static VisualElement BuildStars(MapDefinition map, bool unlocked)
    {
        var row = new VisualElement();
        row.AddToClassList("map-card-stars");

        int earned = unlocked ? MapProgressStore.StarsFor(map) : 0;

        // Reading order, left to right. The number beside each is how many stars
        // the map has to have earned before that place is filled.
        int[] fillsAt = { 1, 3, 2 };

        for (int i = 0; i < fillsAt.Length; i++)
        {
            var star = new StarIcon();
            star.AddToClassList("map-star");
            // The middle one is larger, the way the peak of a crest is
            if (i == 1) star.AddToClassList("map-star--crown");
            // Hidden rather than dropped, so the other stars do not slide over
            star.style.visibility = earned >= fillsAt[i] ? Visibility.Visible : Visibility.Hidden;
            row.Add(star);
        }
        return row;
    }

    // A five-pointed star drawn with Painter2D. Drawn rather than typed because
    // the camp UI has no icon atlas, and a star GLYPH would depend on the serif
    // font happening to ship one — on a platform where it does not, the reward
    // for finishing a map would render as a hollow box.
    private sealed class StarIcon : VisualElement
    {
        private static readonly Color Fill = new Color(0.94f, 0.80f, 0.36f, 1f);
        private static readonly Color Edge = new Color(0.55f, 0.40f, 0.12f, 1f);

        public StarIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            // The first paint can land before layout has given the star a size,
            // which draws nothing and would never come back on its own.
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width;
            float height = contentRect.height;
            if (width <= 1f || height <= 1f) return;

            var painter = context.painter2D;
            float outer = Mathf.Min(width, height) * 0.5f - 1.5f;
            float inner = outer * 0.42f;
            // Nudged down a touch: a star's visual centre sits below its
            // geometric one, because the two lower points are the shorter pair.
            var centre = new Vector2(width * 0.5f, height * 0.52f);

            painter.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float radius = (i % 2 == 0) ? outer : inner;
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 0.2f;
                var point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            painter.ClosePath();

            painter.fillColor = Fill;
            painter.Fill();
            painter.strokeColor = Edge;
            painter.lineWidth = 2f;
            painter.Stroke();
        }
    }

    // A padlock built from two boxes: the camp UI has no icon atlas, and a glyph
    // would depend on the serif font happening to ship one.
    private static VisualElement BuildLockBadge()
    {
        var scrim = new VisualElement();
        scrim.AddToClassList("map-card-scrim");

        var padlock = new VisualElement();
        padlock.AddToClassList("map-lock");

        var shackle = new VisualElement();
        shackle.AddToClassList("map-lock-shackle");
        padlock.Add(shackle);

        var body = new VisualElement();
        body.AddToClassList("map-lock-body");
        padlock.Add(body);

        scrim.Add(padlock);
        return scrim;
    }

    private void SetSelectedIndex(int index)
    {
        if (_cards.Count == 0) return;
        _index = Mathf.Clamp(index, 0, _cards.Count - 1);
        Restack();
    }

    // Place every card at its depth from the one in focus, then raise them into
    // paint order. Both halves matter: the geometry is what fans the stack out,
    // and the order is what makes the focused card cover its neighbours instead
    // of being covered by whichever map happens to come later in the catalog.
    private void Restack()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            int depth = Mathf.Abs(i - _index);
            int side = i < _index ? -1 : 1;
            Card card = _cards[i];

            float width = depth == 0
                ? FocusWidth
                : Mathf.Max(MinWidth, FocusWidth - 50f - 26f * (depth - 1));
            float heightPct = depth == 0
                ? FocusHeightPct
                : Mathf.Max(MinHeightPct, 68f - 8f * (depth - 1));
            float offset = depth == 0 ? 0f : side * (FirstOffset + OffsetStep * (depth - 1));

            card.Slot.style.width = width;
            card.Slot.style.marginLeft = offset - width * 0.5f;
            card.Body.style.height = new Length(heightPct, LengthUnit.Percent);
            // Cards behind are dulled, and further back is duller still, so the
            // one in focus is unmistakable from across the room.
            card.Slot.style.opacity = depth == 0
                ? 1f
                : Mathf.Max(0.3f, 0.62f - 0.12f * (depth - 1));

            bool focused = depth == 0;
            if (focused) card.Frame.AddToClassList(FOCUSED_CLASS);
            else card.Frame.RemoveFromClassList(FOCUSED_CLASS);

            card.Detail.style.display = focused ? DisplayStyle.Flex : DisplayStyle.None;
            card.Stars.style.display = focused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Deepest first on each side, then inward, then the focused card last.
        // Cards on opposite sides never overlap each other, so pairing them off
        // at each depth is enough to get every overlap that does happen right.
        for (int depth = _cards.Count - 1; depth >= 1; depth--)
        {
            int left = _index - depth;
            int right = _index + depth;
            if (left >= 0) _cards[left].Slot.BringToFront();
            if (right < _cards.Count) _cards[right].Slot.BringToFront();
        }
        _cards[_index].Slot.BringToFront();
    }

    // A stack has ends: walking off one is nothing happening, not a wrap round
    // to the far side of the shelf.
    private void Navigate(int direction)
    {
        if (_cards.Count <= 1) return;
        int next = Mathf.Clamp(_index + direction, 0, _cards.Count - 1);
        if (next == _index) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SetSelectedIndex(next);
    }

    private void Confirm()
    {
        if (_index < 0 || _index >= _maps.Count) return;
        var map = _maps[_index];

        // Locked maps stay in the stack so the campaign's shape is legible, but
        // they are not a destination. Answer the press rather than eating it —
        // silence reads as a dropped input, not as a refusal.
        if (!MapProgressStore.IsUnlocked(map))
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
            return;
        }

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        MapSelection.Select(map);
        OnMapChosen?.Invoke(map);
    }

    private void Close()
    {
        Hide();
        OnCloseRequested?.Invoke();
    }

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

        int rawHorizontal = (rightHeld ? 1 : 0) - (leftHeld ? 1 : 0);
        int horizontal = ApplyRepeat(rawHorizontal, ref _heldHorizontal, ref _nextHorizontalRepeat);

        bool confirm = MenuGamepad.SubmitPressed(gp)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = MenuGamepad.CancelPressed(gp)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (horizontal != 0) Navigate(horizontal);
        else if (confirm) Confirm();
        else if (cancel) Close();
    }

    // Edge-trigger with hold-to-repeat, matching TestModePanel's feel
    private int ApplyRepeat(int held, ref int lastHeld, ref float nextRepeat)
    {
        if (held == 0)
        {
            lastHeld = 0;
            return 0;
        }

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
