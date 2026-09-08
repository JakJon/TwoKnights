using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 10;
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
    // Equipment slots PER KNIGHT, not in total. Two things raise it: the Crimson
    // Twins quest, and buying one in the camp shop. Both add ONE, so a player who
    // does both carries three.
    public int equipmentSlots = 1;
    // Special slots PER KNIGHT. The Gold Cart quest and the shop each add one,
    // and a knight with two fires BOTH on one full bar — see PlayerSpecial.
    public int specialSlots = 1;
    // Whether the shop's slot purchases have been made. The counts above already
    // hold the effect; these only exist so the shop can mark the row bought and
    // refuse to sell the same slot twice — a quest-granted slot must not read as
    // a purchase, so this cannot be derived from the counts. See SlotShop.
    public bool boughtEquipmentSlot = false;
    public bool boughtSpecialSlot = false;
    // Every equipment item and special the player owns, quest-granted or bought.
    // Owning is separate from equipping: buying never auto-equips.
    public List<string> ownedEquipment = new List<string>();
    // A new file starts with the two stock specials already in their slots: the
    // left knight fires Rapid Fire, the right one heals. They resolved that way
    // anyway through PlayerSpecial's prefab fallback, but writing them down means
    // the camp shows a filled special slot on the first visit rather than an
    // empty one. A loaded save replaces this list wholesale, so existing files
    // keep whatever they chose.
    public List<KnightLoadout> loadouts = new List<KnightLoadout>
    {
        new KnightLoadout { knightId = "left", specials = new List<string> { "rapid_fire" } },
        new KnightLoadout { knightId = "right", specials = new List<string> { "field_healing" } },
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

    // --- Tutorial (v10) ---
    // False on a brand-new file, which is what sends it into the tutorial instead
    // of the camp (see TutorialRun.TryBegin). Written true only when the tutorial
    // actually finishes, so quitting part-way through means it plays again.
    public bool tutorialCompleted = false;
}

[Serializable]
public class KnightLoadout
{
    public string knightId;                                  // "left" | "right"
    public List<string> equipped = new List<string>();       // equipment ids, count <= equipmentSlots
    public List<string> specials = new List<string>();       // special ids, count <= specialSlots; empty entry = the prefab's default
    // Pre-v9, a knight had exactly one special and it lived here. Kept only so a
    // loaded save still carries the value long enough to fold into specials[0]
    // (Loadout.Normalize); it is written back empty from then on.
    public string specialId = "";
}

[Serializable]
public class MapRecord
{
    public string mapId;
    public bool unlocked;
    public bool gateCleared;
    public bool trueCleared;

    // Most waves ever CLEARED on this map in one run. Separate from the save's
    // single furthestWave, which is the whole file's best across every map — the
    // level select needs to say how deep this map in particular has been run.
    public int furthestWave;
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
