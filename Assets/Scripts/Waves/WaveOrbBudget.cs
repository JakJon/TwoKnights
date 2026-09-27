using UnityEngine;

// The orb allowance every wave works inside.
//
// A wave hands out at most three orbs, health and mana counted TOGETHER — three
// is the whole budget, not three of each. Orbs are the one thing on the board
// that pays the player back, so the ceiling is what keeps a long wave from
// quietly turning into a refill: the tiers that run five and six bursts are the
// hard ones, and they are exactly the ones that were handing out the most.
//
// Bosses are deliberately outside this. The Rat King's orbs fire on phase
// transitions and the Overseer's on health thresholds — they are part of how
// those fights are shaped, not a wave's pacing, and a long fight needs its
// recovery.
public static class WaveOrbBudget
{
    public const int MaxOrbsPerWave = 3;

    /// <summary>
    /// For waves that offer one orb per slot (a burst gap, a round, a rat pair):
    /// whether THIS slot is one of the slots that carries an orb.
    ///
    /// Under the cap every slot gets one. Over it the orbs are dealt out evenly
    /// across the wave rather than filling the first three, and the slots that go
    /// without are the early ones — the end of a wave is its hardest stretch and
    /// the last place that should be running dry.
    /// </summary>
    public static bool SlotCarriesOrb(int slot, int totalSlots)
    {
        if (slot < 0 || slot >= totalSlots) return false;
        if (totalSlots <= MaxOrbsPerWave) return true;
        // Integer division walking the budget across the slots: the slot that ticks
        // the running total over to the next whole orb is the one that carries it.
        return (slot + 1) * MaxOrbsPerWave / totalSlots != slot * MaxOrbsPerWave / totalSlots;
    }

    /// <summary>
    /// For waves that release orbs in clumps rather than one per slot, where there
    /// is no clean slot to spread across. Spend from it and it stops at three.
    /// Make one per RUN of the wave, never a field on the ScriptableObject — a
    /// wave asset is shared and its state outlives the run.
    /// </summary>
    public class Budget
    {
        private int _left = MaxOrbsPerWave;

        public bool TrySpend()
        {
            if (_left <= 0) return false;
            _left--;
            return true;
        }
    }
}
