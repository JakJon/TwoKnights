using System;
using UnityEngine;

/// <summary>
/// One entry per NPC: the art, the voice, and how they arrive and leave.
///
/// A ScriptableObject in Resources rather than fields on a scene object, for the
/// reason EquipmentCatalog is one — the quest scenes are spawned in code and have no
/// scene object to hang references off, and both scenes would otherwise need their
/// own copy of the wiring.
///
/// The prefab is the model prefab the Aseprite importer generates beside each
/// .aseprite. It cannot be wired by hand in YAML: with Individual Layers import the
/// importer mints a fresh GUID per layer sprite on every reimport, so the sub-asset
/// ids are not derivable from outside the editor. See Assets/Editor/NpcCatalogBuilder.
/// </summary>
[CreateAssetMenu(fileName = "NpcCatalog", menuName = "Quests/NPC Catalog")]
public class NpcCatalog : ScriptableObject
{
    /// <summary>How an NPC gets onto the board and off it again. No two share one.</summary>
    public enum Arrival
    {
        /// <summary>Walks in, freezes on frame zero when stopped. King and Cartographer.</summary>
        Walker = 0,
        /// <summary>A smoke puff he is already standing behind. Ninja.</summary>
        Smoke = 1,
        /// <summary>Walks down trailing his Order's element; a veil of it hides him again. Wizard.</summary>
        Elemental = 2,
        /// <summary>Glow first at full strength, then the figure inside it. Paladin.</summary>
        Radiant = 3,
    }

    [Serializable]
    public class Entry
    {
        public NpcId id;

        [Tooltip("The model prefab generated beside the .aseprite. Wired by the editor one-shot, not by hand.")]
        public GameObject prefab;

        [Tooltip("Portrait for the quest log's detail pane. The same art, standing still.")]
        public Sprite portrait;

        public Arrival arrival = Arrival.Walker;

        [Tooltip("Pitch for this NPC's voice blip. What actually tells the five of them apart.")]
        [Range(0.4f, 2.2f)] public float voicePitch = 1f;

        [Tooltip("How big this NPC stands, as a multiple of the knights. The art is the same 32x32 at 32 PPU they are, so 1 puts an NPC on the board at exactly knight size - which reads as another combatant rather than as someone who walked in.")]
        [Range(0.5f, 3f)] public float worldScale = 1.6f;

        [Tooltip("Animator state for the walk cycle, from the Aseprite tag. Blank for the NPCs that have no tags and import as one clip.")]
        public string walkState = "";

        [Tooltip("Cartographer only: the map-opening and map-closing tags.")]
        public string openState = "";
        public string closeState = "";
    }

    [SerializeField] private Entry[] entries = new Entry[0];

    private static NpcCatalog _instance;

    public static NpcCatalog Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<NpcCatalog>("NpcCatalog");
            return _instance;
        }
    }

    public Entry Find(NpcId id)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].id == id) return entries[i];
        }
        return null;
    }

    public Entry[] All => entries ?? new Entry[0];

    /// <summary>
    /// The blip for an NPC, resolved off AudioManager's own fields. Kept here rather
    /// than as a clip on the entry so the sound wiring stays in the one place every
    /// other sound in the game is wired.
    /// </summary>
    public static SoundEffect VoiceFor(NpcId id)
    {
        var audio = AudioManager.Instance;
        if (audio == null) return null;
        switch (id)
        {
            case NpcId.King: return audio.npcVoiceKing;
            case NpcId.Cartographer: return audio.npcVoiceCartographer;
            case NpcId.Ninja: return audio.npcVoiceNinja;
            case NpcId.Wizard: return audio.npcVoiceWizard;
            case NpcId.Paladin: return audio.npcVoicePaladin;
            default: return null;
        }
    }
}
