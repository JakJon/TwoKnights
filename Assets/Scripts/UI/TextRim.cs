using UnityEngine;
using UnityEngine.UIElements;

// A thick black outline for a HUD label (owner, 2026-09-26: the streak numbers).
//
// UI Toolkit's own text outline (-unity-text-outline-width) cannot reach past the
// padding baked into the font, which is about a tenth of the font size — a 10px
// "×1" gets 1px of outline however much the stylesheet asks for. So this draws the
// outline the old way instead: black copies of the label, set in a ring round it,
// behind it.
//
// The label is moved into a box of its own and the copies are laid over it there.
// Every copy carries the label's classes, so the stylesheet sizes and places them
// exactly as it does the label; only their colour is pinned to black. That is why
// class and text changes have to come through here rather than onto the label.
//
// The label's text-shadow glow moves onto one more copy UNDER the ring, so the
// glow shines out past the outline instead of washing colour over it.
public class TextRim
{
    private const int Copies = 12;

    private static readonly StyleTextShadow NoShadow =
        new StyleTextShadow(new TextShadow { offset = Vector2.zero, blurRadius = 0f, color = Color.clear });

    private readonly Label _label;
    private readonly Label _glow;
    private readonly Label[] _rim = new Label[Copies];
    private float _thickness = -1f;

    public TextRim(Label label, float thickness)
    {
        _label = label;

        // Laid out the way the label was: a row sitting on its foot, so the label
        // inside keeps the baseline and margins it had in its old parent
        var box = new VisualElement { name = label.name + "-rim", pickingMode = PickingMode.Ignore };
        box.style.flexDirection = FlexDirection.Row;
        box.style.alignItems = Align.FlexEnd;
        VisualElement parent = label.parent;
        parent.Insert(parent.IndexOf(label), box);

        _glow = AddCopy(box);
        for (int i = 0; i < Copies; i++)
        {
            _rim[i] = AddCopy(box);
            _rim[i].style.textShadow = NoShadow;
        }

        // Last, so it draws over its own ring
        box.Add(label);
        label.style.textShadow = NoShadow;

        SetThickness(thickness);
    }

    private Label AddCopy(VisualElement box)
    {
        var copy = new Label(_label.text) { pickingMode = PickingMode.Ignore };
        foreach (string className in _label.GetClasses()) copy.AddToClassList(className);
        copy.style.position = Position.Absolute;
        Pin(copy, 0f, 0f);
        copy.style.color = Color.black;
        box.Add(copy);
        return copy;
    }

    public void SetText(string text)
    {
        if (_label.text == text) return;
        _label.text = text;
        _glow.text = text;
        for (int i = 0; i < _rim.Length; i++) _rim[i].text = text;
    }

    public void EnableInClassList(string className, bool enable)
    {
        _label.EnableInClassList(className, enable);
        _glow.EnableInClassList(className, enable);
        for (int i = 0; i < _rim.Length; i++) _rim[i].EnableInClassList(className, enable);
    }

    public void AddToClassList(string className) => EnableInClassList(className, true);
    public void RemoveFromClassList(string className) => EnableInClassList(className, false);

    /// <summary>How far out the ring sits, in HUD pixels — the outline's thickness.</summary>
    public void SetThickness(float pixels)
    {
        if (Mathf.Approximately(pixels, _thickness)) return;
        _thickness = pixels;

        for (int i = 0; i < _rim.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / _rim.Length;
            Pin(_rim[i], Mathf.Cos(angle) * pixels, Mathf.Sin(angle) * pixels);
        }
    }

    // Every side of the copy is pinned, so it is exactly the label's own box,
    // moved (x, y). Pinned only on the left and the foot, a copy was as big as
    // its text, and that is not always the label's size: the numbers beside the
    // bars sit in 15px rows, which squeeze the label to 15px with its text
    // centred in it, so the full-height copies drew their ring half a line
    // higher than the number (owner's screenshot, 2026-10-14). The streak line
    // has no fixed height, so there the two sizes happened to agree.
    private static void Pin(Label copy, float x, float y)
    {
        copy.style.left = x;
        copy.style.right = -x;
        copy.style.bottom = y;
        copy.style.top = -y;
    }
}
