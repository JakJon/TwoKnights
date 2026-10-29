using UnityEngine;

/// <summary>
/// Everything the cart sorting challenge puts on the board that is not track: the
/// carts in their four colours, the red and blue levers, the sound of a cart
/// reaching its flag, and the rail layout each phase is played on.
///
/// One asset, Assets/Resources/CartSortingKit.asset, loaded the way the poison and
/// fire resource managers are. The challenge is started from code between two
/// waves, so it has no scene object or wave asset to hang these references on, and
/// a prefab built from an .aseprite cannot be pointed at by hand.
///
/// The rail pieces and the flags are NOT here. They are the track's, and travel
/// with the layout's RailPieceSet like every other piece.
/// </summary>
[CreateAssetMenu(fileName = "CartSortingKit", menuName = "Maps/Cart Sorting Kit")]
public class CartSortingKit : ScriptableObject
{
    [Tooltip("The track for each phase, in phase order: element 0 is phase 1.")]
    [SerializeField] private RailLayout[] layouts;

    [SerializeField] private GameObject redCart;
    [SerializeField] private GameObject blueCart;
    [SerializeField] private GameObject greenCart;
    [SerializeField] private GameObject yellowCart;

    [Tooltip("Rail_Switch_Red — the lever that turns the red corner pieces.")]
    [SerializeField] private GameObject redLever;

    [Tooltip("Rail_Switch_Blue — the lever that turns the blue corner pieces.")]
    [SerializeField] private GameObject blueLever;

    [Tooltip("Played each time a cart reaches its own flag.")]
    [SerializeField] private SoundEffect deliveredSound;

    private static CartSortingKit _instance;

    public static CartSortingKit Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<CartSortingKit>("CartSortingKit");
            return _instance;
        }
    }

    public SoundEffect DeliveredSound => deliveredSound;

    /// <summary>The track for a phase, by its 1-based number. Null if that phase has no track yet.</summary>
    public RailLayout LayoutFor(int phaseNumber)
    {
        int index = phaseNumber - 1;
        return layouts != null && index >= 0 && index < layouts.Length ? layouts[index] : null;
    }

    /// <summary>The cart in this colour, or null for a colour that has none.</summary>
    public GameObject CartFor(CartColor color)
    {
        switch (color)
        {
            case CartColor.Red: return redCart;
            case CartColor.Blue: return blueCart;
            case CartColor.Green: return greenCart;
            case CartColor.Yellow: return yellowCart;
        }
        return null;
    }

    public GameObject LeverFor(RailSwitchColor color)
    {
        return color == RailSwitchColor.Blue ? blueLever : redLever;
    }
}
