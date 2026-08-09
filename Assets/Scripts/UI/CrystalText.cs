using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The one way crystal amounts are shown: the number, then the crystal icon.
///
/// House rule — a crystal amount is NEVER written as "3 crystals" and never with a
/// stand-in glyph like a text diamond. The icon is the unit, so it always comes from
/// <see cref="EquipmentCatalog.CrystalIcon"/> and always sits right of the count.
/// Anywhere that needs to show a price, a balance or a reward goes through here so
/// the three of them cannot drift apart.
/// </summary>
public static class CrystalText
{
    public const string RowClass = "crystal-amount";
    public const string CountClass = "crystal-amount-count";
    public const string IconClass = "crystal-amount-icon";

    /// <summary>A fresh count+icon row, for lists built in code.</summary>
    public static VisualElement Build(int amount, float iconSize = 16f)
    {
        var row = new VisualElement();
        row.AddToClassList(RowClass);
        Fill(row, amount, iconSize);
        return row;
    }

    /// <summary>
    /// Fills a row that already exists (one declared in UXML, or one being reused),
    /// creating the count label and icon on first use.
    ///
    /// Layout is applied inline rather than left to a stylesheet: this is used from
    /// the camp AND from the in-run wave-survived panel, which are separate UI
    /// documents with separate USS. Inline keeps it correct in both; the classes
    /// are left on for colour and font.
    /// </summary>
    public static void Fill(VisualElement row, int amount, float iconSize = 16f)
    {
        if (row == null) return;

        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        var count = row.Q<Label>(className: CountClass);
        if (count == null)
        {
            count = new Label();
            count.AddToClassList(CountClass);
            row.Add(count);
        }
        // Count is created first and Add() appends, so the number always precedes
        // the icon — including on a row Fill has already run over.
        var icon = row.Q<VisualElement>(className: IconClass);
        if (icon == null)
        {
            icon = new VisualElement();
            icon.AddToClassList(IconClass);
            row.Add(icon);
        }
        icon.style.width = iconSize;
        icon.style.height = iconSize;
        icon.style.flexShrink = 0;
        icon.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
        icon.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        count.style.marginRight = 4;

        // The icon IS the unit. If the catalog art is missing the number would read as
        // a bare integer with nothing to say what it counts, so fall back to the word
        // rather than render something meaningless.
        var sprite = EquipmentCatalog.Instance != null ? EquipmentCatalog.Instance.CrystalIcon : null;
        if (sprite != null)
        {
            icon.style.backgroundImage = new StyleBackground(sprite);
            icon.style.display = DisplayStyle.Flex;
            count.text = amount.ToString();
        }
        else
        {
            icon.style.display = DisplayStyle.None;
            count.text = amount == 1 ? "1 crystal" : $"{amount} crystals";
        }
    }
}
