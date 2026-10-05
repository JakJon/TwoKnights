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
/// </summary>
public class QuestNotices : MonoBehaviour
{
    private static readonly Color Gold = new Color(0.957f, 0.667f, 0.212f, 1f);

    private const string SortingLayer = "Default";
    private const int SortingOrder = 600;
    private const int IconOrder = 601;
    private const float FadeSeconds = 0.25f;

    /// <summary>
    /// Floor for the reward hold, for the case where no jingle is wired yet. The
    /// box is meant to be un-skippable for the length of the melody; with no melody
    /// it still has to be un-skippable for long enough to read.
    /// </summary>
    private const float MinimumRewardHold = 1.4f;

    /// <summary>Size of an objective or reward entry, relative to the card's body text.</summary>
    private const int ListItemSizePercent = 78;

    // The same wrapper ListItem applies, in two halves, for the card that has to
    // write the words between them a piece at a time (see CardBuilder).
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

    private TextMeshPro _label;
    /// <summary>The "New Quest!" mark in the card's top right corner.</summary>
    private TextMeshPro _stamp;
    private QuestBackdrop _backdrop;
    private readonly List<SpriteRenderer> _icons = new List<SpriteRenderer>();
    /// <summary>What new icon renderers are parented to — the body label's object,
    /// so an icon is positioned in the same local space the glyphs are measured in.</summary>
    private GameObject _iconHost;

    /// <summary>One piece of art on a card, and the character it hangs off.</summary>
    private struct IconPlacement
    {
        public Sprite Icon;

        /// <summary>
        /// Index into the card's PLAIN text — every rich-text tag stripped out — of
        /// the character the icon sits after. Plain rather than the string that was
        /// handed to TMP because TMP_TextInfo.characterInfo, which is what the icon
        /// is actually measured against, indexes the parsed text.
        /// </summary>
        public int AfterCharacter;
    }

    /// <summary>One entry in a quest card's reward list.</summary>
    private struct RewardLine
    {
        /// <summary>The thing's name. The icon, if it has one, hangs off the end of this.</summary>
        public string Name;

        /// <summary>Its effect line, drawn italic after the name. Equipment only.</summary>
        public string Effect;

        public Sprite Icon;
    }

    /// <summary>
    /// A card being written: the rich string TMP is handed, and, kept in step with
    /// it, a count of the plain characters TMP will actually lay out.
    ///
    /// The two have to be tracked apart because an icon is placed against a laid-out
    /// GLYPH, and TMP indexes those by parsed text with every tag stripped. Counting
    /// into the rich string instead would walk the icons further and further right
    /// of where they belong as the markup piled up. Line breaks DO count — TMP keeps
    /// them in characterInfo as ordinary (invisible) characters.
    /// </summary>
    private sealed class CardBuilder
    {
        private readonly StringBuilder _rich = new StringBuilder();
        private readonly List<IconPlacement> _icons = new List<IconPlacement>();
        private int _plainLength;

        /// <summary>Markup. TMP eats it and draws nothing, so it counts for nothing.</summary>
        public void Tag(string tag) { _rich.Append(tag); }

        /// <summary>Words the player sees. These are what an icon anchor counts.</summary>
        public void Text(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _rich.Append(text);
            _plainLength += text.Length;
        }

        /// <summary>Hangs an icon off the last character written. Null art is a no-op.</summary>
        public void IconAfterLast(Sprite icon)
        {
            if (icon == null || _plainLength == 0) return;
            _icons.Add(new IconPlacement { Icon = icon, AfterCharacter = _plainLength - 1 });
        }

        public string Body { get { return _rich.ToString(); } }
        public List<IconPlacement> Icons { get { return _icons; } }
    }

    /// <summary>
    /// One thing a quest handed over. Split out of QuestReward because a reward is
    /// a record — several fields on one object — and a presentation is a sequence.
    /// </summary>
    private struct Grant
    {
        /// <summary>"Obtained" for a thing you now hold, "Unlocked" for a place you may now go.</summary>
        public string Verb;
        public string Name;
        public Sprite Icon;
        /// <summary>The item's own effect line, if it has one. Drawn under the name, italic.</summary>
        public string Effect;
    }

    public static QuestNotices Create(WaveName banner)
    {
        var go = new GameObject("Quest Notices");
        // The same plate in the same place as the dialogue. A reward box that
        // arrived somewhere else would read as a different screen rather than as
        // the conversation continuing.
        go.transform.position = new Vector3(0f, QuestBackdrop.CenterY, 0f);
        var notices = go.AddComponent<QuestNotices>();
        notices.Build(banner);
        return notices;
    }

    private void Build(WaveName banner)
    {
        _backdrop = QuestBackdrop.Create(transform);

        var block = new Vector2(QuestBackdrop.TextWidth, QuestBackdrop.TextHeight);
        var bodyGo = new GameObject("Notice Body");
        bodyGo.transform.SetParent(transform, false);
        bodyGo.transform.localPosition = QuestBackdrop.TopLeft(block);

        _label = bodyGo.AddComponent<TextMeshPro>();
        var bannerText = banner != null ? banner.GetComponent<TMP_Text>() : null;
        if (bannerText != null && bannerText.font != null) _label.font = bannerText.font;

        _label.rectTransform.sizeDelta = block;
        _label.alignment = TextAlignmentOptions.TopLeft;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 1.8f;
        _label.fontSizeMax = 3.2f;
        _label.color = Gold;
        _label.richText = true;
        _label.text = "";
        _label.alpha = 0f;

        var mesh = bodyGo.GetComponent<MeshRenderer>();
        if (mesh != null)
        {
            mesh.sortingLayerName = SortingLayer;
            mesh.sortingOrder = SortingOrder;
        }

        // The corner mark, in its own object over the same block. Sharing the body
        // label would mean fighting TMP for a right-aligned run on a left-aligned
        // line; a second label with TopRight alignment over the same rect lands in
        // the corner exactly, and cannot push the words below it around.
        var stampGo = new GameObject("Notice Stamp");
        stampGo.transform.SetParent(bodyGo.transform, false);
        _stamp = stampGo.AddComponent<TextMeshPro>();
        if (_label.font != null) _stamp.font = _label.font;
        _stamp.rectTransform.sizeDelta = block;
        _stamp.alignment = TextAlignmentOptions.TopRight;
        _stamp.enableAutoSizing = false;
        _stamp.fontStyle = FontStyles.Italic;
        _stamp.color = Gold;
        _stamp.richText = true;
        _stamp.text = "";
        _stamp.alpha = 0f;

        var stampMesh = stampGo.GetComponent<MeshRenderer>();
        if (stampMesh != null)
        {
            stampMesh.sortingLayerName = SortingLayer;
            stampMesh.sortingOrder = SortingOrder;
        }

        // The rewards' own art, parked beside their names. SpriteRenderers rather
        // than TMP sprite tags because that would need a TMP sprite asset built
        // from every icon in the game, kept in step with the catalog by hand.
        // Made on demand in IconSlot: a reward box shows one, a quest card shows
        // one per thing the quest pays, and neither number is known here.
        _iconHost = bodyGo;
    }

    /// <summary>
    /// Everything the quest paid out, one box at a time, each over its own chime
    /// and each un-skippable until the chime has finished. A reward that can be
    /// pressed past in the same motion as the line before it is a reward the player
    /// will swear they never got.
    /// </summary>
    public IEnumerator ShowReward(Quest quest)
    {
        var grants = Describe(quest != null ? quest.Reward : null);
        for (int i = 0; i < grants.Count; i++)
        {
            var grant = grants[i];
            string headline = grant.Verb + " " + grant.Name;
            var body = new StringBuilder(headline);
            if (!string.IsNullOrEmpty(grant.Effect)) body.Append("\n<i>").Append(grant.Effect).Append("</i>");

            // The icon goes after the last character of the HEADLINE, which is
            // plain text and so indexes one-for-one into the laid-out characters.
            // Reading "the end of the first line" instead would drop the icon into
            // the middle of a long name that had wrapped.
            yield return Raise(body.ToString(), grant.Icon, headline.Length - 1);

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

        var card = DescribeCompleted(quest);
        yield return Raise(card.Body, card.Icons, stamp: "Quest Complete!");

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
            var card = DescribeUnlocked(quests[i]);
            yield return Raise(card.Body, card.Icons, stamp: "New Quest!");

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
    /// </summary>
    private static List<Grant> Describe(QuestReward reward)
    {
        var grants = new List<Grant>();
        if (reward == null) return grants;

        // "Unlocked", not "Obtained". A map is somewhere you may now go, and a
        // player told they obtained a mine will look for it in their bags.
        if (reward.UnlocksMap)
        {
            grants.Add(new Grant { Verb = "Unlocked", Name = reward.DescribeMapUnlock() });
        }

        if (reward.GrantsEquipment)
        {
            var catalog = EquipmentCatalog.Instance;
            var def = catalog != null ? catalog.Find(reward.EquipmentId) : null;
            grants.Add(new Grant
            {
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
            var catalog = EquipmentCatalog.Instance;
            grants.Add(new Grant
            {
                Verb = "Obtained",
                Name = "another equipment slot",
                Icon = catalog != null ? catalog.EquipmentSlotIcon : null,
            });
        }

        if (reward.ExtraSpecialSlot)
        {
            var catalog = EquipmentCatalog.Instance;
            grants.Add(new Grant
            {
                Verb = "Obtained",
                Name = "another special slot",
                Icon = catalog != null ? catalog.SpecialSlotIcon : null,
            });
        }

        if (reward.RevealsUpgrade)
        {
            grants.Add(new Grant
            {
                Verb = "Obtained",
                Name = QuestUpgradeReveals.DisplayName(reward.UpgradeSlug),
            });
        }

        if (reward.Crystals > 0)
        {
            var catalog = EquipmentCatalog.Instance;
            // The count then the icon, never the word — the same form CrystalText
            // draws everywhere else in the game.
            grants.Add(new Grant
            {
                Verb = "Obtained",
                Name = reward.Crystals.ToString(),
                Icon = catalog != null ? catalog.CrystalIcon : null,
            });
        }

        return grants;
    }

    /// <summary>
    /// The finished quest's card: the same heading and the same italic ask the
    /// offer card showed, under an "Objective" label so the player reads it as
    /// what they did rather than as a fresh thing to go and do.
    /// </summary>
    private static CardBuilder DescribeCompleted(Quest quest)
    {
        var card = new CardBuilder();
        card.Tag("<size=125%><u>");
        card.Text(quest.Name);
        card.Tag("</u></size>");

        if (quest.HasObjectives)
        {
            card.Text("\n");
            card.Text(CardLineCount(quest) == 1 ? "Objective" : "Objectives");
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null || objective.HideOnNpcCard) continue;
                // The ask as it was written for the card, not a counter. It is
                // finished, so "500/500" is a number the player has to read to
                // learn nothing.
                card.Text("\n");
                card.Tag("<i>" + ListItemOpen);
                card.Text(objective.NpcLine);
                card.Tag(ListItemClose + "</i>");
            }
        }

        return card;
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

    private static CardBuilder DescribeUnlocked(Quest quest)
    {
        var card = new CardBuilder();
        // The name is the heading of the card, so it is sized and underlined as
        // one. Everything below it is the ask and the payment, in plain weight.
        card.Tag("<size=125%><u>");
        card.Text(quest.Name);
        card.Tag("</u></size>");

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
                card.Text("\n");
                card.Tag("<i>" + ListItemOpen);
                card.Text(objective.NpcLine);
                card.Tag(ListItemClose + "</i>");
            }
        }

        // One reward per line under the heading. Run together on a single line,
        // a quest paying a map AND an item read as one muddled sentence with a
        // comma in it rather than as two things being handed over.
        var rewards = DescribeRewardLines(quest);
        if (rewards.Count > 0)
        {
            card.Text("\n");
            card.Text(rewards.Count == 1 ? "Reward:" : "Rewards:");
            for (int i = 0; i < rewards.Count; i++)
            {
                card.Text("\n");
                card.Tag(ListItemOpen);
                card.Text(rewards[i].Name);

                // Against the NAME, and the reason the effect line below is a
                // LINE rather than the ": does a thing" tail it used to be: the
                // icon is parked immediately after the last letter of the name,
                // so anything still sitting there would be drawn underneath it.
                // The reward boxes already break the effect off this way, which
                // is why they could carry art and this card could not.
                card.IconAfterLast(rewards[i].Icon);
                card.Tag(ListItemClose);

                if (!string.IsNullOrEmpty(rewards[i].Effect))
                {
                    card.Text("\n");
                    card.Tag(ListItemOpen + "<i>");
                    card.Text(rewards[i].Effect);
                    card.Tag("</i>" + ListItemClose);
                }
            }
        }
        return card;
    }

    /// <summary>
    /// Everything the quest pays, one entry per line, in the same order the
    /// reward boxes hand them over — and with the same art on it, so the card that
    /// promises a thing and the box that hands it over show the same picture.
    /// </summary>
    private static List<RewardLine> DescribeRewardLines(Quest quest)
    {
        var lines = new List<RewardLine>();
        var reward = quest.Reward;
        if (reward == null) return lines;

        var catalog = EquipmentCatalog.Instance;

        // The map by NAME. This used to read "A new map" whenever the map was
        // still locked, which is every time this card is shown — the card is the
        // quest's introduction, so the map is by definition not open yet. Naming
        // the place you are being sent is the point of the line.
        if (reward.UnlocksMap) lines.Add(new RewardLine { Name = MapName(reward.UnlocksMapId) });

        if (reward.GrantsEquipment)
        {
            var def = catalog != null ? catalog.Find(reward.EquipmentId) : null;
            lines.Add(new RewardLine
            {
                Name = def != null ? def.DisplayName : reward.EquipmentId,
                // The item's own effect line, italic, exactly as the shop and the
                // equipment screen render it. A reward the player cannot evaluate is
                // a reward they cannot want.
                Effect = def != null ? def.Effect : null,
                Icon = def != null ? def.Icon : null,
            });
        }

        if (reward.ExtraEquipmentSlot)
        {
            lines.Add(new RewardLine
            {
                Name = "Another equipment slot",
                Icon = catalog != null ? catalog.EquipmentSlotIcon : null,
            });
        }

        if (reward.ExtraSpecialSlot)
        {
            lines.Add(new RewardLine
            {
                Name = "Another special slot",
                Icon = catalog != null ? catalog.SpecialSlotIcon : null,
            });
        }

        if (reward.RevealsUpgrade)
        {
            lines.Add(new RewardLine { Name = QuestUpgradeReveals.DisplayName(reward.UpgradeSlug) });
        }

        if (reward.Crystals > 0)
        {
            lines.Add(new RewardLine
            {
                Name = reward.Crystals + (reward.Crystals == 1 ? " crystal" : " crystals"),
                Icon = catalog != null ? catalog.CrystalIcon : null,
            });
        }
        return lines;
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
    /// Lays a box out and fades it up with the plate behind it. The plate never
    /// resizes — it is the same fixed panel the dialogue speaks from, so a reward
    /// and the line that earned it are one surface rather than two.
    /// </summary>
    private IEnumerator Raise(string body, List<IconPlacement> icons, string stamp = null)
    {
        _label.text = body;
        // Forces the layout now, so the icons below are placed against THIS text
        // rather than against whatever stood here last.
        _label.ForceMeshUpdate();

        SetStamp(stamp);
        PlaceIcons(icons);
        yield return Fade(1f);
    }

    /// <summary>A box carrying at most one piece of art — what a reward box is.</summary>
    private IEnumerator Raise(string body, Sprite icon, int iconAfterCharacter = -1, string stamp = null)
    {
        List<IconPlacement> icons = null;
        if (icon != null)
        {
            icons = new List<IconPlacement>(1)
            {
                new IconPlacement { Icon = icon, AfterCharacter = iconAfterCharacter }
            };
        }
        return Raise(body, icons, stamp);
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
        for (int i = 0; i < _icons.Count; i++) _icons[i].enabled = false;
    }

    /// <summary>
    /// Lays every icon this card asked for against the text that has just been
    /// measured, and switches off any left over from the card before it.
    /// </summary>
    private void PlaceIcons(List<IconPlacement> placements)
    {
        int used = 0;
        if (placements != null)
        {
            var info = _label.textInfo;
            for (int i = 0; i < placements.Count; i++)
            {
                if (PlaceIcon(IconSlot(used), placements[i], info)) used++;
            }
        }

        // Anything the pool still holds beyond what this card needed. Disabled
        // rather than destroyed — the next card usually wants them back.
        for (int i = used; i < _icons.Count; i++) _icons[i].enabled = false;
    }

    /// <summary>
    /// The nth icon renderer, made on demand. A pool rather than one renderer,
    /// because a quest card lists everything the quest pays and several of those
    /// entries have art — which is the whole reason the detail card used to show
    /// no icons at all while the reward boxes after it showed theirs.
    /// </summary>
    private SpriteRenderer IconSlot(int index)
    {
        while (_icons.Count <= index)
        {
            var go = new GameObject("Notice Icon " + _icons.Count);
            go.transform.SetParent(_iconHost.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = IconOrder;
            renderer.enabled = false;
            _icons.Add(renderer);
        }
        return _icons[index];
    }

    /// <summary>
    /// Parks one icon immediately after its anchor character, on that character's
    /// own line. False when there was nothing to draw or nowhere to draw it.
    ///
    /// The position is read off the laid-out glyph rather than computed from the
    /// string, because autosizing means the font size — and therefore where the
    /// name ends — is not known until the text has been laid out. Same reason the
    /// icon's size comes from the measured line height: at font size 1.8 a fixed
    /// scale would tower over the words, at 3.2 it would be lost in them.
    /// </summary>
    private static bool PlaceIcon(SpriteRenderer renderer, IconPlacement placement, TMP_TextInfo info)
    {
        renderer.sprite = placement.Icon;
        renderer.enabled = false;
        if (placement.Icon == null) return false;

        int afterCharacter = placement.AfterCharacter;
        if (info == null || afterCharacter < 0 ||
            afterCharacter >= info.characterCount || afterCharacter >= info.characterInfo.Length)
        {
            return false;
        }

        var last = info.characterInfo[afterCharacter];

        // Measured against the LINE, never against the anchor glyph's own quad.
        // topRight/bottomRight are tight to the letter's ink, so "1 crystal" (ends
        // on a full-height l) and "3 crystals" (ends on a tail-less s) put their
        // midpoints in two different places, and BOTH sat above the words - which
        // is why the crystal floated over the line instead of standing in it.
        //
        // Baseline to ascender is the band the capitals occupy. Sizing the art to
        // that band and centring it on the same band is what "inline" means: the
        // icon is as tall as the text beside it and shares its middle.
        int lineNumber = Mathf.Clamp(last.lineNumber, 0, Mathf.Max(0, info.lineCount - 1));
        float baseline = last.baseLine;
        float band = last.ascender - baseline;
        if (info.lineInfo != null && lineNumber < info.lineInfo.Length)
        {
            var line = info.lineInfo[lineNumber];
            baseline = line.baseline;
            band = line.ascender - baseline;
        }
        if (band <= 0f) band = last.ascender - last.descender;

        float ppu = placement.Icon.pixelsPerUnit;
        Vector2 size = placement.Icon.rect.size / ppu;
        float scale = size.y > 0f ? band / size.y : 1f;
        renderer.transform.localScale = Vector3.one * scale;

        // Where the art has to END UP: sitting in the line's own band, one small gap
        // past the name. xAdvance rather than the ink's right edge, so the gap is the
        // same whether the name ends in a narrow letter or a wide one.
        Vector2 centre = new Vector2(
            last.xAdvance + band * 0.32f + size.x * scale * 0.5f,
            baseline + band * 0.5f);

        // And where the TRANSFORM has to go to put it there, which is not the same
        // point. A SpriteRenderer draws its sprite with the PIVOT on the transform,
        // and these icons are imported pivot-at-the-bottom (SpriteAlignment 7,
        // BottomCenter — see crystal.aseprite.meta), not centred. Setting the
        // transform to the centre therefore drew the whole crystal upward from
        // there, which is the entire reason it floated above its line instead of
        // standing in it. Read off the sprite rather than assumed, because the art
        // this places is whatever the catalog holds — crystals, equipment, map
        // panes — and they do not all share a pivot.
        Vector2 pivotOffCentre = placement.Icon.pivot / ppu - size * 0.5f;
        renderer.transform.localPosition = centre + pivotOffCentre * scale;

        renderer.enabled = true;
        return true;
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
        _backdrop.SetAlpha(alpha);
        for (int i = 0; i < _icons.Count; i++)
        {
            var color = _icons[i].color;
            _icons[i].color = new Color(color.r, color.g, color.b, alpha);
        }
    }
}
