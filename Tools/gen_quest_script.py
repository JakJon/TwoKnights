#!/usr/bin/env python3
"""
Generates Docs/Design/quest-script.md — the hand-authoring document for quest
NPC dialogue.

Every field is prefilled from the quest line files under
Assets/Scripts/Quests/Lines/ EXCEPT the two being written by hand (OFFER and
COMPLETED) and the tutorial-run flag.

Run from the repo root:  python Tools/gen_quest_script.py

Written as a .py file rather than a shell heredoc on purpose: backticks in the
C# payload get command-substituted by the shell and silently gut the output.
"""

import os
import re
import sys
from collections import OrderedDict

LINES_DIR = os.path.join("Assets", "Scripts", "Quests", "Lines")
OUT = os.path.join("Docs", "Design", "quest-script.md")

# Which NPC carries each line, per the revamp. The wizard's accent Order decides
# whether he trails fire or frost; the paladin's tints his glow.
NPC_BY_CLASS = OrderedDict([
    ("ForestQuests",   ("Cartographer", "The Camp Fields")),
    ("MineQuests",     ("Cartographer", "The Mine")),
    ("SerpentQuests",  ("Ninja",        "Order of the Serpent")),
    ("ShadowQuests",   ("Ninja",        "Order of the Shadow")),
    ("EmberQuests",    ("Wizard (fire)",  "Order of the Ember")),
    ("FrigidQuests",   ("Wizard (frost)", "Order of the Frigid")),
    ("GuardianQuests", ("Paladin",      "Order of the Guardian")),
    ("DawnQuests",     ("Paladin",      "Order of the Dawn")),
])

# QuestBuild's map constants.
BUILTIN = {
    "Forest": "camp_fields",
    "Mine": "mine",
    "Keep": "pallid_keep",
    "Camp": "",
}

MAP_NAMES = {
    "camp_fields": "The Camp Fields",
    "mine": "The Mine",
    "pallid_keep": "The Pallid Keep",
    "": "The Camp",
}

# The handful of values computed in C# rather than declared as constants.
# Targets cross-checked against the generated Docs/Design/quests.md.
COMPUTED = {
    "ForestQuests.ExploreStat": "waves.distinct.camp_fields",
    "MineQuests.ExploreStat": "waves.distinct.mine",
    "ForestQuests.FinaleTarget()": 45,
    "MineQuests.FinaleTarget()": 42,
}
# Computed UnlockConditions, as (statKey, atLeast).
COMPUTED_GATES = {
    "ForestQuests.GateCleared": ("maps.camp_fields.gate_cleared", 1),
    "MineQuests.MineOpen": ("maps.mine.unlocked", 1),
}

# Combination-upgrade quests. These do not exist in the line files yet — they
# are proposed here so their prose can be written in the same pass. Both orders
# of Sleeping Dart belong to the ninja, so only he appears for it.
COMBO_QUESTS = [
    dict(
        id="combo_sleeping_dart",
        name="(name me)",
        npc="Ninja",
        upgrade="Sleeping Dart I / II",
        orders="Shadow x Serpent",
        gate=["complete **Initiation: The Green Oath** (Serpent)",
              "complete **Initiation: The Silent Oath** (Shadow)",
              "one knight qualifies on both Orders at once"],
        objective="Acquire the Sleeping Dart  (`upgrades.taken.sleeping_dart_1` >= 1, progress hidden)",
        reward="crystals - amount TBD",
    ),
    dict(
        id="combo_fire_sight",
        name="(name me)",
        npc="Wizard (fire) + Paladin",
        upgrade="Fire Sight",
        orders="Ember x Guardian",
        gate=["complete **Initiation: The Ashen Oath** (Ember)",
              "complete **Initiation: The Standing Order** (Guardian)",
              "one knight qualifies on both Orders at once"],
        objective="Acquire Fire Sight  (`upgrades.taken.fire_sight` >= 1, progress hidden)",
        reward="crystals - amount TBD",
    ),
]


FEATS_FILE = os.path.join("Assets", "Scripts", "Stats", "Feats.cs")


def load_feats():
    """Feats.<Name> constants, so quest objectives that spend a feat key render
    as the key rather than as an unresolved symbol."""
    out = {}
    if not os.path.exists(FEATS_FILE):
        return out
    src = open(FEATS_FILE, encoding="utf8").read()
    for m in re.finditer(r'const\s+string\s+(\w+)\s*=\s*"([^"]*)"', src):
        out["Feats." + m.group(1)] = m.group(2)
        out[m.group(1)] = m.group(2)
    return out


FEATS = load_feats()


# ---------------------------------------------------------------- C# parsing

def strip_comments(src):
    """Remove // and /* */ comments without touching string literals."""
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '"':
            out.append(c)
            i += 1
            while i < n:
                if src[i] == '\\':
                    out.append(src[i:i + 2])
                    i += 2
                    continue
                out.append(src[i])
                if src[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                i += 1
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            i = src.find('*/', i)
            i = n if i < 0 else i + 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def split_top_level(text, sep=','):
    """Split on `sep` at bracket depth zero, ignoring string literals."""
    parts, depth, buf, i, n = [], 0, [], 0, len(text)
    while i < n:
        c = text[i]
        if c == '"':
            buf.append(c)
            i += 1
            while i < n:
                if text[i] == '\\':
                    buf.append(text[i:i + 2])
                    i += 2
                    continue
                buf.append(text[i])
                if text[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        if c == sep and depth == 0:
            parts.append(''.join(buf))
            buf = []
            i += 1
            continue
        buf.append(c)
        i += 1
    if ''.join(buf).strip():
        parts.append(''.join(buf))
    return [p.strip() for p in parts]


def balanced(src, open_idx):
    """Index just past the ')' matching the '(' at open_idx."""
    depth, i, n = 0, open_idx, len(src)
    while i < n:
        c = src[i]
        if c == '"':
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == '"':
                    break
                i += 1
        elif c == '(':
            depth += 1
        elif c == ')':
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise ValueError("unbalanced parentheses")


STRING_RE = re.compile(r'"((?:[^"\\]|\\.)*)"')


def unescape(s):
    return (s.replace('\\"', '"').replace('\\\\', '\\')
             .replace('\\n', '\n').replace('\\t', '\t'))


def as_string(expr, symbols):
    """Evaluate a C# string expression: literals, concatenation, identifiers."""
    expr = expr.strip()
    pieces = split_top_level(expr, '+')
    out = []
    for p in pieces:
        p = p.strip()
        m = STRING_RE.fullmatch(p)
        if m:
            out.append(unescape(m.group(1)))
        elif p in symbols:
            out.append(str(symbols[p]))
        elif p in COMPUTED:
            out.append(str(COMPUTED[p]))
        else:
            out.append("{" + p + "}")   # visible marker if anything is missed
    return ''.join(out)


def as_int(expr, symbols):
    expr = expr.strip()
    if re.fullmatch(r'-?\d+', expr):
        return int(expr)
    if expr in COMPUTED:
        return COMPUTED[expr]
    if expr in symbols:
        return symbols[expr]
    return expr


def parse_call(expr):
    """'One(a, b)' -> ('One', ['a','b']); returns (None, None) if not a call."""
    m = re.match(r'([A-Za-z_][\w.]*)\s*\(', expr.strip())
    if not m:
        return None, None
    start = expr.index('(', m.start())
    end = balanced(expr, start)
    return m.group(1), split_top_level(expr[start + 1:end - 1])


def parse_objectives(expr, symbols):
    """Returns a list of dicts: statKey, target, label, hideProgress."""
    expr = expr.strip()
    calls = []
    name, args = parse_call(expr)
    if name == 'One':
        calls = [args]
    else:
        # new[] { Obj(...), Obj(...) }
        brace = expr.find('{')
        if brace < 0:
            return []
        inner = expr[brace + 1:expr.rfind('}')]
        for item in split_top_level(inner):
            n2, a2 = parse_call(item)
            if n2 == 'Obj':
                calls.append(a2)

    objectives = []
    for args in calls:
        pos = [a for a in args if ':' not in a.split('(')[0]]
        named = {}
        for a in args:
            m = re.match(r'(\w+)\s*:\s*(.+)$', a, re.S)
            if m and m.group(1) in ('label', 'hideProgress', 'statKey', 'target'):
                named[m.group(1)] = m.group(2).strip()
        pos = [a for a in args if not re.match(r'\w+\s*:', a)]
        stat = as_string(pos[0], symbols) if len(pos) > 0 else named.get('statKey', '?')
        target = as_int(pos[1], symbols) if len(pos) > 1 else named.get('target', '?')
        label = as_string(pos[2], symbols) if len(pos) > 2 else (
            as_string(named['label'], symbols) if 'label' in named else None)
        hide = 'true' in named.get('hideProgress', '').lower()
        if len(pos) > 3 and 'true' in pos[3].lower():
            hide = True
        objectives.append(dict(stat=stat, target=target, label=label, hide=hide))
    return objectives


def parse_gate(expr, symbols):
    """Returns a list of dicts: kind ('after'|'stat'), value, atLeast."""
    name, args = parse_call(expr)
    if name != 'Gate':
        return []
    conds = []
    for a in args:
        a = a.strip()
        if a in COMPUTED_GATES:
            key, at = COMPUTED_GATES[a]
            conds.append(dict(kind='stat', key=key, at=at))
            continue
        n2, a2 = parse_call(a)
        if n2 == 'After':
            conds.append(dict(kind='after', quest=as_string(a2[0], symbols)))
        elif n2 == 'Stat':
            key = as_string(a2[0], symbols)
            at = as_int(a2[1], symbols) if len(a2) > 1 else 1
            conds.append(dict(kind='stat', key=key, at=at))
    return conds


def parse_reward(expr, symbols):
    name, args = parse_call(expr)
    if name != 'Reward':
        return {}
    out = {}
    for a in args:
        m = re.match(r'(\w+)\s*:\s*(.+)$', a.strip(), re.S)
        if not m:
            continue
        k, v = m.group(1), m.group(2).strip()
        if k == 'crystals':
            out['crystals'] = as_int(v, symbols)
        elif k in ('equipmentId', 'unlocksMapId'):
            out[k] = as_string(v, symbols)
        else:
            out[k] = 'true' in v.lower()
    return out


def parse_file(path):
    src = strip_comments(open(path, encoding='utf8').read())
    cls = os.path.splitext(os.path.basename(path))[0]

    symbols = dict(BUILTIN)
    symbols.update(FEATS)
    for m in re.finditer(r'const\s+string\s+(\w+)\s*=\s*"((?:[^"\\]|\\.)*)"', src):
        symbols[m.group(1)] = unescape(m.group(2))
        symbols["%s.%s" % (cls, m.group(1))] = unescape(m.group(2))
    for m in re.finditer(r'const\s+int\s+(\w+)\s*=\s*(-?\d+)', src):
        symbols[m.group(1)] = int(m.group(2))
    symbols['ExploreStat'] = COMPUTED.get('%s.ExploreStat' % cls, 'ExploreStat')
    symbols['FinaleTarget()'] = COMPUTED.get('%s.FinaleTarget()' % cls, '?')

    quests = []
    for m in re.finditer(r'new\s+Quest\s*\(', src):
        start = src.index('(', m.start())
        body = src[start + 1:balanced(src, start) - 1]
        args = {}
        for arg in split_top_level(body):
            am = re.match(r'(\w+)\s*:\s*(.+)$', arg.strip(), re.S)
            if am:
                args[am.group(1)] = am.group(2).strip()
        quests.append(dict(
            cls=cls,
            id=as_string(args.get('id', '""'), symbols),
            name=as_string(args.get('name', '""'), symbols),
            description=as_string(args.get('description', '""'), symbols),
            mapId=as_string(args.get('mapId', '""'), symbols),
            objectives=parse_objectives(args.get('objectives', ''), symbols),
            gate=parse_gate(args.get('unlocks', ''), symbols),
            unlockMode='Any' if 'Any' in args.get('unlockMode', '') else 'All',
            reward=parse_reward(args.get('reward', ''), symbols),
        ))
    return quests


# ---------------------------------------------------------------- rendering

def render_gate(q, by_id):
    if not q['gate']:
        return "*(available from a new game)*"
    parts = []
    for c in q['gate']:
        if c['kind'] == 'after':
            other = by_id.get(c['quest'])
            parts.append("complete **%s**" % (other['name'] if other else c['quest']))
        else:
            parts.append("`%s` reaches %s" % (c['key'], c['at']))
    joiner = " **or** " if q['unlockMode'] == 'Any' else " and "
    return joiner.join(parts)


def render_condition(q):
    if not q['gate']:
        return "—"
    return "<br>".join(
        ("`%s` >= 1  *(completion of %s)*" % ("quests.%s.completed" % c['quest'], c['quest']))
        if c['kind'] == 'after' else
        ("`%s` >= %s" % (c['key'], c['at']))
        for c in q['gate'])


def render_objectives(q):
    rows = []
    for o in q['objectives']:
        label = o['label'] or "*(stat's own label)*"
        row = "`%s` >= %s — \"%s\"" % (o['stat'], o['target'], label)
        if o['hide']:
            row += "  *(progress hidden)*"
        rows.append(row)
    if not rows:
        return "—"
    if len(rows) > 1:
        return "<br>".join("%d. %s" % (i + 1, r) for i, r in enumerate(rows)) + \
               "<br>*(all must be met together)*"
    return rows[0]


def render_reward(q):
    r = q['reward']
    parts = []
    if r.get('crystals'):
        parts.append("%s crystal%s" % (r['crystals'], "" if r['crystals'] == 1 else "s"))
    if r.get('equipmentId'):
        parts.append("equipment `%s`" % r['equipmentId'])
    if r.get('extraSlot'):
        parts.append("**another equipment slot** (both knights)")
    if r.get('extraSpecialSlot'):
        parts.append("**another special slot** (both knights)")
    if r.get('unlocksMapId'):
        parts.append("unlocks map `%s`" % r['unlocksMapId'])
    return ", ".join(parts) if parts else "—"


def blockquote(text, width=94):
    words, lines, cur = text.split(), [], ""
    for w in words:
        if cur and len(cur) + 1 + len(w) > width:
            lines.append(cur)
            cur = w
        else:
            cur = (cur + " " + w).strip()
    if cur:
        lines.append(cur)
    return "\n".join("> " + ln for ln in lines) if lines else ">"


def main():
    if not os.path.isdir(LINES_DIR):
        sys.exit("run me from the repo root (missing %s)" % LINES_DIR)

    all_quests = []
    for cls in NPC_BY_CLASS:
        path = os.path.join(LINES_DIR, cls + ".cs")
        if not os.path.exists(path):
            sys.exit("missing line file: %s" % path)
        all_quests.extend(parse_file(path))

    by_id = {q['id']: q for q in all_quests}

    # Reverse lookup: which quests does finishing this one open?
    opens = {q['id']: [] for q in all_quests}
    for q in all_quests:
        for c in q['gate']:
            if c['kind'] == 'after' and c['quest'] in opens:
                opens[c['quest']].append(q['id'])

    out = []
    w = out.append

    w("# Two Knights — Quest Script")
    w("")
    w("**This is a fill-in document.** Generated by `Tools/gen_quest_script.py` from the quest")
    w("line files under `Assets/Scripts/Quests/Lines/`. Every field is prefilled with its")
    w("current value except the three you are writing:")
    w("")
    w("- **OFFER** — what the NPC says when the quest first appears. Currently blank.")
    w("- **IN PROGRESS** — the quest log body. Prefilled with the existing text; edit freely.")
    w("- **COMPLETED** — what the NPC says when you finish it. Currently blank.")
    w("- **Tutorial run** — `yes` or `no`. See below.")
    w("")
    w("Nothing is implemented from this document until you hand it back.")
    w("")
    w("## The tutorial-run rule")
    w("")
    w("During the player's tutorial run, a quest marked **`no`** makes **no progress at all** —")
    w("its stats still accumulate, but the quest neither advances nor unlocks nor completes.")
    w("A quest marked **`yes`** behaves normally.")
    w("")
    w("Default below is `no` for everything. Flip the starting quests you want live during")
    w("that first run to `yes` — you mentioned the Rat King quest and the line running on to")
    w("the Crimson Twins.")
    w("")
    w("## House style, for reference")
    w("")
    w("- Descriptions are **paragraphs**, 3-5 sentences, plain and grounded. Terse portentous")
    w("  one-liners were tried and rejected.")
    w("- **Quest prose carries no numbers.** Numbers live in the objective line.")
    w("- **Never print world units** — no `2.3u`. Percentages if a size must be described.")
    w("- Recurring threads: the quartermaster and his tally board, the scouts whose maps")
    w("  disagree, the Orders finding you rather than the reverse.")
    w("")
    w("> **Two targets are computed at runtime, not authored.** The explorer finales")
    w("> (`forest_explorer_2` = 45, `mine_explorer_2` = 42) count every playable wave on")
    w("> their map, so both numbers move whenever waves are added. They are shown here as")
    w("> they stood when this was generated.")
    w("")
    w("## The cast")
    w("")
    w("| NPC | Carries | Entrance | Exit |")
    w("|---|---|---|---|")
    w("| **Cartographer** | The Camp Fields, The Mine | walks in, freezes on walk frame 1, "
      "then loops: open map / hold 3s / close map / hold 5s | walks downward out of view |")
    w("| **Ninja** | Serpent, Shadow | dark grey smoke puff, ninja already looping behind it "
      "| the same smoke puff swallows him |")
    w("| **Wizard** | Ember, Frigid | walks down into view trailing fire or frost | a fire or "
      "frost veil covers and hides him |")
    w("| **Paladin** | Guardian, Dawn | glow fades in at full, then the paladin fades in with "
      "orbiting orbs | paladin and orbs fade out, glow follows 1s later |")
    w("")
    w("---")
    w("")

    # Table of contents
    w("## Contents")
    w("")
    seen_npc = None
    for cls, (npc, line) in NPC_BY_CLASS.items():
        qs = [q for q in all_quests if q['cls'] == cls]
        if npc != seen_npc:
            w("")
            w("**%s**" % npc.split(' (')[0])
            seen_npc = npc
        w("- %s (%d)" % (line, len(qs)))
    w("")
    w("**Combination upgrades** (2) — new quests, written from scratch")
    w("")
    w("---")
    w("")

    n_written = 0
    for cls, (npc, line) in NPC_BY_CLASS.items():
        qs = [q for q in all_quests if q['cls'] == cls]
        w("# %s — %s" % (npc, line))
        w("")
        w("*%d quests.*" % len(qs))
        w("")
        for q in qs:
            n_written += 1
            w("## %s" % q['name'])
            w("")
            w("| | |")
            w("|---|---|")
            w("| **Id** | `%s` |" % q['id'])
            w("| **NPC** | %s |" % npc)
            w("| **Group** | %s |" % MAP_NAMES.get(q['mapId'], q['mapId']))
            w("| **Unlocked by** | %s |" % render_gate(q, by_id))
            w("| **Unlock condition** | %s |" % render_condition(q))
            w("| **Unlocks** | %s |" % (
                ", ".join("**%s**" % by_id[o]['name'] for o in opens[q['id']])
                if opens[q['id']] else "—"))
            w("| **Complete condition** | %s |" % render_objectives(q))
            w("| **Reward** | %s |" % render_reward(q))
            w("| **Tutorial run** | `no`   ← `yes` / `no` |")
            w("")
            w("**OFFER** — the NPC says this when the quest appears")
            w("")
            w(">")
            w("")
            w("**IN PROGRESS** — the quest log body *(current text; edit freely)*")
            w("")
            w(blockquote(q['description']))
            w("")
            w("**COMPLETED** — the NPC says this when you finish it")
            w("")
            w(">")
            w("")
        w("---")
        w("")

    # Combination upgrade quests
    w("# Combination upgrades — new quests")
    w("")
    w("These two do not exist yet. They are the only cross-order combination upgrades in the")
    w("game today, found by walking the actual unlock graph in `Assets/Upgrades/`:")
    w("**Sleeping Dart** (`secondOrder`) and **Fire Sight** (Ember order count + a Guardian")
    w("prerequisite). *Guided Reflections* is excluded — both its parents are Guardian, so it")
    w("is a chain-meets-chain combo inside one Order rather than a two-Order one.")
    w("")
    w("Each combination upgrade **stays out of the draft entirely** until its quest unlocks.")
    w("The quest opens the moment one knight first qualifies on both Orders; the player then")
    w("sees the upgrade appear, takes it, and the quest completes.")
    w("")
    for c in COMBO_QUESTS:
        n_written += 1
        w("## %s" % c['name'])
        w("")
        w("| | |")
        w("|---|---|")
        w("| **Id** | `%s` |" % c['id'])
        w("| **Name** | *(write me)* |")
        w("| **NPC** | %s |" % c['npc'])
        w("| **Reveals** | %s — %s |" % (c['upgrade'], c['orders']))
        w("| **Unlocked by** | %s |" % "<br>".join(c['gate']))
        w("| **Unlocks** | — |")
        w("| **Complete condition** | %s |" % c['objective'])
        w("| **Reward** | %s |" % c['reward'])
        w("| **Tutorial run** | `no` |")
        w("")
        w("**OFFER** — the NPC says this when the quest appears")
        w("")
        w(">")
        w("")
        w("**IN PROGRESS** — the quest log body")
        w("")
        w(">")
        w("")
        w("**COMPLETED** — the NPC says this when you finish it")
        w("")
        w(">")
        w("")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf8', newline='\n') as fh:
        fh.write("\n".join(out).rstrip() + "\n")

    print("wrote %s" % OUT)
    print("%d quests (%d existing + %d combination)"
          % (n_written, len(all_quests), len(COMBO_QUESTS)))

    # Loud warnings rather than a silently wrong document.
    bad = [q['id'] for q in all_quests if '{' in q['description'] or not q['objectives']]
    for q in all_quests:
        for o in q['objectives']:
            if '{' in str(o['stat']) or '{' in str(o['target']):
                bad.append(q['id'])
    if bad:
        print("UNRESOLVED SYMBOLS in: %s" % ", ".join(sorted(set(bad))))
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
