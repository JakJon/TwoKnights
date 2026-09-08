/// <summary>
/// The two knights are told apart on the field by their plume: the left one's is
/// red, the right one's blue. Wherever their names appear in text, the words carry
/// the same two colours, so "Right Knight" in a menu and the knight standing on the
/// right of the screen are obviously the same knight.
/// </summary>
/// <remarks>
/// These are not the plume colours themselves. The sprite accents (#9C3828 and
/// #2E6FD8) are chosen to sit against blue-grey armour; on the near-black menu
/// ground they read as muddy, so each is lifted in value at its own hue. The two
/// results are matched in brightness to each other as well, so neither name shouts
/// louder than the other when they sit side by side.
///
/// Every one of these strings goes into a UI Toolkit Label, which parses rich text
/// by default. Anything that is NOT headed for a Label — a log line, a save key, a
/// stats bucket — wants the plain name instead, or the tags travel with it.
/// </remarks>
public static class KnightNames
{
    public const string LeftHex = "#D1533F";
    public const string RightHex = "#4985E6";

    public const string LeftPlain = "Left Knight";
    public const string RightPlain = "Right Knight";

    public const string Left = "<color=" + LeftHex + ">" + LeftPlain + "</color>";
    public const string Right = "<color=" + RightHex + ">" + RightPlain + "</color>";

    /// <summary>Wraps any wording in a knight's colour — lower case, possessive,
    /// abbreviated, whatever the sentence around it needs.</summary>
    public static string Tint(string text, bool left)
    {
        return $"<color={(left ? LeftHex : RightHex)}>{text}</color>";
    }

    public static string For(KnightTarget target)
    {
        return target == KnightTarget.LeftKnight ? Left : Right;
    }
}
