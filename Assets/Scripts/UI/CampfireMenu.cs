using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// The camp menu as a ring around the fire.
//
// Each menu item is a piece of art on the ground with its title above it, set on
// a circle about the knights. The left stick picks one the way it turns a
// knight's shield: push it in a direction and a glow on the ring points exactly
// that way. Whichever item's slice of the ring the glow is in is the one picked,
// so one is ALWAYS picked — there is no direction that selects nothing. Thin
// spokes mark where each slice begins and ends. Let go of the stick and the glow
// stays where it was and breathes.
//
// This class is the ring and nothing else. What an item DOES when it is chosen
// is CampMenuController's, handed in with AddItem; which panels are open, the
// input cooldown and the save are all still its business. The place the ring is
// drawn in — backdrop, knights, fire, light — is CampfireScene.
public class CampfireMenu : MonoBehaviour
{
    /// <summary>
    /// How far from the fire each item's middle sits, in world units. The owner's
    /// number (2026-10-13): he asked for the first layout's 3.05 to go a unit and
    /// a half further out. It is about as far as the frame allows — see
    /// CampfireScene.Center.
    /// </summary>
    public const float ItemRadius = 4.55f;

    // The same dead zone the shield uses (ShieldOrbit), so the stick answers here
    // exactly as it does in a run
    private const float StickDeadzone = 0.5f;

    // An item's art stands this far below its point on the ring, which puts the
    // art and its title together about on the ring
    private const float ArtDrop = 0.62f;
    private const float TitleGap = 0.1f;

    // How far the picked item's art rises, in art pixels
    private const float PickedLiftPixels = 2f;

    // How much nearer another item has to be before the pick changes. Less than
    // a degree and a half: enough to stop a stick resting on a spoke from
    // chattering, not enough to see against the spoke.
    private const float Stickiness = 1.5f;

    // Glow, band and slice light have been turned down twice at the owner's
    // word (2026-10-13): first by half ("too strong"), then to about a third of
    // that ("still needs to be significantly more transparent"). They started at
    // 0.34, 0.3 and 0.05. He wants this faint — a hint on the ground, not a
    // spotlight. The spokes were left alone both times.
    private const float GlowStrength = 0.06f;
    private const float GlowSpread = 0.4f;
    // Far more steps than the shader's default of 24. The glow is so faint now
    // that at 24 it would be one or two visible bands; this keeps it falling
    // away in about as many steps as the first, much stronger one did.
    private const float GlowLevels = 128f;
    private const float BreathDepth = 0.16f;
    private const float BreathRate = 2.4f;

    private static readonly Color BandColor = new Color(0.04f, 0.04f, 0.07f, 0.06f);
    // The spokes. Half as strong as first built (0.36), at the owner's word
    // (2026-10-13); the circle that used to join them round the knights is gone.
    private static readonly Color LineColor = new Color(1f, 0.86f, 0.6f, 0.18f);
    private static readonly Color SliceColor = new Color(1f, 0.72f, 0.4f, 0.01f);
    private static readonly Color GlowColor = new Color(1f, 0.74f, 0.4f, 1f);

    private static readonly Color RestTint = new Color(0.8f, 0.8f, 0.8f, 1f);
    private static readonly Color PickedTint = Color.white;
    private static readonly Color LockedTint = new Color(0.3f, 0.3f, 0.36f, 1f);

    // The cartographer's map: open, held open, shut, held shut
    private static readonly float[] KeeperHolds = { 0.12f, 1.8f, 0.12f, 1.3f };

    private const string ItemClass = "wheel-item";
    private const string TitleClass = "wheel-title";
    private const string PickedClass = "wheel-item--selected";
    private const string LockedClass = "wheel-item--locked";
    private const string BadgeClass = "wheel-badge";
    private const string BadgeGlowClass = "wheel-badge-glow";

    private const int MaxDividers = 8;

    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadius");
    private static readonly int OuterRadiusId = Shader.PropertyToID("_OuterRadius");
    private static readonly int FadeRadiusId = Shader.PropertyToID("_FadeRadius");
    private static readonly int InnerFadeId = Shader.PropertyToID("_InnerFade");
    private static readonly int BandColorId = Shader.PropertyToID("_BandColor");
    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int SelColorId = Shader.PropertyToID("_SelColor");
    private static readonly int SelStartId = Shader.PropertyToID("_SelStart");
    private static readonly int SelEndId = Shader.PropertyToID("_SelEnd");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    private static readonly int GlowSpreadId = Shader.PropertyToID("_GlowSpread");
    private static readonly int AngleId = Shader.PropertyToID("_Angle");
    private static readonly int LevelsId = Shader.PropertyToID("_Levels");
    private static readonly int DividersId = Shader.PropertyToID("_Dividers");
    private static readonly int DividerCountId = Shader.PropertyToID("_DividerCount");

    /// <summary>One thing on the ring.</summary>
    public sealed class Item
    {
        public string Id { get; internal set; }
        /// <summary>Degrees, anticlockwise from straight right: 90 is the top of the ring.</summary>
        public float Angle { get; internal set; }
        public bool Locked { get; internal set; }
        /// <summary>The notification dot beside the title, and its halo. Hidden until lit.</summary>
        public VisualElement Badge { get; internal set; }
        public VisualElement BadgeGlow { get; internal set; }

        internal Action Activate;
        internal Vector2 Ground;
        internal Vector2 TitleAnchor;
        internal VisualElement Root;
        internal Label Title;
        internal Vector2 PlacedAt = new Vector2(float.NaN, float.NaN);
        internal float SliceStart;
        internal float SliceEnd;
        internal readonly List<SpriteRenderer> Art = new List<SpriteRenderer>();
        internal readonly List<Vector3> ArtHome = new List<Vector3>();
    }

    private CampfireScene _scene;
    private Camera _camera;
    private VisualElement _layer;
    private Material _wheel;

    private readonly List<Item> _items = new List<Item>();
    private readonly float[] _dividers = new float[MaxDividers];
    private Item _selected;
    private bool _active = true;

    // Where the stick last pointed, and where the glow is drawn — the second
    // chases the first fast enough to look locked to it
    private float _targetDegrees = 90f;
    private float _glowDegrees = 90f;
    private float _breath;
    private Vector2 _lastMouse;

    private SpriteRenderer _keeper;
    private Sprite[] _keeperFrames;
    private Item _keeperItem;
    private int _keeperFrame;
    private float _keeperNext;

    /// <summary>The picked item changed. Never fires for the first pick.</summary>
    public event Action<Item> SelectionChanged;

    /// <summary>The mouse was clicked on the ring. The pick has already moved under it.</summary>
    public event Action ClickConfirmed;

    public Item Selected => _selected;

    /// <summary>
    /// Builds the camp and an empty ring. <paramref name="layer"/> is the element
    /// of the camp's UI document the titles are put in, and <paramref name="host"/>
    /// is the object that owns that document: the camp is built into ITS scene,
    /// so it is torn down with the menu it belongs to. Null when the camp cannot
    /// be drawn, and the caller keeps the list menu.
    /// </summary>
    public static CampfireMenu Create(CampfireKit kit, VisualElement layer, GameObject host)
    {
        Camera camera = Camera.main;
        if (kit == null || layer == null || host == null || camera == null) return null;

        var holder = new GameObject("Campfire Menu");
        // A new object lands in the ACTIVE scene, which is not promised to be the
        // camp's while it is still loading
        if (holder.scene != host.scene) SceneManager.MoveGameObjectToScene(holder, host.scene);
        var menu = holder.AddComponent<CampfireMenu>();
        menu._camera = camera;
        menu._layer = layer;
        menu._scene = CampfireScene.Create(kit, holder.transform, camera);
        if (menu._scene == null)
        {
            Destroy(holder);
            return null;
        }

        menu._wheel = menu._scene.WheelMaterial;
        menu.DressWheel();
        menu.ResetMouse();
        return menu;
    }

    private void OnDestroy()
    {
        // The titles live in the camp's UI document, not under this object
        foreach (Item item in _items)
        {
            if (item.Root != null) item.Root.RemoveFromHierarchy();
        }
        _items.Clear();
    }

    // ------------------------------------------------------------------
    // Filling the ring
    // ------------------------------------------------------------------

    /// <summary>
    /// Puts an item on the ring at <paramref name="angleDegrees"/> (0 = right,
    /// 90 = top). <paramref name="art"/> may be null for an item that is only a
    /// title. <paramref name="artShift"/> slides the art sideways from the item's
    /// spot, for an item made of more than one piece.
    /// </summary>
    public Item AddItem(string id, float angleDegrees, string title, Sprite art, Action activate,
                        Vector2 artShift = default(Vector2))
    {
        var item = new Item { Id = id, Angle = Mathf.Repeat(angleDegrees, 360f), Activate = activate };

        float radians = item.Angle * Mathf.Deg2Rad;
        Vector2 spot = CampfireScene.Center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ItemRadius;

        if (art != null)
        {
            item.Ground = CampfireScene.Snap(spot + new Vector2(0f, -ArtDrop));
            AddArt(item, "Item " + id, art, artShift);
        }
        else
        {
            item.Ground = spot;
            item.TitleAnchor = spot + new Vector2(0f, -0.12f);
        }

        BuildTitle(item, title);
        _items.Add(item);
        LayOutSlices();

        if (_selected == null) Pick(NearestTo(_targetDegrees), false);
        else Tint(item);
        return item;
    }

    /// <summary>
    /// Stands someone beside an item's art and loops <paramref name="frames"/> on
    /// them: the cartographer at the shop, rolling his map open and shut. Hidden
    /// while the item is locked.
    /// </summary>
    public void AddKeeper(Item item, Sprite[] frames, Vector2 shift)
    {
        if (item == null || frames == null || frames.Length == 0 || frames[0] == null) return;

        _keeper = AddArt(item, "Keeper " + item.Id, frames[0], shift);
        _keeperFrames = frames;
        _keeperItem = item;
        _keeperFrame = 0;
        _keeperNext = Time.unscaledTime + KeeperHolds[0];
        _keeper.enabled = !item.Locked;
        Tint(item);
    }

    private SpriteRenderer AddArt(Item item, string objectName, Sprite sprite, Vector2 shift)
    {
        SpriteRenderer renderer = _scene.Place(objectName, sprite, item.Ground + shift);
        item.Art.Add(renderer);
        item.ArtHome.Add(renderer.transform.position);

        // The title sits over the middle of everything the item is made of
        Bounds all = item.Art[0].bounds;
        for (int i = 1; i < item.Art.Count; i++) all.Encapsulate(item.Art[i].bounds);
        item.TitleAnchor = new Vector2(all.center.x, all.max.y + TitleGap);
        return renderer;
    }

    private void BuildTitle(Item item, string title)
    {
        item.Root = new VisualElement { pickingMode = PickingMode.Ignore };
        item.Root.AddToClassList(ItemClass);

        item.Title = new Label(title) { pickingMode = PickingMode.Ignore };
        item.Title.AddToClassList(TitleClass);
        item.Root.Add(item.Title);

        // Halo first so the dot draws over it — the same pair the list menu's
        // buttons carry, pulsed by the same clock in CampMenuController
        item.BadgeGlow = new VisualElement { pickingMode = PickingMode.Ignore };
        item.BadgeGlow.AddToClassList(BadgeGlowClass);
        item.BadgeGlow.style.display = DisplayStyle.None;
        item.Root.Add(item.BadgeGlow);

        item.Badge = new VisualElement { pickingMode = PickingMode.Ignore };
        item.Badge.AddToClassList(BadgeClass);
        item.Badge.style.display = DisplayStyle.None;
        item.Root.Add(item.Badge);

        _layer.Add(item.Root);
    }

    public void SetTitle(Item item, string title)
    {
        if (item != null && item.Title != null) item.Title.text = title;
    }

    /// <summary>
    /// A locked item keeps its place and its slice — the glow can still point at
    /// it — but its art goes dark, its keeper stays away, and choosing it does
    /// nothing.
    /// </summary>
    public void SetLocked(Item item, bool locked)
    {
        if (item == null) return;
        item.Locked = locked;
        if (item.Root != null) item.Root.EnableInClassList(LockedClass, locked);
        if (item == _keeperItem && _keeper != null) _keeper.enabled = !locked;
        Tint(item);
    }

    /// <summary>
    /// Whether the ring is the thing on screen. Off while a panel is open over
    /// the camp: the titles are hidden and the stick is left to the panel.
    /// </summary>
    public void SetActive(bool active)
    {
        _active = active;
        if (_layer != null) _layer.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        if (active) ResetMouse();
    }

    /// <summary>Chooses the picked item. A locked one only answers with the back sound.</summary>
    public void ActivateSelected()
    {
        if (_selected == null) return;

        if (_selected.Locked)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
            return;
        }

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        _selected.Activate?.Invoke();
    }

    // ------------------------------------------------------------------
    // Every frame
    // ------------------------------------------------------------------

    private void Update()
    {
        float now = Time.unscaledTime;
        float delta = Time.unscaledDeltaTime;

        bool pushed = false;
        if (_active)
        {
            Vector2 stick = ReadDirection();
            if (stick != Vector2.zero)
            {
                _targetDegrees = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
                pushed = true;
            }

            ReadMouse();

            Item nearest = NearestTo(_targetDegrees);
            if (nearest != null && nearest != _selected) Pick(nearest, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev: walk the camp through every place it could be pitched
            var keys = Keyboard.current;
            if (keys != null && keys.f7Key.wasPressedThisFrame)
            {
                Debug.Log("[CampfireMenu] Camp shown at: " + _scene.ShowNextSite() + " (not saved)");
            }
#endif
        }

        // Near enough to instant that the glow reads as locked to the stick, with
        // the last few degrees eased so a flick does not strobe
        _glowDegrees = Mathf.LerpAngle(_glowDegrees, _targetDegrees, 1f - Mathf.Exp(-delta * 40f));

        // Steady while the stick is held over; breathing once it is let go
        _breath = Mathf.MoveTowards(_breath, pushed ? 0f : 1f, delta * 3f);
        float wave = 1f - BreathDepth * (0.5f + 0.5f * Mathf.Sin(now * BreathRate));

        if (_wheel != null)
        {
            _wheel.SetFloat(AngleId, _glowDegrees * Mathf.Deg2Rad);
            _wheel.SetFloat(GlowStrengthId, GlowStrength * Mathf.Lerp(1f, wave, _breath));
        }

        TurnKeeper(now);
    }

    private void LateUpdate()
    {
        if (!_active || _layer == null || _layer.panel == null) return;

        // The titles are UI and the art is in the world, so each title is walked
        // to its spot over the art. Only written when it has actually moved —
        // which is on the first frame and when the window changes size.
        foreach (Item item in _items)
        {
            Vector2 panel = RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel, item.TitleAnchor, _camera);
            Vector2 local = _layer.WorldToLocal(panel);
            if (Mathf.Abs(local.x - item.PlacedAt.x) < 0.05f && Mathf.Abs(local.y - item.PlacedAt.y) < 0.05f) continue;

            item.PlacedAt = local;
            item.Root.style.left = local.x;
            item.Root.style.top = local.y;
        }
    }

    // Left stick first, then the d-pad, then the keys. Any pad, not only the
    // last one touched: a second controller left on the desk must not be the
    // only one the camp listens to.
    private static Vector2 ReadDirection()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.magnitude > StickDeadzone) return stick;

            Vector2 dpad = pad.dpad.ReadValue();
            if (dpad.sqrMagnitude > 0.25f) return dpad;
        }

        var keys = Keyboard.current;
        if (keys != null)
        {
            float x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f)
                    - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1f : 0f)
                    - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1f : 0f);
            if (x != 0f || y != 0f) return new Vector2(x, y);
        }

        return Vector2.zero;
    }

    // The mouse points the glow the way the stick does: at wherever it is,
    // measured from the fire. Only when it has actually moved, so a mouse lying
    // still does not fight the stick.
    private void ReadMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screen = mouse.position.ReadValue();
        bool moved = (screen - _lastMouse).sqrMagnitude > 4f;
        bool clicked = mouse.leftButton.wasPressedThisFrame;
        if (!moved && !clicked) return;
        _lastMouse = screen;

        Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
        Vector2 fromFire = (Vector2)world - CampfireScene.Center;
        float distance = fromFire.magnitude;

        // Over the knights themselves there is no direction to speak of
        if (distance < 0.9f) return;

        _targetDegrees = Mathf.Atan2(fromFire.y, fromFire.x) * Mathf.Rad2Deg;

        if (clicked && distance <= CampfireScene.OuterRadius)
        {
            Item nearest = NearestTo(_targetDegrees);
            if (nearest != null && nearest != _selected) Pick(nearest, false);
            ClickConfirmed?.Invoke();
        }
    }

    private void ResetMouse()
    {
        var mouse = Mouse.current;
        if (mouse != null) _lastMouse = mouse.position.ReadValue();
    }

    // ------------------------------------------------------------------
    // Picking
    // ------------------------------------------------------------------

    private Item NearestTo(float degrees)
    {
        Item best = null;
        float bestOff = float.MaxValue;
        foreach (Item item in _items)
        {
            float off = Mathf.Abs(Mathf.DeltaAngle(item.Angle, degrees));
            if (item == _selected) off -= Stickiness;
            if (off < bestOff)
            {
                bestOff = off;
                best = item;
            }
        }
        return best;
    }

    private void Pick(Item item, bool announce)
    {
        if (item == null) return;

        Item before = _selected;
        _selected = item;

        if (before != null) Tint(before);
        Tint(item);
        PushSlice();

        if (!announce || before == null) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectionChanged?.Invoke(item);
    }

    // The picked item's art comes up to full brightness and rises a couple of
    // pixels; the rest sit back a little. Whole pixels, so nothing lands between
    // two rows of the art.
    private void Tint(Item item)
    {
        bool picked = item == _selected;
        Color tint = item.Locked ? LockedTint : (picked ? PickedTint : RestTint);
        float lift = picked && !item.Locked ? PickedLiftPixels / CampfireScene.PixelsPerUnit : 0f;

        for (int i = 0; i < item.Art.Count; i++)
        {
            item.Art[i].color = tint;
            item.Art[i].transform.position = item.ArtHome[i] + new Vector3(0f, lift, 0f);
        }

        if (item.Root != null) item.Root.EnableInClassList(PickedClass, picked);
    }

    // Each item owns the stretch of the ring that is nearer to it than to either
    // neighbour, so the boundaries are the halfway points between neighbours —
    // which is exactly where the spokes are drawn.
    private void LayOutSlices()
    {
        var ordered = new List<Item>(_items);
        ordered.Sort((a, b) => a.Angle.CompareTo(b.Angle));

        int count = ordered.Count;
        int dividers = 0;
        for (int i = 0; i < count; i++)
        {
            Item here = ordered[i];
            Item next = ordered[(i + 1) % count];

            float gap = count > 1 ? Mathf.Repeat(next.Angle - here.Angle, 360f) : 360f;
            if (count > 1 && gap <= 0f) gap = 360f;
            float boundary = here.Angle + gap * 0.5f;

            here.SliceEnd = boundary;
            next.SliceStart = boundary;
            if (count > 1 && dividers < MaxDividers) _dividers[dividers++] = boundary * Mathf.Deg2Rad;
        }

        if (_wheel != null)
        {
            _wheel.SetFloatArray(DividersId, _dividers);
            _wheel.SetFloat(DividerCountId, dividers);
        }
        PushSlice();
    }

    private void PushSlice()
    {
        if (_wheel == null || _selected == null) return;
        _wheel.SetFloat(SelStartId, _selected.SliceStart * Mathf.Deg2Rad);
        _wheel.SetFloat(SelEndId, _selected.SliceEnd * Mathf.Deg2Rad);
    }

    private void DressWheel()
    {
        if (_wheel == null) return;
        _wheel.SetVector(CenterId, CampfireScene.Center);
        _wheel.SetFloat(InnerRadiusId, CampfireScene.InnerRadius);
        _wheel.SetFloat(OuterRadiusId, CampfireScene.OuterRadius);
        _wheel.SetFloat(FadeRadiusId, CampfireScene.FadeRadius);
        _wheel.SetFloat(InnerFadeId, CampfireScene.InnerFade);
        _wheel.SetColor(BandColorId, CampfireScene.ToRenderSpace(BandColor));
        _wheel.SetColor(LineColorId, CampfireScene.ToRenderSpace(LineColor));
        _wheel.SetColor(SelColorId, CampfireScene.ToRenderSpace(SliceColor));
        _wheel.SetColor(GlowColorId, CampfireScene.ToRenderSpace(GlowColor));
        _wheel.SetFloat(GlowSpreadId, GlowSpread);
        _wheel.SetFloat(LevelsId, GlowLevels);
        _wheel.SetFloat(GlowStrengthId, GlowStrength);
        _wheel.SetFloat(AngleId, _glowDegrees * Mathf.Deg2Rad);
        _wheel.SetFloatArray(DividersId, _dividers);
        _wheel.SetFloat(DividerCountId, 0f);
    }

    // ------------------------------------------------------------------
    // The cartographer
    // ------------------------------------------------------------------

    private void TurnKeeper(float now)
    {
        if (_keeper == null || _keeperFrames == null || !_keeper.enabled || now < _keeperNext) return;

        _keeperFrame = (_keeperFrame + 1) % _keeperFrames.Length;
        if (_keeperFrames[_keeperFrame] != null) _keeper.sprite = _keeperFrames[_keeperFrame];
        _keeperNext = now + KeeperHolds[_keeperFrame % KeeperHolds.Length];
    }
}
