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
        copy.style.left = 0f;
        copy.style.bottom = 0f;
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
            _rim[i].style.left = Mathf.Cos(angle) * pixels;
            _rim[i].style.bottom = Mathf.Sin(angle) * pixels;
        }
    }
}
