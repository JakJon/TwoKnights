using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The boxes that follow an NPC's line: what you were given, and what opened.
///
/// On the same plate and in the same gold as the dialogue, in the same place, so a
/// quest reads as one continuous moment rather than as a scene followed by some
/// menus.
///
/// REWARDS COME ONE AT A TIME. A quest that opens a map and hands over an item
/// gives two things, and "Obtained The Mine, The Gnawed Crown" is a receipt — one
/// line the player skims and forgets. Each gets its own box, its own chime and its
/// own press, which is the Pokemon rule and the reason anybody remembers what they
/// were given.
///
/// EVERY REWARD STANDS IN A FRAME OF ITS OWN (owner's sketch, 2026-10-13). A
/// line of gold round it, and what kind of thing it is written across the top
/// of that line: New Map, Equipment, Crystals. A new quest's card lists what the
/// quest pays as a row of these along its foot, and the card that later hands
/// one over shows that same frame by itself, so the thing promised and the thing
/// given are the same picture. Before this they were lines of text under the
/// word "Reward", and a map, an item and its effect read as one paragraph.
/// </summary>
public class QuestNotices : MonoBehaviour
{
    private static readonly Color Gold = new Color(0.957f, 0.667f, 0.212f, 1f);

    private const string SortingLayer = "Default";
    /// <summary>The line round a reward: over the plate (596) and under the words.</summary>
    private const int FrameOrder = 597;
    private const int SortingOrder = 600;
    private const int IconOrder = 601;
    private const float FadeSeconds = 0.25f;

    /// <summary>
    /// Floor for the reward hold, for the case where no jingle is wired yet. The
    /// box is meant to be un-skippable for the length of the melody; with no melody
    /// it still has to be un-skippable for long enough to read.
    /// </summary>
    private const float MinimumRewardHold = 1.4f;

    /// <summary>Size of an objective entry, relative to the card's body text.</summary>
    private const int ListItemSizePercent = 78;

    // static readonly rather than const: an int spliced into a string is not a
    // compile-time constant in C#, and spelling the 78 twice is how the two drift.
    private static readonly string ListItemOpen = "<size=" + ListItemSizePercent + "%>";
    private const string ListItemClose = "</size>";

    /// <summary>
    /// The "New Quest!" stamp, as a fraction of the body's font size. Tied to the
    /// list items so the corner mark and the entries under it are one small size
    /// rather than two nearly-equal ones.
    /// </summary>
    private const float StampSizeFraction = ListItemSizePercent / 100f;

    // ---- how big the words are: how many of the art's pixels one of the font's covers ----

    /// <summary>The card's own words fit themselves between these. The serif's 1.8 to 3.2, scaled so the capitals are the height they were.</summary>
    private const float BodyScaleMin = 0.375f;
    private const float BodyScaleMax = 2f / 3f;

    /// <summary>A reward's name: the size the body tops out at, two screen pixels to the font's one at 1080p.</summary>
    private const float NameScale = 2f / 3f;

    /// <summary>The word on a frame's top edge, and an item's effect under its name.</summary>
    private const float SmallScale = 0.5f;

    // ---- a reward's frame, in the art's pixels ----

    /// <summary>Between one frame and the next.</summary>
    private const int FrameGap = 6;

    /// <summary>Between the frame's line and what is inside it: at each side, over the name, under the last line.</summary>
    private const int FramePadX = 6;
    private const int FramePadTop = 3;
    private const int FramePadBottom = 2;

    /// <summary>
    /// How far the effect is pulled up under the name. A line of this font keeps
    /// three pixels clear under its letters' feet, and a second line stacked
    /// straight below that reads as a separate paragraph.
    /// </summary>
    private const int EffectLift = 2;

    /// <summary>The line stops this far short of the word written across it, either side.</summary>
    private const int LegendClear = 3;
    /// <summary>The least line left between a corner and that word, so the top edge always reads as an edge.</summary>
    private const int CornerRun = 4;
    private const int LegendHeight = 12;

    /// <summary>
    /// How tall an icon stands. The icons are drawn 32 pixels tall (a few 16),
    /// and this is 32 of the SCREEN's pixels at 1080p, so one of the icon's
    /// pixels lands on one of the screen's (or on two by two).
    /// </summary>
    private const float IconHeight = 32f / 3f;
    private const int IconGap = 3;

    /// <summary>The words over a row of frames end this far above the row's top line.</summary>
    private const int HeadingGap = 2;
    /// <summary>The quest's own words end at least this far above the "Reward:" under them.</summary>
    private const int WordsGap = 1;

    /// <summary>
    /// A label is given this much more room than its words measured, so a line
    /// that fits exactly is not broken in two by a rounding error.
    /// </summary>
    private const float Slack = 0.5f;

    private TextMeshPro _label;
    /// <summary>The "New Quest!" mark in the card's top right corner.</summary>
    private TextMeshPro _stamp;
    /// <summary>"Reward:", over the row of frames on a new quest's card.</summary>
    private TextMeshPro _heading;
    private QuestBackdrop _backdrop;
    private int _plateWidth;

    /// <summary>One for each reward on the card, made the first time a card needs that many.</summary>
    private readonly List<RewardFrame> _frames = new List<RewardFrame>();
    /// <summary>How tall one line of a reward's name is, in the art's pixels. Measured with each row.</summary>
    private float _nameLine;

    /// <summary>One thing a quest pays, as its frame shows it.</summary>
    private struct RewardLine
    {
        /// <summary>Written across the frame's top edge: what kind of thing this is.</summary>
        public string Kind;

        /// <summary>The word over the frame when it is handed over: "Obtained" for a thing you now hold, "Unlocked" for a place you may now go.</summary>
        public string Verb;

        public string Name;

        /// <summary>Its effect line, in smaller letters under the name. Equipment only.</summary>
        public string Effect;

        public Sprite Icon;

        /// <summary>The icon follows the name instead of leading it. Crystals: the count, then the crystal.</summary>
        public bool IconLast;
    }

    /// <summary>
    /// What one reward's frame is drawn with, and the sizes worked out for it on
    /// the card being shown. Sizes are in the art's pixels.
    /// </summary>
    private sealed class RewardFrame
    {
        public GameObject Root;
        public SpriteRenderer Line;
        public SpriteRenderer Icon;
        public TextMeshPro Legend;
        public TextMeshPro Name;
        public TextMeshPro Effect;

        public float LegendWidth;
        public float NameWidth;
        /// <summary>The icon and the gap beside it. Nothing when there is no icon.</summary>
        public float IconWidth;
        public bool HasEffect;

        /// <summary>The narrowest the inside may be, and what it would take to put the effect on one line.</summary>
        public float Least;
        public float Want;
        /// <summary>What it was given.</summary>
        public float Inner;

        public float NameHeight;
        public float EffectHeight;

        public float NameRoom => Mathf.Max(1f, Inner - IconWidth);
        public float ContentHeight => NameHeight + (HasEffect ? EffectHeight - EffectLift : 0f);
    }

    public static QuestNotices Create(WaveName banner)
    {
        var go = new GameObject("Quest Notices");
        // The same plate in the same place as the dialogue. A reward box that
        // arrived somewhere else would read as a different screen rather than as
        // the conversation continuing.
        go.transform.position = new Vector3(0f, QuestBackdrop.CenterY, 0f);
        var notices = go.AddComponent<QuestNotices>();
        notices.Build();
        return notices;
    }

    private void Build()
    {
        _plateWidth = QuestBackdrop.WidthPixels;
        _backdrop = QuestBackdrop.Create(transform);

        int pad = QuestBackdrop.PaddingPixels;
        var block = new Vector2(_plateWidth - pad * 2, QuestBackdrop.HeightPixels - pad * 2) * QuestBackdrop.Pixel;

        // The card's own words, hung from the plate's top left corner. Its block
        // is as wide as the plate allows; how TALL it is depends on what else the
        // card carries, and is set for each card — see LayOut.
        _label = MakeLabel("Notice Body", transform, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
        _label.transform.localPosition = At(pad, pad);
        _label.rectTransform.sizeDelta = block;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = RunFont.WorldSize(BodyScaleMin);
        _label.fontSizeMax = RunFont.WorldSize(BodyScaleMax);

        // The corner mark, in its own object. Sharing the body label would mean
        // fighting TMP for a right-aligned run on a left-aligned line; a second
        // label hung from the top right corner lands there exactly, and cannot
        // push the words below it around.
        _stamp = MakeLabel("Notice Stamp", transform, TextAlignmentOptions.TopRight, new Vector2(1f, 1f));
        _stamp.transform.localPosition = At(_plateWidth - pad, pad);
        _stamp.rectTransform.sizeDelta = block;
        _stamp.fontStyle = FontStyles.Italic;

        // Stood on its bottom left corner, because where it goes is counted up
        // from the row of frames under it.
        _heading = MakeLabel("Notice Reward Heading", transform, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f));

        // The rewards' frames are made on demand in Frame: a reward card shows
        // one, a quest card shows one for each thing the quest pays, and neither
        // number is known here.
    }

    /// <summary>
    /// A point on the plate, counted in the art's pixels from its left edge and
    /// down from its top edge, in this object's own coordinates.
    /// </summary>
    private Vector3 At(float fromLeft, float fromTop)
    {
        return new Vector3((-_plateWidth * 0.5f + fromLeft) * QuestBackdrop.Pixel,
                           (QuestBackdrop.HeightPixels * 0.5f - fromTop) * QuestBackdrop.Pixel,
                           0f);
    }

    /// <summary>A length TMP measured, in the art's pixels.</summary>
    private static float Pixels(float world)
    {
        return world / QuestBackdrop.Pixel;
    }

    /// <summary>
    /// A label in the run's pixel font, like the dialogue box these cards follow.
    /// <paramref name="pivot"/> is the corner of its block it is positioned by.
    /// </summary>
    private static TextMeshPro MakeLabel(string name, Transform parent, TextAlignmentOptions align, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshPro>();
        RunFont.Apply(label);

        label.rectTransform.pivot = pivot;
        label.alignment = align;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.color = Gold;
        label.richText = true;
        label.text = "";
        label.alpha = 0f;

        var mesh = go.GetComponent<MeshRenderer>();
        if (mesh != null)
        {
            mesh.sortingLayerName = SortingLayer;
            mesh.sortingOrder = SortingOrder;
        }
        return label;
    }

    private static SpriteRenderer MakeRenderer(string name, Transform parent, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = order;
        renderer.color = new Color(1f, 1f, 1f, 0f);
        renderer.enabled = false;
        return renderer;
    }

    /// <summary>
    /// Everything the quest paid out, one box at a time, each over its own chime
    /// and each un-skippable until the chime has finished. A reward that can be
    /// pressed past in the same motion as the line before it is a reward the player
    /// will swear they never got.
    /// </summary>
    public IEnumerator ShowReward(Quest quest)
    {
        var rewards = Describe(quest != null ? quest.Reward : null, handedOver: true);
        var one = new List<RewardLine>(1);
        for (int i = 0; i < rewards.Count; i++)
        {
            // The word, and under it the same frame the quest's card promised
            // this in: its name, its art, and what it does.
            one.Clear();
            one.Add(rewards[i]);
            yield return Raise(rewards[i].Verb, null, one);

            var jingle = AudioManager.Instance != null ? AudioManager.Instance.rewardJingle : null;
            float hold = MinimumRewardHold;
            if (jingle != null && jingle.clip != null)
            {
                AudioManager.Instance.PlaySFX(jingle);
                hold = Mathf.Max(hold, jingle.clip.length);
            }

            // The gate. Presses during this are deliberately ignored rather than
            // buffered — a press meant for the box before should not spend the
            // acknowledgement for this one.
            float until = Time.unscaledTime + hold;
            while (Time.unscaledTime < until) yield return null;

            while (!QuestSceneInput.AdvancePressed()) yield return null;
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
            yield return Lower();
        }
    }

    /// <summary>
    /// The card that goes up the instant a quest is finished, BEFORE anyone walks
    /// on to react to it: the quest's name and the thing that was asked, stamped
    /// complete.
    ///
    /// No reward on it. What the quest pays is handed over afterwards, by the NPC,
    /// one box at a time — printing it here as well would spoil every one of those
    /// boxes before they open and turn the payout into a re-read.
    /// </summary>
    public IEnumerator ShowCompleted(Quest quest)
    {
        if (quest == null) yield break;

        yield return Raise(DescribeCompleted(quest), "Quest Complete!");

        float readyAt = Time.unscaledTime + 0.25f;
        while (Time.unscaledTime < readyAt) yield return null;
        while (!QuestSceneInput.AdvancePressed()) yield return null;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        yield return Lower();
    }

    /// <summary>
    /// Each newly opened quest on its own card: name, its objectives in italics,
    /// then what it pays. One at a time, because two of them stacked is a list and
    /// a list is the wall of text this whole revamp exists to delete.
    /// </summary>
    public IEnumerator ShowUnlocked(IList<Quest> quests)
    {
        for (int i = 0; i < quests.Count; i++)
        {
            // What it pays, in the same order the reward boxes hand it over and
            // in the same frames, so the card that promises a thing and the box
            // that hands it over show the same picture.
            var rewards = Describe(quests[i].Reward, handedOver: false);
            yield return Raise(DescribeUnlocked(quests[i]), "New Quest!", rewards,
                               rewards.Count == 1 ? "Reward:" : "Rewards:");

            float readyAt = Time.unscaledTime + 0.25f;
            while (Time.unscaledTime < readyAt) yield return null;
            while (!QuestSceneInput.AdvancePressed()) yield return null;

            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
            yield return Lower();
        }
    }

    // ---------- what a reward is made of ----------

    /// <summary>
    /// A reward broken into the things it actually gives, in the order they should
    /// be shown: where you may go, then what you hold, then what you may now draft,
    /// then the money.
    ///
    /// Crystals go LAST because they are the consolation half of a mixed reward —
    /// leading with them buries the item behind a number.
    ///
    /// <paramref name="handedOver"/> is whether this is the reward being given
    /// (the boxes after a finished quest) or being promised (a new quest's card).
    /// </summary>
    private static List<RewardLine> Describe(QuestReward reward, bool handedOver)
    {
        var lines = new List<RewardLine>();
        if (reward == null) return lines;

        var catalog = EquipmentCatalog.Instance;

        // "Unlocked", not "Obtained". A map is somewhere you may now go, and a
        // player told they obtained a mine will look for it in their bags.
        if (reward.UnlocksMap)
        {
            lines.Add(new RewardLine
            {
                Kind = "New Map",
                Verb = "Unlocked",
                // By NAME on the quest's card, open or not. That card is the
                // quest's introduction, so the map is by definition not open
                // yet, and naming the place you are being sent is the point of
                // the line. Handed over, the reward's own wording is used, which
                // is the name once the map is open.
                Name = handedOver ? reward.DescribeMapUnlock() : MapName(reward.UnlocksMapId),
            });
        }

        if (reward.GrantsEquipment)
        {
            var def = catalog != null ? catalog.Find(reward.EquipmentId) : null;
            lines.Add(new RewardLine
            {
                Kind = "Equipment",
                Verb = "Obtained",
                Name = def != null ? def.DisplayName : reward.EquipmentId,
                Icon = def != null ? def.Icon : null,
                // What it does, on the spot. A reward the player cannot evaluate is
                // a reward they cannot want, and the equipment screen is two menus
                // away from here.
                Effect = def != null ? def.Effect : "",
            });
        }

        if (reward.ExtraEquipmentSlot)
        {
            lines.Add(new RewardLine
            {
                Kind = "New Slot",
                Verb = "Obtained",
                Name = "Another equipment slot",
                Icon = catalog != null ? catalog.EquipmentSlotIcon : null,
            });
        }

        if (reward.ExtraSpecialSlot)
        {
            lines.Add(new RewardLine
            {
                Kind = "New Slot",
                Verb = "Obtained",
                Name = "Another special slot",
                Icon = catalog != null ? catalog.SpecialSlotIcon : null,
            });
        }

        if (reward.RevealsUpgrade)
        {
            lines.Add(new RewardLine
            {
                Kind = "New Upgrade",
                Verb = "Obtained",
                Name = QuestUpgradeReveals.DisplayName(reward.UpgradeSlug),
            });
        }

        if (reward.Crystals > 0)
        {
            // The count then the icon, never the word — the same form CrystalText
            // draws everywhere else in the game. The word is on the frame.
            lines.Add(new RewardLine
            {
                Kind = "Crystals",
                Verb = "Obtained",
                Name = reward.Crystals.ToString(),
                Icon = catalog != null ? catalog.CrystalIcon : null,
                IconLast = true,
            });
        }

        return lines;
    }

    /// <summary>
    /// The finished quest's card: the same heading and the same italic ask the
    /// offer card showed, under an "Objective" label so the player reads it as
    /// what they did rather than as a fresh thing to go and do.
    /// </summary>
    private static string DescribeCompleted(Quest quest)
    {
        var card = new StringBuilder();
        card.Append("<size=125%><u>").Append(quest.Name).Append("</u></size>");

        if (quest.HasObjectives)
        {
            card.Append('\n');
            card.Append(CardLineCount(quest) == 1 ? "Objective" : "Objectives");
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null || objective.HideOnNpcCard) continue;
                // The ask as it was written for the card, not a counter. It is
                // finished, so "500/500" is a number the player has to read to
                // learn nothing.
                card.Append('\n');
                card.Append("<i>").Append(ListItemOpen);
                card.Append(objective.NpcLine);
                card.Append(ListItemClose).Append("</i>");
            }
        }

        return card.ToString();
    }

    // How many objectives the card will list - some leave themselves off it.
    private static int CardLineCount(Quest quest)
    {
        int lines = 0;
        for (int i = 0; i < quest.Objectives.Length; i++)
        {
            if (quest.Objectives[i] != null && !quest.Objectives[i].HideOnNpcCard) lines++;
        }
        return lines;
    }

    /// <summary>
    /// A new quest's own words: its name and what it asks. What it pays is not
    /// in here — that is drawn under these as a row of frames.
    /// </summary>
    private static string DescribeUnlocked(Quest quest)
    {
        var card = new StringBuilder();
        // The name is the heading of the card, so it is sized and underlined as
        // one. Everything below it is the ask, in plain weight.
        card.Append("<size=125%><u>").Append(quest.Name).Append("</u></size>");

        if (quest.HasObjectives)
        {
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null || objective.HideOnNpcCard) continue;
                // No counter. Progress on a quest revealed this second is either
                // zero or an accident of a shared stat, and either way the words
                // written for the card are a better first impression of the ask
                // than "0/500".
                card.Append('\n');
                card.Append("<i>").Append(ListItemOpen);
                card.Append(objective.NpcLine);
                card.Append(ListItemClose).Append("</i>");
            }
        }

        return card.ToString();
    }

    /// <summary>The map's real name, open or not, falling back to its id.</summary>
    private static string MapName(string mapId)
    {
        var catalog = MapCatalog.Instance;
        var map = catalog != null ? catalog.Find(mapId) : null;
        return map != null ? map.DisplayName : mapId;
    }

    // ---------- drawing one box ----------

    /// <summary>
    /// Lays a card out and fades it up with the plate behind it. The plate never
    /// resizes — it is the same fixed panel the dialogue speaks from, so a reward
    /// and the line that earned it are one surface rather than two.
    /// </summary>
    private IEnumerator Raise(string words, string stamp = null,
                              List<RewardLine> rewards = null, string rowHeading = null)
    {
        LayOut(words, stamp, rewards, rowHeading);
        yield return Fade(1f);
    }

    /// <summary>
    /// Puts a card's pieces where they go. Three kinds of card come through here:
    ///
    ///   words only                    a finished quest. The words have the whole plate.
    ///   words, heading and rewards    a new quest. The frames stand in a row on the
    ///                                 plate's foot with "Reward:" over them, and the
    ///                                 quest's words fit themselves into what is left
    ///                                 above.
    ///   a word and one reward         a reward being handed over. The frame sits
    ///                                 straight under the word.
    ///
    /// The frames are measured FIRST, because they do not shrink: a reward's name
    /// is the same size on every card. The quest's words are what give way.
    /// </summary>
    private void LayOut(string words, string stamp, List<RewardLine> rewards, string rowHeading)
    {
        int pad = QuestBackdrop.PaddingPixels;
        float textWidth = _plateWidth - pad * 2;
        float textHeight = QuestBackdrop.HeightPixels - pad * 2;

        for (int i = 0; i < _frames.Count; i++) _frames[i].Root.SetActive(false);

        int count = rewards != null ? rewards.Count : 0;
        int rowHeight = count > 0 ? MeasureRow(rewards, textWidth) : 0;
        bool onFoot = count > 0 && !string.IsNullOrEmpty(rowHeading);

        float rowTop = 0f;
        float wordsHeight = textHeight;
        float headingHeight = 0f;

        if (onFoot)
        {
            rowTop = QuestBackdrop.HeightPixels - pad - rowHeight;

            // Room is kept for the heading at the biggest the body can be. It is
            // then set at whatever size the body came out, so the two are one
            // size; if that is smaller it simply stands lower in its room.
            _heading.fontSize = _label.fontSizeMax;
            headingHeight = Pixels(_heading.GetPreferredValues(rowHeading).y);
            wordsHeight = rowTop - HeadingGap - headingHeight - WordsGap - pad;
        }
        else if (count > 0)
        {
            // Measured before the words are set, as TMP measures with the label
            // itself. One word at the body's biggest size, which is what it gets.
            float used = string.IsNullOrEmpty(words)
                ? 0f
                : Pixels(_label.GetPreferredValues(words, textWidth * QuestBackdrop.Pixel, 1000f).y);
            rowTop = Mathf.Ceil(pad + used + HeadingGap);
        }

        _label.rectTransform.sizeDelta = new Vector2(textWidth, Mathf.Max(1f, wordsHeight)) * QuestBackdrop.Pixel;
        _label.text = words ?? "";
        // Forces the layout now, so the stamp and the heading below are sized
        // off THIS text rather than off whatever stood here last.
        _label.ForceMeshUpdate();

        SetStamp(stamp);

        _heading.text = onFoot ? rowHeading : "";
        if (onFoot)
        {
            _heading.fontSize = _label.fontSize;
            _heading.rectTransform.sizeDelta = new Vector2(textWidth, headingHeight) * QuestBackdrop.Pixel;
            _heading.transform.localPosition = At(pad, rowTop - HeadingGap);
        }

        if (count > 0) PlaceRow(rewards, pad, rowTop, rowHeight);

        // Whatever was made after the last fade is still at its starting alpha.
        SetAlpha(_label.alpha);
    }

    /// <summary>
    /// Puts the corner mark up, sized off the body AFTER the body has been laid
    /// out. The body autosizes, so its point size is not known until then, and a
    /// fixed size would tower over a small card and vanish on a large one.
    /// </summary>
    private void SetStamp(string text)
    {
        if (_stamp == null) return;
        _stamp.text = string.IsNullOrEmpty(text) ? "" : text;
        if (string.IsNullOrEmpty(text)) return;
        _stamp.fontSize = _label.fontSize * StampSizeFraction;
        _stamp.ForceMeshUpdate();
    }

    private IEnumerator Lower()
    {
        yield return Fade(0f);
        _label.text = "";
        if (_stamp != null) _stamp.text = "";
        if (_heading != null) _heading.text = "";
        // Switched off rather than destroyed — the next card usually wants them back.
        for (int i = 0; i < _frames.Count; i++) _frames[i].Root.SetActive(false);
    }

    // ---------- the rewards' frames ----------

    /// <summary>
    /// The nth frame, made on demand. A pool rather than one, because a quest's
    /// card lists everything the quest pays.
    /// </summary>
    private RewardFrame Frame(int index)
    {
        while (_frames.Count <= index)
        {
            var root = new GameObject("Notice Reward " + _frames.Count);
            root.transform.SetParent(transform, false);

            var frame = new RewardFrame { Root = root };
            frame.Line = MakeRenderer("Reward Frame", root.transform, FrameOrder);

            // A SpriteRenderer rather than a TMP sprite tag, because that would
            // need a TMP sprite asset built from every icon in the game, kept in
            // step with the catalog by hand.
            frame.Icon = MakeRenderer("Reward Icon", root.transform, IconOrder);

            // Centred on the frame's top line by its capitals, as the speaker's
            // name is on the dialogue box's tab.
            frame.Legend = MakeLabel("Reward Kind", root.transform, TextAlignmentOptions.Capline, new Vector2(0.5f, 0.5f));
            frame.Legend.fontSize = RunFont.WorldSize(SmallScale);
            frame.Legend.textWrappingMode = TextWrappingModes.NoWrap;

            frame.Name = MakeLabel("Reward Name", root.transform, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
            frame.Name.fontSize = RunFont.WorldSize(NameScale);

            frame.Effect = MakeLabel("Reward Effect", root.transform, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
            frame.Effect.fontSize = RunFont.WorldSize(SmallScale);

            _frames.Add(frame);
        }
        return _frames[index];
    }

    /// <summary>
    /// Works out how wide each reward's frame is, and returns how tall the row of
    /// them stands, in the art's pixels.
    ///
    /// A frame is as wide as what is in it and no wider: its name with its icon,
    /// or the word on its top edge if that is longer. One with an effect would
    /// like to be wide enough to put the effect on a single line, and gets as
    /// much of that as the row has left; the effect wraps inside whatever it was
    /// given. So crystals come out narrow, and an item takes the room.
    ///
    /// Every frame in a row is the same height, the tallest one's.
    /// </summary>
    private int MeasureRow(List<RewardLine> rewards, float rowWidth)
    {
        int count = rewards.Count;
        float room = rowWidth - FrameGap * (count - 1) - FramePadX * 2 * count;
        float leastTotal = 0f;
        float extraTotal = 0f;

        // One line of a name, for standing an icon level with it — see PlaceRow.
        _nameLine = Pixels(Frame(0).Name.GetPreferredValues("H").y);

        for (int i = 0; i < count; i++)
        {
            RewardFrame frame = Frame(i);
            RewardLine reward = rewards[i];
            frame.Root.SetActive(true);

            frame.LegendWidth = Mathf.Ceil(Pixels(frame.Legend.GetPreferredValues(reward.Kind ?? "").x));
            frame.NameWidth = Mathf.Ceil(Pixels(frame.Name.GetPreferredValues(reward.Name ?? "").x));
            frame.IconWidth = reward.Icon != null ? IconWidth(reward.Icon) + IconGap : 0f;
            frame.HasEffect = !string.IsNullOrEmpty(reward.Effect);

            // The word on the top edge needs the line to carry on a little
            // either side of it, or the frame has no top.
            float legendNeeds = frame.LegendWidth + (LegendClear + CornerRun) * 2 - FramePadX * 2;
            frame.Least = Mathf.Ceil(Mathf.Max(frame.IconWidth + frame.NameWidth, legendNeeds));
            frame.Want = frame.HasEffect
                ? Mathf.Max(frame.Least, Mathf.Ceil(Pixels(frame.Effect.GetPreferredValues(reward.Effect).x)))
                : frame.Least;

            leastTotal += frame.Least;
            extraTotal += frame.Want - frame.Least;
        }

        float spare = room - leastTotal;
        float tallest = 0f;

        for (int i = 0; i < count; i++)
        {
            RewardFrame frame = _frames[i];
            RewardLine reward = rewards[i];

            if (spare >= extraTotal) frame.Inner = frame.Want;
            else if (spare > 0f) frame.Inner = frame.Least + Mathf.Floor((frame.Want - frame.Least) * spare / extraTotal);
            // More names than the plate is wide. Nothing in the game does this;
            // if something ever does, every frame gives up its share and the
            // names wrap.
            else frame.Inner = Mathf.Max(1f, Mathf.Floor(frame.Least * room / leastTotal));

            frame.NameHeight = Pixels(frame.Name.GetPreferredValues(
                reward.Name ?? "", (frame.NameRoom + Slack) * QuestBackdrop.Pixel, 1000f).y);
            frame.EffectHeight = frame.HasEffect
                ? Pixels(frame.Effect.GetPreferredValues(
                    reward.Effect, (frame.Inner + Slack) * QuestBackdrop.Pixel, 1000f).y)
                : 0f;

            tallest = Mathf.Max(tallest, frame.ContentHeight);
        }

        return Mathf.CeilToInt(FramePadTop + tallest + FramePadBottom);
    }

    /// <summary>
    /// Draws the row <see cref="MeasureRow"/> measured, its top left corner at
    /// <paramref name="left"/>, <paramref name="top"/> on the plate.
    /// </summary>
    private void PlaceRow(List<RewardLine> rewards, float left, float top, int height)
    {
        float x = left;
        for (int i = 0; i < rewards.Count; i++)
        {
            RewardFrame frame = _frames[i];
            RewardLine reward = rewards[i];

            int width = Mathf.RoundToInt(frame.Inner) + FramePadX * 2;
            int gapWidth = Mathf.Clamp(Mathf.RoundToInt(frame.LegendWidth) + LegendClear * 2, 0, Mathf.Max(0, width - CornerRun * 2));
            int gapFrom = (width - gapWidth) / 2;

            // A sprite is drawn from its middle, and a frame may be an odd
            // number of pixels across, so the middle is placed on a half pixel
            // to keep the line on whole ones.
            frame.Line.sprite = QuestBackdrop.Outline(width, height, gapFrom, gapWidth);
            QuestBackdrop.Unlight(frame.Line);
            frame.Line.transform.localPosition = At(x + width * 0.5f, top + height * 0.5f);
            frame.Line.enabled = true;

            // The middle of its capitals on the middle of the line.
            frame.Legend.rectTransform.sizeDelta = new Vector2(gapWidth, LegendHeight) * QuestBackdrop.Pixel;
            frame.Legend.transform.localPosition = At(x + gapFrom + gapWidth * 0.5f, top + 0.5f);
            frame.Legend.text = reward.Kind ?? "";

            // What is inside stands in the middle of the frame's height, so a
            // name alone sits level in a row made tall by its neighbour's effect.
            float inside = height - FramePadTop - FramePadBottom;
            float contentTop = Mathf.Round(top + FramePadTop + (inside - frame.ContentHeight) * 0.5f);

            // And a name alone stands in the middle of its width too. Under an
            // effect it starts at the left, on the same edge the effect does.
            float nameShown = Mathf.Min(frame.NameWidth, frame.NameRoom);
            float headWidth = frame.IconWidth + nameShown;
            float headLeft = x + FramePadX;
            if (!frame.HasEffect) headLeft += Mathf.Max(0f, Mathf.Floor((frame.Inner - headWidth) * 0.5f));

            float nameLeft = reward.IconLast ? headLeft : headLeft + frame.IconWidth;
            float iconLeft = reward.IconLast ? headLeft + nameShown + IconGap : headLeft;

            frame.Name.rectTransform.sizeDelta = new Vector2(frame.NameRoom + Slack, frame.NameHeight) * QuestBackdrop.Pixel;
            frame.Name.transform.localPosition = At(nameLeft, contentTop);
            frame.Name.text = reward.Name ?? "";

            frame.Effect.rectTransform.sizeDelta = new Vector2(frame.Inner + Slack, Mathf.Max(1f, frame.EffectHeight)) * QuestBackdrop.Pixel;
            frame.Effect.transform.localPosition = At(x + FramePadX, contentTop + frame.NameHeight - EffectLift);
            frame.Effect.text = frame.HasEffect ? reward.Effect : "";

            // In this font the middle of the capitals is the middle of the line,
            // so an icon centred on the name's first line stands level with it.
            float iconWide = reward.Icon != null ? IconWidth(reward.Icon) : 0f;
            PlaceIcon(frame.Icon, reward.Icon, At(iconLeft + iconWide * 0.5f, contentTop + _nameLine * 0.5f));

            x += width + FrameGap;
        }
    }

    /// <summary>How wide an icon is drawn, in the art's pixels, standing <see cref="IconHeight"/> tall.</summary>
    private static float IconWidth(Sprite icon)
    {
        Vector2 size = icon.rect.size;
        return size.y > 0f ? IconHeight * size.x / size.y : IconHeight;
    }

    /// <summary>
    /// Draws <paramref name="icon"/> with its middle on <paramref name="centre"/>,
    /// <see cref="IconHeight"/> tall. Nothing is drawn for a reward with no art.
    /// </summary>
    private static void PlaceIcon(SpriteRenderer renderer, Sprite icon, Vector3 centre)
    {
        renderer.sprite = icon;
        renderer.enabled = icon != null;
        if (icon == null) return;

        float ppu = icon.pixelsPerUnit;
        Vector2 size = icon.rect.size / ppu;
        float scale = size.y > 0f ? IconHeight * QuestBackdrop.Pixel / size.y : 1f;
        renderer.transform.localScale = Vector3.one * scale;

        // Where the TRANSFORM has to go to put the art's middle there, which is
        // not the same point. A SpriteRenderer draws its sprite with the PIVOT
        // on the transform, and these icons are imported pivot-at-the-bottom
        // (SpriteAlignment 7, BottomCenter — see crystal.aseprite.meta), not
        // centred. Read off the sprite rather than assumed, because the art this
        // places is whatever the catalog holds — crystals, equipment, slots —
        // and they do not all share a pivot.
        Vector2 pivotOffCentre = icon.pivot / ppu - size * 0.5f;
        renderer.transform.localPosition = centre + (Vector3)(pivotOffCentre * scale);
    }

    private IEnumerator Fade(float to)
    {
        float from = _label.alpha;
        if (Mathf.Approximately(from, to))
        {
            SetAlpha(to);
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / FadeSeconds));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        _label.alpha = alpha;
        if (_stamp != null) _stamp.alpha = alpha;
        if (_heading != null) _heading.alpha = alpha;
        _backdrop.SetAlpha(alpha);

        // White tint: the frame's colour is baked into its sprite and an icon is
        // drawn in its own, so the tint carries nothing but the fade.
        var tint = new Color(1f, 1f, 1f, alpha);
        for (int i = 0; i < _frames.Count; i++)
        {
            RewardFrame frame = _frames[i];
            frame.Line.color = tint;
            frame.Icon.color = tint;
            frame.Legend.alpha = alpha;
            frame.Name.alpha = alpha;
            frame.Effect.alpha = alpha;
        }
    }
}
