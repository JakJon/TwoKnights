using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The text box the NPCs speak through: world space, below the knights, typing one
/// letter at a time in that NPC's voice.
///
/// World space rather than a UIDocument overlay, and built in code rather than
/// authored into a scene, for the reasons TutorialText already documents — it should
/// read as part of the arena the player is looking at, and it only exists while
/// somebody is talking. It borrows the same font and the same gold, so the game has
/// one voice rather than two.
/// </summary>
public class DialogueBox : MonoBehaviour
{
    // Everything here is measured from QuestBackdrop, which owns the plate's size
    // and where it sits. The hardcoded anchors this replaced put the "press to
    // continue" line at y = -5.7, below the bottom edge of a 5.625 camera.
    private const float HintHeight = 0.5f;

    // The wave banner's gold, the same one the tutorial speaks in.
    private static readonly Color Gold = new Color(0.957f, 0.667f, 0.212f, 1f);

    private const float FontSizeMax = 3.2f;
    private const float FontSizeMin = 1.8f;

    // Above every sprite in the arena. NOT a new sorting layer — adding one leaves
    // every sprite on it unlit black until each Light2D is told about it.
    private const string SortingLayer = "Default";
    private const int SortingOrder = 600;

    private const float FadeSeconds = 0.25f;

    /// <summary>A blip every Nth visible character, not every one — a per-letter
    /// chatter is a drone rather than a voice.</summary>
    private const int LettersPerBlip = 3;

    private TextMeshPro _label;
    private TextMeshPro _hint;
    private QuestBackdrop _backdrop;

    public static DialogueBox Create(WaveName banner)
    {
        var go = new GameObject("Dialogue Box");
        go.transform.position = new Vector3(0f, QuestBackdrop.CenterY, 0f);
        var box = go.AddComponent<DialogueBox>();
        box.Build(banner);
        return box;
    }

    private void Build(WaveName banner)
    {
        _backdrop = QuestBackdrop.Create(transform);

        // Top-left inside the plate, a fixed padding in from both edges. Prose
        // reads from the top-left corner; centring it made a one-line page and a
        // four-line page start in different places, which is exactly the wandering
        // the plate exists to stop.
        var body = new Vector2(QuestBackdrop.TextWidth, QuestBackdrop.TextHeight - HintHeight);
        var bodyGo = new GameObject("Dialogue Body");
        bodyGo.transform.SetParent(transform, false);
        bodyGo.transform.localPosition = QuestBackdrop.TopLeft(body);
        _label = BuildLabel(banner, bodyGo, body, FontSizeMax, FontSizeMin,
                            TextAlignmentOptions.TopLeft);

        // "press to continue", in the opposite corner. Its own object so it can
        // blink while the text above it stands still, and out of the way of the
        // left-aligned prose rather than under it.
        var hint = new Vector2(QuestBackdrop.TextWidth * 0.5f, HintHeight);
        var hintGo = new GameObject("Dialogue Hint");
        hintGo.transform.SetParent(transform, false);
        hintGo.transform.localPosition = QuestBackdrop.BottomRight(hint);
        _hint = BuildLabel(banner, hintGo, hint, 1.5f, 1.2f, TextAlignmentOptions.BottomRight);
        _hint.text = "";
        _hint.alpha = 0f;
    }

    /// <summary>
    /// "The King: ", bold, in front of every page. Bold rather than a second
    /// colour because the box has one voice and a second colour in it would read
    /// as emphasis inside the sentence.
    /// </summary>
    private static string SpeakerPrefix(NpcId speaker)
    {
        string name = QuestLog.NpcName(speaker);
        return string.IsNullOrEmpty(name) ? "" : "<b>" + name + ":</b> ";
    }

    private static TextMeshPro BuildLabel(WaveName banner, GameObject host, Vector2 block,
                                          float max, float min, TextAlignmentOptions align)
    {
        var label = host.AddComponent<TextMeshPro>();

        // The banner's own font asset. Failing to find it is not worth stopping for —
        // TMP falls back to its default and the scene still reads.
        var bannerText = banner != null ? banner.GetComponent<TMP_Text>() : null;
        if (bannerText != null && bannerText.font != null) label.font = bannerText.font;

        label.rectTransform.sizeDelta = block;
        label.alignment = align;
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
        label.fontSize = max;
        label.color = Gold;
        label.text = string.Empty;
        label.alpha = 0f;

        var mesh = host.GetComponent<MeshRenderer>();
        if (mesh != null)
        {
            mesh.sortingLayerName = SortingLayer;
            mesh.sortingOrder = SortingOrder;
        }
        return label;
    }

    /// <summary>
    /// Types one page out and waits for the player to acknowledge it.
    ///
    /// <paramref name="onDirective"/> runs before the first character, for the
    /// staging beats that sit between pages — the King's crown, his walk away.
    /// Returned as a coroutine so a directive can take as long as it needs.
    /// </summary>
    public IEnumerator Speak(DialogueScript.Page page, NpcId speaker, float pitch,
                             Func<DialogueScript.Directive, IEnumerator> onDirective)
    {
        if (page.Before != DialogueScript.Directive.None && onDirective != null)
        {
            yield return onDirective(page.Before);
        }
        if (page.IsEmpty) yield break;

        if (_label.alpha < 1f) yield return FadeBody(1f);

        // The press that acknowledged the PREVIOUS page is still down on the frame
        // this one starts, and wasPressedThisFrame is true for all of it. Without
        // this grace every page after the first read as already-rushed and appeared
        // whole — which is exactly what "only the first box types" was.
        float armedAt = Time.unscaledTime + 0.12f;
        while (Time.unscaledTime < armedAt) yield return null;

        var voice = NpcCatalog.VoiceFor(speaker);

        // Who is talking, standing in front of the line rather than typed out with
        // it. The name is not something the NPC says, so it is there from the first
        // frame of the page and the typewriter starts after it.
        string prefix = SpeakerPrefix(speaker);
        var shown = new StringBuilder(prefix, prefix.Length + page.Letters.Count);
        int sinceBlip = 0;

        for (int i = 0; i < page.Letters.Count; i++)
        {
            var letter = page.Letters[i];
            shown.Append(letter.Character);
            _label.text = shown.ToString();

            // Whitespace is not a sound. Without this the voice keeps ticking
            // through the gaps and the rhythm stops matching the words.
            if (!char.IsWhiteSpace(letter.Character) && ++sinceBlip >= LettersPerBlip)
            {
                sinceBlip = 0;
                if (voice != null) AudioManager.Instance?.PlayVoice(voice, pitch);
            }

            // A press mid-page fills the rest of it in immediately rather than
            // advancing — the player asked to read it, not to skip it. The second
            // press then moves on, which is the convention everywhere else.
            //
            // LEAVING THE LOOP is what makes that true, and it is an audio fix as
            // much as a text one. This used to set a "rushed" flag and keep
            // iterating, which ran every remaining letter of the page inside a
            // single frame — and fired every remaining letter's BLIP with it. Fifty
            // one-shots of the same clip starting on the same frame do not read as
            // a voice talking quickly, they sum into one clipped bang, which is
            // what "all the voice sfx play at once really loud" is. Nothing is lost
            // by stopping here: the line below prints the finished page outright,
            // so those extra iterations only ever added the noise.
            if (QuestSceneInput.AdvancePressed()) break;

            float wait = DialogueScript.SecondsFor(letter);
            float until = Time.unscaledTime + wait;
            bool rushed = false;
            while (Time.unscaledTime < until)
            {
                if (QuestSceneInput.AdvancePressed()) { rushed = true; break; }
                yield return null;
            }
            if (rushed) break;
        }

        _label.text = prefix + page.PlainText;
        yield return WaitForAcknowledgement();
    }

    private IEnumerator WaitForAcknowledgement()
    {
        _hint.text = "press to continue";
        yield return Fade(_hint, _hint.alpha, 0.55f);

        // The grace exists because the press that finished the typewriter is often
        // still down. Same reasoning as WaveSurvivedPanel's input guard.
        float readyAt = Time.unscaledTime + 0.18f;
        while (Time.unscaledTime < readyAt) yield return null;
        while (!QuestSceneInput.AdvancePressed()) yield return null;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        _hint.alpha = 0f;
        _hint.text = "";
    }

    public IEnumerator Hide()
    {
        _hint.alpha = 0f;
        yield return FadeBody(0f);
        _label.text = "";
    }

    /// <summary>
    /// The words and the plate they stand on, together. Separate from the hint's
    /// own fade because the hint blinks on and off inside a page while the plate
    /// stays put for the whole conversation.
    /// </summary>
    private IEnumerator FadeBody(float to)
    {
        float from = _label.alpha;
        if (Mathf.Approximately(from, to))
        {
            _label.alpha = to;
            _backdrop.SetAlpha(to);
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / FadeSeconds);
            _label.alpha = alpha;
            _backdrop.SetAlpha(alpha);
            yield return null;
        }
        _label.alpha = to;
        _backdrop.SetAlpha(to);
    }

    private IEnumerator Fade(TextMeshPro label, float from, float to)
    {
        if (Mathf.Approximately(from, to))
        {
            label.alpha = to;
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            // Unscaled: the whole scene runs with the game frozen behind it.
            elapsed += Time.unscaledDeltaTime;
            label.alpha = Mathf.Lerp(from, to, elapsed / FadeSeconds);
            yield return null;
        }
        label.alpha = to;
    }
}
