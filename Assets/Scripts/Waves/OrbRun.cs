using System.Collections;
using UnityEngine;

// The orbs a wave lets across the board, as one authorable block.
//
// An orb is COLLECTED BY SHOOTING IT (CollectibleOrb), not by touching it, which
// is the whole reason it belongs in the tuning rather than beside it: taking one
// costs an arrow and a shield facing, at a moment the wave has already decided is
// busy. A wave that drops an orb in the middle of a tight window is offering a
// genuine trade; one that drops it in dead air is just handing out health.
//
// Orbs travel at 5 u/s and destroy themselves at the far point, so `from` and
// `to` are also how long the shot stays available: the full width of the board is
// 24 units, a little under five seconds of flight.
//
// They never register with wave tracking, so an orb nobody shot cannot hold a
// wave open — it simply leaves.
[System.Serializable]
public class OrbRun
{
    [Tooltip("Health orbs restore 20; mana orbs pay 10 toward the special. False = mana.")]
    public bool healthOrb;

    [Tooltip("How many cross over the course of the wave. 0 = none.")]
    public int count = 1;

    [Tooltip("Seconds into the wave before the first one crosses")]
    public float firstAt = 8f;

    [Tooltip("Seconds between them when there is more than one")]
    public float interval = 12f;

    [Tooltip("Where it enters. Off-frame on purpose, so it flies in rather than appearing.")]
    public Vector2 from = new Vector2(-12f, -1.5f);

    [Tooltip("Where it leaves. Keep the line clear of the track: an orb behind a rail is an orb whose arrows get eaten by the carts.")]
    public Vector2 to = new Vector2(12f, -1.5f);

    // How many of this run's orbs have gone out on the current wave. Reset at the
    // top of every Release: the run lives on a wave asset, and asset state
    // outlives the run that set it.
    private int _released;

    /// <summary>
    /// Release the run. Start it beside the wave rather than inside it — orbs do
    /// not gate anything, so a wave must never wait on one.
    /// </summary>
    public IEnumerator Release(Spawner spawner)
    {
        // Before the first yield, so it has happened by the time StartCoroutine
        // returns and a SendFirstIfWaiting later in the same frame counts right
        _released = 0;

        // One frame before anything is decided, and it is load-bearing. A wave
        // starts this beside itself and stops it when it ends, but StartCoroutine
        // returns NULL for an enumerator that finishes without ever yielding — so
        // a run with no orbs in it used to hand the wave a null handle, and the
        // StopCoroutine at the end of the wave threw. That exception kills the
        // wave coroutine where it stands, which means MarkSpawningComplete never
        // runs and the run hangs on a wave that can never end. Yielding first
        // costs a frame nobody can see and makes a zero-orb run impossible to
        // author wrong.
        yield return null;

        if (spawner == null || count <= 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, firstAt));

        for (int i = 0; i < count; i++)
        {
            // A slot SendFirstIfWaiting already paid for is skipped rather than
            // sent again, so a hurried first orb takes the first slot's place
            // instead of bunching up against it
            if (_released <= i)
            {
                spawner.SpawnOrb(from, to, healthOrb);
                _released++;
            }
            if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.1f, interval));
        }
    }

    /// <summary>
    /// Sends the first orb now if the timer has not got to it yet. Every Mine
    /// wave has an orb (owner, 2026-09-23), but most of them end when the players
    /// kill what is out, so a quick clear could finish the wave before firstAt
    /// and the stop at the end of the wave took the orb with it. Waves call this
    /// as their LAST group goes out, which is the latest point an orb still
    /// crosses a live board.
    ///
    /// Does nothing on a run authored with no orbs, or once one has gone out.
    /// </summary>
    public void SendFirstIfWaiting(Spawner spawner)
    {
        if (spawner == null || count <= 0 || _released > 0) return;

        spawner.SpawnOrb(from, to, healthOrb);
        _released++;
    }
}
