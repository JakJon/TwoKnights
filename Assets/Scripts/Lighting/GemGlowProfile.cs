using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Which colours in a stage's art are gems or ore, and how they glow. A stage on
// a MapDefinition points at one of these; GemGlow turns it into the halo that
// actually draws.
//
// There are no positions in here. GemGlow looks through the stage's backdrop
// and foreground for pixels of these colours every time the stage goes up, so a
// gem painted in, moved or rubbed out in Aseprite gains, moves or loses its
// glow with no other change. Only a brand-new gem COLOUR has to be added here.
//
// Like the canopy, this is meant to be barely there: a hint that the gems give
// off a little light, not a lamp. Each gem is dark most of the time and glows
// a little once every few seconds, on its own clock. If in doubt, turn
// Brightness down.
[CreateAssetMenu(fileName = "GemGlow", menuName = "Maps/Gem Glow Profile")]
public class GemGlowProfile : ScriptableObject
{
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int LevelsId = Shader.PropertyToID("_Levels");
    private static readonly int LastsId = Shader.PropertyToID("_Lasts");

    [Tooltip("Custom/GemGlow. Held as a reference rather than found by name so the shader is " +
             "guaranteed to travel into a build.")]
    [SerializeField] private Shader shader;

    [Header("What glows")]
    [Tooltip("Every pixel of one of these colours glows, in that colour. List the lit faces of the " +
             "gems — a dark shadow-side colour adds very little light, and a colour shared with the " +
             "plain rock would make the rock glow too.")]
    [SerializeField] private List<Color> gemColours = new List<Color>();

    [Tooltip("How far a pixel may be from a listed colour and still count as it, in 0-255 steps per " +
             "channel. Keep it low: the Mine's lighter floor purple is only about 30 away from the " +
             "darkest amethyst.")]
    [Range(0, 64)][SerializeField] private int colourTolerance = 12;

    [Header("The halo")]
    [Tooltip("How far the light reaches from each gem pixel, in art pixels.")]
    [Range(1f, 24f)][SerializeField] private float radius = 8f;

    [Tooltip("How quickly the light from neighbouring gem pixels adds up to a full glow. Low: a lone " +
             "one-pixel fleck barely glows and only the bigger stones get a full halo. High: every " +
             "fleck glows nearly as much as the biggest stone.")]
    [Range(0.05f, 3f)][SerializeField] private float buildUp = 0.4f;

    [Tooltip("How much light a gem adds at the top of a glow. ADDITIVE in linear light, so the useful " +
             "range is tiny: 0.03 is a glint you half catch, 0.1 is a gem lighting the room. Zero: no glow.")]
    [Range(0f, 0.5f)][SerializeField] private float brightness = 0.03f;

    [Tooltip("Steps the halo is dithered to between nothing and full. Low keeps it chunky like the " +
             "art; high smooths it into a gradient that no longer looks hand-placed.")]
    [Range(1f, 32f)][SerializeField] private float ditherLevels = 5f;

    [Header("When it glows")]
    [Tooltip("Seconds between one gem's glows, on average. A gem is dark the rest of the time (owner, " +
             "2026-10-24: a steady glow was too noticeable).")]
    [FormerlySerializedAs("pulsePeriod")]
    [Range(1f, 30f)][SerializeField] private float glowEvery = 5f;

    [Tooltip("How far each gem's own gap strays from Glow Every, as a fraction of it. Every gem keeps " +
             "its own clock, so they drift in and out of step instead of taking turns in a fixed order.")]
    [Range(0f, 0.5f)][SerializeField] private float everyVariation = 0.25f;

    [Tooltip("Seconds one glow takes to come up and fade away again. Keep it well under Glow Every, or " +
             "the gems are glowing more often than not.")]
    [Range(0.2f, 10f)][SerializeField] private float glowLasts = 1.5f;

    public Shader Shader => shader;
    public IReadOnlyList<Color> GemColours => gemColours;
    public int ColourTolerance => colourTolerance;
    public float Radius => radius;
    public float BuildUp => buildUp;
    public float GlowEvery => glowEvery;
    public float EveryVariation => everyVariation;

    /// <summary>
    /// Bumped whenever the profile is edited in the inspector, so a glow already
    /// on screen can repaint itself while the values are being tuned in play mode.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Writes the values every gem shares. The halos themselves — gem colours,
    /// radius, build-up — and each gem's own timing are GemGlow's to work out.
    /// </summary>
    public void PushTo(Material material)
    {
        if (material == null) return;

        material.SetFloat(BrightnessId, brightness);
        material.SetFloat(LevelsId, Mathf.Max(ditherLevels, 1f));
        material.SetFloat(LastsId, glowLasts);
    }

    void OnValidate()
    {
        Revision++;
    }
}
