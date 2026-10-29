using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The tutorial's voice: one line at a time, standing in the arena on the right
/// where the second knight will eventually be. World space rather than a HUD
/// overlay because the empty half of the board is the only place text can sit
/// without covering the lesson, and because it should read as part of the arena
/// the player is looking at rather than as menu chrome laid over it.
///
/// Built in code, not authored into the scene: it is set in the run's pixel font
/// (RunFont), like the damage numbers and the text box, and it only exists during
/// a tutorial.
/// </summary>
public class TutorialText : MonoBehaviour
{
    // Centre of the block, in world units. The camera is orthographic at size
    // 5.625, so the view is x [-10, 10] by y [-5.625, 5.625] and the right knight
    // stands at (2, -0.5) — this sits just outboard of it, clear of the left
    // knight's half of the board entirely.
    private static readonly Vector3 Anchor = new Vector3(5.1f, 0.6f, 0f);
    private static readonly Vector2 BlockSize = new Vector2(9f, 5f);

    /// <summary>
    /// Where a SECOND line stands, under the first. Its block is short so the two
    /// cannot grow into each other, and it is far enough down to read as a
    /// separate remark rather than as a wrapped continuation of the line above.
    /// </summary>
    public static readonly Vector3 HintAnchor = new Vector3(5.1f, -1.6f, 0f);
    private static readonly Vector2 HintBlockSize = new Vector2(9f, 2f);

    // Where the lines stand once the second knight is on the board. The usual
    // place is that knight's half, and any line longer than a few words would be
    // printed across it. So the voice moves to the middle of the board, above
    // both knights' heads. The block is wider there because there is room for
    // it: the two long lines at the end of the tutorial come out at three rows
    // and two, where the usual nine units would break them into five and three.
    private static readonly Vector3 OverBothKnights = new Vector3(0f, 3.6f, 0f);
    private static readonly Vector2 OverBothKnightsBlockSize = new Vector2(12.5f, 5f);

    // The wave banner's gold, so the tutorial sounds like the rest of the game
    private static readonly Color Gold = new Color(0.957f, 0.667f, 0.212f, 1f);

    // Autosizing with a low ceiling, not a fixed size: nearly every line comes out
    // at the maximum and so reads as one consistent voice, and the two or three
    // long ones shrink to fit instead of spilling out of the block.
    //
    // The ceiling is the pixel font drawn pixel for pixel with the art, which is
    // a shade smaller than the serif's 5.5 was; the floor is half that.
    private static readonly float FontSizeMax = RunFont.WorldSize(1f);
    private static readonly float FontSizeMin = RunFont.WorldSize(0.5f);

    // Above every sprite in the arena. Not a new sorting layer — adding one leaves
    // every sprite on it unlit black until each Light2D is told about it.
    private const string SortingLayer = "Default";
    private const int SortingOrder = 500;

    private const float FadeSeconds = 0.35f;

    private TextMeshPro _label;

    // Where this block sits, and how big it is, when nothing has asked it to
    // move. Per instance, so the hint line keeps its own place when it is raised.
    private Vector3 _anchor;
    private Vector2 _block;

    public static TutorialText Create(WaveName banner)
    {
        return Create(banner, "Tutorial Text", Anchor, BlockSize);
    }

    /// <summary>
    /// A second line that can stand under the first — see <see cref="HintAnchor"/>.
    /// Its own object rather than a second row of the same block: the two have to
    /// be able to fade independently, which is the whole point of it.
    /// </summary>
    public static TutorialText CreateHint(WaveName banner)
    {
        return Create(banner, "Tutorial Hint", HintAnchor, HintBlockSize);
    }

    private static TutorialText Create(WaveName banner, string name, Vector3 anchor, Vector2 block)
    {
        var go = new GameObject(name);
        go.transform.position = anchor;

        var text = go.AddComponent<TutorialText>();
        text._anchor = anchor;
        text._block = block;
        text.Build(banner, block);
        return text;
    }

    private void Build(WaveName banner, Vector2 block)
    {
        _label = gameObject.AddComponent<TextMeshPro>();

        // The run's pixel font. The wave banner itself kept its serif, so this
        // no longer borrows the banner's.
        RunFont.Apply(_label);

        _label.rectTransform.sizeDelta = block;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = FontSizeMin;
        _label.fontSizeMax = FontSizeMax;
        _label.fontSize = FontSizeMax;
        _label.color = Gold;
        _label.text = string.Empty;
        _label.alpha = 0f;

        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = SortingLayer;
            meshRenderer.sortingOrder = SortingOrder;
        }
    }

    /// <summary>
    /// Fade a line up and leave it standing. For lines with a lesson under them.
    ///
    /// <paramref name="lift"/> moves the block up off its anchor for this line
    /// only, for the one lesson whose enemy walks straight through where the text
    /// stands. Applied while the block is still invisible, and every other line
    /// asks for none, so the tutorial's voice does not wander around the screen.
    /// </summary>
    public IEnumerator Raise(string line, float lift = 0f)
    {
        yield return RaiseAt(line, _anchor + Vector3.up * lift, _block);
    }

    /// <summary>
    /// Fade a line up over the middle of the board and leave it standing. For
    /// the end of the tutorial, when the right knight is standing where the
    /// lines usually go. A plain <see cref="Raise"/> afterwards puts the block
    /// back in its own place.
    /// </summary>
    public IEnumerator RaiseOverBothKnights(string line)
    {
        yield return RaiseAt(line, OverBothKnights, OverBothKnightsBlockSize);
    }

    // Moved and resized while the block is still invisible
    private IEnumerator RaiseAt(string line, Vector3 position, Vector2 block)
    {
        transform.position = position;
        _label.rectTransform.sizeDelta = block;
        _label.text = line;
        yield return Fade(_label.alpha, 1f);
    }

    /// <summary>Take the current line back down.</summary>
    public IEnumerator Lower()
    {
        yield return Fade(_label.alpha, 0f);
    }

    /// <summary>Fade a line up, hold it, and take it down again. For lines with nothing to do.</summary>
    public IEnumerator Say(string line, float hold)
    {
        yield return Raise(line);
        yield return new WaitForSeconds(hold);
        yield return Lower();
    }

    /// <summary><see cref="Say"/>, over the middle of the board.</summary>
    public IEnumerator SayOverBothKnights(string line, float hold)
    {
        yield return RaiseOverBothKnights(line);
        yield return new WaitForSeconds(hold);
        yield return Lower();
    }

    private IEnumerator Fade(float from, float to)
    {
        // Already there. Worth the check because the hint line is lowered at the
        // end of every lesson whether or not it was ever raised, and a fade from
        // nothing to nothing would still cost each of them a third of a second.
        if (Mathf.Approximately(from, to))
        {
            _label.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.deltaTime;
            _label.alpha = Mathf.Lerp(from, to, elapsed / FadeSeconds);
            yield return null;
        }
        _label.alpha = to;
    }
}
