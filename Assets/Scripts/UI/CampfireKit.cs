using UnityEngine;

/// <summary>
/// The art the campfire camp is built from: one picture per menu item, the fire,
/// the two knights, the cartographer's map frames, and the two shaders that draw
/// the ring and the firelight.
///
/// One asset, Assets/Resources/CampfireKit.asset, loaded the way CartSortingKit
/// is. The camp is put together in code so the scene needs no wiring, and a
/// sprite inside an .aseprite cannot be pointed at by hand — its id is made on
/// import. Tools > Two Knights > Build Campfire Kit fills this in.
///
/// The backdrop is NOT here. It is the map's, and comes from whichever stage the
/// last run reached — see CampSite.
/// </summary>
[CreateAssetMenu(fileName = "CampfireKit", menuName = "Maps/Campfire Kit")]
public class CampfireKit : ScriptableObject
{
    [Header("Menu items")]
    [Tooltip("Venture Forth — the knights' swords and shield.")]
    [SerializeField] private Sprite ventureForth;
    [Tooltip("Journal — two bedrolls, a journal and a pencil.")]
    [SerializeField] private Sprite journal;
    [Tooltip("Equipment — the trunk.")]
    [SerializeField] private Sprite equipment;
    [Tooltip("Shop — the crates and the sack the cartographer stands beside.")]
    [SerializeField] private Sprite shopGoods;
    [Tooltip("Settings — the toolbox, the wrench and the gear.")]
    [SerializeField] private Sprite settings;

    [Tooltip("The cartographer rolling his map open and shut, in order. Played on a loop beside the shop's crates.")]
    [SerializeField] private Sprite[] cartographerFrames;

    [Header("The fire and the knights")]
    [Tooltip("The campfire's frames, in order.")]
    [SerializeField] private Sprite[] fireFrames;
    [Tooltip("Stands left of the fire, facing it.")]
    [SerializeField] private Sprite leftKnight;
    [Tooltip("Stands right of the fire, facing it.")]
    [SerializeField] private Sprite rightKnight;

    [Header("Shaders")]
    [Tooltip("Custom/CampWheel. Held as a reference so it is sure to travel into a build.")]
    [SerializeField] private Shader wheelShader;
    [Tooltip("Custom/CampfireLight. Held as a reference for the same reason.")]
    [SerializeField] private Shader firelightShader;

    private static CampfireKit _instance;

    public static CampfireKit Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<CampfireKit>("CampfireKit");
            return _instance;
        }
    }

    public Sprite VentureForth => ventureForth;
    public Sprite Journal => journal;
    public Sprite Equipment => equipment;
    public Sprite ShopGoods => shopGoods;
    public Sprite Settings => settings;
    public Sprite[] CartographerFrames => cartographerFrames;
    public Sprite[] FireFrames => fireFrames;
    public Sprite LeftKnight => leftKnight;
    public Sprite RightKnight => rightKnight;
    public Shader WheelShader => wheelShader;
    public Shader FirelightShader => firelightShader;

    /// <summary>
    /// Whether there is enough here to draw the camp. The camp falls back to the
    /// old list of buttons when there is not, rather than showing half a scene.
    /// The cartographer is left out on purpose: the shop reads fine as crates.
    /// </summary>
    public bool IsComplete =>
        ventureForth != null && journal != null && equipment != null && shopGoods != null && settings != null
        && fireFrames != null && fireFrames.Length > 0 && fireFrames[0] != null
        && leftKnight != null && rightKnight != null
        && wheelShader != null && firelightShader != null;
}
