/// <summary>
/// One phrasing for an item's detail block, shared by the shop and the equipment
/// screen so the same item never reads two different ways.
///
/// The mechanical line comes first and plain, because it is what a purchase or a
/// loadout decision actually rests on. Flavor follows, dimmed and italic, so it
/// reads as colour rather than as another rule.
/// </summary>
public static class ItemText
{
    private const string FlavorColor = "#9C907A";

    public static string Detail(string effect, string flavor)
    {
        bool hasEffect = !string.IsNullOrEmpty(effect);
        bool hasFlavor = !string.IsNullOrEmpty(flavor);

        if (hasEffect && hasFlavor)
        {
            return effect + $"\n<i><color={FlavorColor}>{flavor}</color></i>";
        }
        if (hasEffect) return effect;
        if (hasFlavor) return $"<i><color={FlavorColor}>{flavor}</color></i>";
        return "";
    }
}
