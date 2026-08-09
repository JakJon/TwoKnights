using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 8;
    // The name shown on this file's bar in the file select. Set once, at the
    // moment the file is created; there is no rename.
    public string profileName = "";
    // Wall-clock seconds spent in the game on this file, unscaled — a paused
    // menu still counts, an alt-tabbed window does not. Double, not float:
    // float's step at 100+ hours is coarse enough to swallow whole frames.
    public double playTimeSeconds = 0;
    public int gold = 0;
    public int furthestWave = 0;
    // Honor and Knight Rank were removed in v7. Old saves still carry the keys;
    // JsonUtility ignores fields it has no home for, so they simply fall away.
    // Level select reopens here; empty on old saves, which resolves to the
    // first unlocked map (see MapSelection.Resolve)
    public string lastPlayedMapId = "";
    public List<QuestCompletion> completedQuests = new List<QuestCompletion>();
    public List<StatEntry> stats = new List<StatEntry>();
    public List<MapRecord> maps = new List<MapRecord>();

    // --- Equipment meta layer (v6) ---
    // Crystals are the quest currency and the camp shop's only price tag
    public int crystals = 0;
    // Lifetime crystals earned on this file. Separate from the balance because
    // spending must not reduce it — this is a record of what the file has found,
    // and it is what the file select shows.
    public int totalCrystalsEarned = 0;
    // Equipment slots PER KNIGHT, not in total. The Crimson Twins quest is the
    // one thing that raises this, to 2.
    public int equipmentSlots = 1;
    // Every equipment item and special the player owns, quest-granted or bought.
    // Owning is separate from equipping: buying never auto-equips.
    public List<string> ownedEquipment = new List<string>();
    // A new file starts with the two stock specials already in their slots: the
    // left knight fires Rapid Fire, the right one mends. They resolved that way
    // anyway through PlayerSpecial's prefab fallback, but writing them down means
    // the camp shows a filled special slot on the first visit rather than an
    // empty one. A loaded save replaces this list wholesale, so existing files
    // keep whatever they chose.
    public List<KnightLoadout> loadouts = new List<KnightLoadout>
    {
        new KnightLoadout { knightId = "left", specialId = "rapid_fire" },
        new KnightLoadout { knightId = "right", specialId = "field_mending" },
    };
    // Quest ids the player has actually opened in the log. A quest that is
    // unlocked but absent here is what lights the notification dot.
    public List<string> seenQuests = new List<string>();

    // --- Camp notices ---
    // The shop stays hidden until the first crystal is earned — a price list is
    // noise to someone with nothing to spend. Latched, not derived from the
    // balance, so spending back down to zero does not hide the shop again.
    public bool everHadCrystals = false;
    // Whether the shop has been opened since it appeared. Drives its dot.
    public bool shopSeen = false;
    // Equipment ids the player has actually looked at. Anything owned but absent
    // here lights the Equipment dot, same rule as seenQuests.
    public List<string> seenEquipment = new List<string>();
}

[Serializable]
public class KnightLoadout
{
    public string knightId;                                  // "left" | "right"
    public List<string> equipped = new List<string>();       // equipment ids, count <= equipmentSlots
    public string specialId = "";                            // empty = the knight prefab's default
}

[Serializable]
public class MapRecord
{
    public string mapId;
    public bool unlocked;
    public bool gateCleared;
    public bool trueCleared;
}

[Serializable]
public class QuestCompletion
{
    public string questId;
    public string completedDate;
}

[Serializable]
public class StatEntry
{
    public string key;
    public int value;
}
