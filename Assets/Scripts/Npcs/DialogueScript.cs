using System.Collections.Generic;
using System.Text;

/// <summary>
/// Parses the authored quest text into pages of characters with a speed each, plus
/// the staging directives that fire between them.
///
/// The markup is the one the doc was written in, kept as authored rather than
/// converted to something tidier, so a line in Docs/Design/quest-script.md and the
/// line in the line file are the same string:
///
///   \n          page break — the box clears and waits for the next press
///   &lt;slow&gt;      TOGGLE, not an open tag. The same tag opens and closes a span;
///               there is no &lt;/slow&gt;. Two of them wrap a phrase.
///   &lt;fast&gt;      the same, the other way
///   &lt;crown&gt;     the King's crown layer appears from here on
///   &lt;nocrown&gt;   ...and is hidden from here on
///   &lt;exit&gt;      the NPC leaves now; the next page waits until he has settled
///
/// A toggle is genuinely simpler for an author than a matched pair — there is no
/// way to nest one wrongly, and forgetting the closing one just means the rest of
/// the page is slow, which is visible immediately rather than a parse error.
/// </summary>
public static class DialogueScript
{
    public enum Speed { Normal, Slow, Fast }

    /// <summary>Something that happens at a page boundary rather than being spoken.</summary>
    public enum Directive { None, ShowCrown, HideCrown, Exit }

    public struct Letter
    {
        public char Character;
        public Speed Speed;
    }

    public class Page
    {
        public readonly List<Letter> Letters = new List<Letter>();
        /// <summary>Fires BEFORE this page is typed.</summary>
        public Directive Before = Directive.None;

        /// <summary>The page's text with no markup, for measuring and for the label.</summary>
        public string PlainText
        {
            get
            {
                var sb = new StringBuilder(Letters.Count);
                for (int i = 0; i < Letters.Count; i++) sb.Append(Letters[i].Character);
                return sb.ToString();
            }
        }

        public bool IsEmpty => Letters.Count == 0;
    }

    /// <summary>
    /// Pages in order. A directive sitting alone on a page attaches itself to the
    /// NEXT page rather than becoming a blank box the player has to press through —
    /// which is what "&lt;exit&gt; on its own line" in the doc is asking for.
    /// </summary>
    public static List<Page> Parse(string source)
    {
        var pages = new List<Page>();
        if (string.IsNullOrEmpty(source)) return pages;

        var speed = Speed.Normal;
        var pending = Directive.None;
        var page = new Page();

        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];

            if (c == '\n')
            {
                if (!page.IsEmpty)
                {
                    page.Before = pending;
                    pending = Directive.None;
                    pages.Add(page);
                }
                page = new Page();
                i++;
                continue;
            }

            if (c == '<')
            {
                int close = source.IndexOf('>', i + 1);
                if (close > i)
                {
                    string tag = source.Substring(i + 1, close - i - 1).ToLowerInvariant();
                    bool handled = true;
                    switch (tag)
                    {
                        // Toggles: the same tag turns the span on and off again.
                        case "slow": speed = speed == Speed.Slow ? Speed.Normal : Speed.Slow; break;
                        case "fast": speed = speed == Speed.Fast ? Speed.Normal : Speed.Fast; break;
                        case "crown": pending = Directive.ShowCrown; break;
                        case "nocrown": pending = Directive.HideCrown; break;
                        case "exit": pending = Directive.Exit; break;
                        default: handled = false; break;
                    }
                    if (handled)
                    {
                        i = close + 1;
                        continue;
                    }
                    // An unknown tag is left alone rather than eaten. Better for the
                    // player to see a stray "<foo>" than for prose to vanish.
                }
            }

            page.Letters.Add(new Letter { Character = c, Speed = speed });
            i++;
        }

        if (!page.IsEmpty)
        {
            page.Before = pending;
            pending = Directive.None;
            pages.Add(page);
        }
        else if (pending != Directive.None && pages.Count > 0)
        {
            // A directive after the last page still has to run — the King's exit is
            // the whole reason: it trails his final line rather than preceding one.
            var tail = new Page();
            tail.Before = pending;
            pages.Add(tail);
        }

        return pages;
    }

    /// <summary>Seconds per character. Punctuation holds a beat longer than a letter.</summary>
    public static float SecondsFor(Letter letter)
    {
        // Tuned by reading it, twice. 0.032s a character was a wipe rather than
        // speech; 0.055 was slow enough to wait on. This sits between them.
        float baseDelay;
        switch (letter.Speed)
        {
            case Speed.Slow: baseDelay = 0.10f; break;
            case Speed.Fast: baseDelay = 0.022f; break;
            default: baseDelay = 0.042f; break;
        }

        char c = letter.Character;
        if (c == '.' || c == '!' || c == '?') return baseDelay + 0.20f;
        if (c == ',' || c == ';' || c == ':') return baseDelay + 0.09f;
        return baseDelay;
    }
}
