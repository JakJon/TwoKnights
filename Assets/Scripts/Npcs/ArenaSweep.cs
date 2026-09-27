using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clears the leftovers of the fight before an NPC walks into it, and puts back
/// afterwards everything that was only being held quiet.
///
/// A wave ends with damage numbers still rising, blood still settling, fire and
/// frost still drifting off whatever they were on. None of it belongs in a scene
/// where somebody is standing in the middle of the arena talking — the screenshot
/// that prompted this had a "-10" hanging in the air beside the King.
///
/// The sweep splits in two, and the split is the whole point:
///
///   * One-shots are DESTROYED. TransientEffect is the marker the project already
///     uses for "runtime visual, do not carry this anywhere", and DamageText is the
///     one such object that predates it. They were going to expire anyway.
///   * Standing systems are SILENCED AND RESTORED. A knight's Glacial Ward ring is
///     built once, on the first frame the ward exists, and the component never
///     rebuilds it — so switching its emission off for the conversation switched it
///     off for the rest of the run. Every one that gets stopped here is written
///     down and started again when the scene is over.
///
/// The knights' WEAPONS are silenced the same way, and for a blunter reason: a
/// scene plays at timeScale zero, so an arrow loosed on the last frame of the wave
/// hangs in the air for the whole conversation and a sword caught mid-arc stands
/// there like a signpost. Everything already in flight goes, and the bow, blade and
/// special stop answering the buttons until the last NPC has left.
///
/// And the last wave's leftovers go outright — see <see cref="ClearPlayingField"/>.
/// A wave ends when the things it COUNTS are dead, which leaves carts circling,
/// the track under them, the castle's mirrors and any stray orb or cloud still on
/// the board, all of it in the way of whatever the event puts there.
/// </summary>
public static class ArenaSweep
{
    /// <summary>What was silenced, and whether it was running when we found it.</summary>
    private static readonly List<KeyValuePair<ParticleSystem, bool>> Silenced =
        new List<KeyValuePair<ParticleSystem, bool>>();

    /// <summary>
    /// Each disarmed weapon, and how to put it back exactly as it was found.
    ///
    /// A closure rather than a list of components because the three weapon scripts
    /// share no base type, and because the value being restored is not always true —
    /// the tutorial hands the knights their weapons one at a time, and a quest scene
    /// must not finish that lesson early. The component is kept alongside it anyway,
    /// so a dead entry can be told from a live one: this list is static, so a run
    /// abandoned mid-scene would otherwise carry "already disarmed" into the NEXT
    /// run and leave those knights' weapons live through every scene in it.
    /// </summary>
    private static readonly List<KeyValuePair<Object, System.Action>> Rearm =
        new List<KeyValuePair<Object, System.Action>>();

    public static void Clear()
    {
        ClearPlayingField();

        // Damage numbers. Their own type rather than the marker, because they add
        // TransientEffect reflectively and an older one in flight may not carry it.
        foreach (var text in Object.FindObjectsByType<DamageText>(FindObjectsSortMode.None))
        {
            if (text != null) Object.Destroy(text.gameObject);
        }

        // Blood, dust, sparks, splinters — every one-shot the fight threw.
        foreach (var effect in Object.FindObjectsByType<TransientEffect>(FindObjectsSortMode.None))
        {
            if (effect != null) Object.Destroy(effect.gameObject);
        }

        // Particles attached to something that is still alive — a burning enemy's
        // flame, a knight's ward ring. Stopped and cleared rather than destroyed:
        // the host object is not ours to remove, and its owner may never rebuild it.
        foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (system == null) continue;
            // The NPC's own effects are spawned after this runs, but a scene with
            // two NPCs sweeps between them — so never clear one of ours.
            if (system.GetComponentInParent<NpcActor>() != null) continue;
            if (AlreadySilenced(system)) continue;

            var emission = system.emission;
            Silenced.Add(new KeyValuePair<ParticleSystem, bool>(system, emission.enabled));
            system.Clear(true);
            emission.enabled = false;
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Disarm();
    }

    /// <summary>
    /// Takes everything the last wave left on the field OFF it, before an event — an
    /// Order trial or an NPC scene — puts its own things there (owner's call,
    /// 2026-09-25). Carts still circling were catching trial shots, and nothing about
    /// them belongs to the event.
    ///
    /// Destroyed, never killed: nothing here pays a kill, a quest count or a drop.
    /// Nothing is restored afterwards either — every wave lays its own track and
    /// mirrors as it starts, and BeginWave clears both anyway.
    ///
    /// Only ever called between waves. Events run after the wave has finished, so
    /// anything still standing here is scenery the wave did not wait for.
    /// </summary>
    public static void ClearPlayingField()
    {
        // The ground fires and the standing ice. Frost first, while the bodies it
        // is sitting on still exist, so the frozen tally is handed back cleanly.
        FireField.ClearAll();
        FrigidBoost.ClearFieldFrost();

        // The mine's track and everything riding it — carts, wrecks, pads, flags.
        if (RailNetwork.Instance != null) RailNetwork.Instance.ClearAll();

        // The castle's panes, which would bend a trial's rock into the wrong knight.
        if (MirrorNetwork.Instance != null) MirrorNetwork.Instance.Clear();

        // Anything else still standing: a cart that was never on the network's
        // books, a body mid-death, anything a wave did not count.
        DestroyAll<EnemyBase>();

        // Enemy ammunition still in the air, rocks sent back by a guard included.
        DestroyAll<ProjectileSettings>();
        DestroyAll<EnemyPickaxe>();
        DestroyAll<EnemyBomb>();
        DestroyAll<EnemyFireball>();
        DestroyAll<SonarWave>();

        // An orb still crossing, and venom still hanging where it was left.
        DestroyAll<CollectibleOrb>();
        DestroyAll<PoisonCloud>();
        DestroyAll<PoisonTrailBubble>();
    }

    private static void DestroyAll<T>() where T : Component
    {
        foreach (var thing in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (thing != null) Object.Destroy(thing.gameObject);
        }
    }

    /// <summary>
    /// Takes the fight out of the knights' hands for the length of the conversation:
    /// every shot still in the air is destroyed, any swing still arcing is dropped,
    /// and the bow, blade and special stop reading their buttons.
    ///
    /// Only the FIRST sweep of a run of scenes disarms. Two quests back to back sweep
    /// between them, and a second pass would write down "disabled" as the state to
    /// restore and hand the knights back a bow that never fires again.
    /// </summary>
    private static void Disarm()
    {
        // Arrows, fireballs, shurikens, darts and vials all carry PlayerProjectile.
        // They go whether or not the weapons were already down: a scene that follows
        // another scene can still find one, because the first scene froze it in place
        // rather than letting it fly out of the frame.
        foreach (var shot in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None))
        {
            if (shot == null) continue;
            // A fireball bursts when it is destroyed — told it burned out instead,
            // or a Firebrand lob still in the air goes off over the conversation.
            var fireball = shot.GetComponent<FireballProjectile>();
            if (fireball != null) fireball.MarkBurnedOut();
            Object.Destroy(shot.gameObject);
        }

        // A swing is a coroutine on scaled time, so it does not end by itself while
        // the scene holds timeScale at zero — it has to be told to put the blade away.
        foreach (var swing in Object.FindObjectsByType<SwordSwing>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
        {
            if (swing != null) swing.CancelSwing();
        }

        // Entries whose knight went away with the last run's arena. Dropping them
        // first is what lets the check below tell "this run's weapons are already
        // down" from "the last run left its paperwork lying here".
        for (int i = Rearm.Count - 1; i >= 0; i--)
        {
            if (Rearm[i].Key == null) Rearm.RemoveAt(i);
        }
        if (Rearm.Count > 0) return;

        foreach (var shooter in Object.FindObjectsByType<PlayerShooter>(FindObjectsInactive.Include,
                                                                       FindObjectsSortMode.None))
        {
            if (shooter == null) continue;
            bool was = shooter.InputEnabled;
            shooter.InputEnabled = false;
            Remember(shooter, () => shooter.InputEnabled = was);
        }

        foreach (var swing in Object.FindObjectsByType<SwordSwing>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None))
        {
            if (swing == null) continue;
            bool was = swing.InputEnabled;
            swing.InputEnabled = false;
            Remember(swing, () => swing.InputEnabled = was);
        }

        // The special too. It is a button like the other two, and a screen-clearing
        // blast let off over a conversation is the same problem as an arrow, louder.
        foreach (var special in Object.FindObjectsByType<PlayerSpecial>(FindObjectsInactive.Include,
                                                                       FindObjectsSortMode.None))
        {
            if (special == null) continue;
            bool was = special.InputEnabled;
            special.InputEnabled = false;
            Remember(special, () => special.InputEnabled = was);
        }
    }

    private static void Remember(Object component, System.Action rearm)
    {
        Rearm.Add(new KeyValuePair<Object, System.Action>(component, rearm));
    }

    /// <summary>
    /// Wakes everything <see cref="Clear"/> put to sleep. Call it once the last
    /// NPC has gone, not between scenes — a pair of back-to-back quests would
    /// otherwise flicker the knights' auras back on between them.
    ///
    /// Safe to call when nothing was swept, and safe to call twice.
    /// </summary>
    public static void Restore()
    {
        for (int i = 0; i < Silenced.Count; i++)
        {
            var system = Silenced[i].Key;
            // Destroyed while the scene played — a burning enemy's flame outliving
            // the enemy by a frame is the ordinary case.
            if (system == null) continue;

            var emission = system.emission;
            emission.enabled = Silenced[i].Value;
            if (Silenced[i].Value) system.Play(true);
        }
        Silenced.Clear();

        for (int i = 0; i < Rearm.Count; i++)
        {
            // Destroyed while the scene played. Nothing to hand back.
            if (Rearm[i].Key == null) continue;
            Rearm[i].Value();
        }
        Rearm.Clear();
    }

    private static bool AlreadySilenced(ParticleSystem system)
    {
        for (int i = 0; i < Silenced.Count; i++)
        {
            if (Silenced[i].Key == system) return true;
        }
        return false;
    }
}
