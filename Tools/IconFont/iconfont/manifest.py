"""The manifest is the font's source of truth: which glyph sits at which
codepoint, and where its artwork comes from."""

import json
import os
from collections import OrderedDict

# Codepoints are load-bearing: 763 raw &#xE9F1;-style literals across 211 XAML
# files reference them directly, so an assignment can be appended to but never
# reshuffled.
PRIVATE_USE = range(0xE000, 0xF900)


class Icon:
    __slots__ = ("name", "code", "src", "advance", "note", "blank")

    def __init__(self, name, code, src, advance=None, note=None, blank=False):
        self.name = name
        self.code = code
        self.src = src
        # Natural advance is the source's viewBox aspect scaled to the em; an
        # explicit value overrides it, which is how the non-square glyphs
        # (Scam at 45x20, Clock12px at 18x12) keep their metrics.
        self.advance = advance
        self.note = note
        # A glyph that is deliberately empty: it occupies a codepoint and an
        # advance but paints nothing. Declared so that artwork which fails to
        # import cannot quietly become one.
        self.blank = blank

    @property
    def is_alias(self):
        """A codepoint that carries another codepoint's glyph.

        The font has always held some drawings twice - the checkmark sits at four
        codepoints - because the app reaches them through different names. An
        alias records that instead of storing the outline again, and lets the
        build point both codepoints at one glyph.
        """
        return self.src.startswith("alias:")

    @property
    def alias_code(self):
        return int(self.src.split(":", 1)[1], 16)

    @property
    def is_remote(self):
        return (":" in self.src and not self.is_alias
                and not os.path.isabs(self.src))

    @property
    def source_kind(self):
        return self.src.split(":", 1)[0] if self.is_remote else "local"

    @property
    def source_id(self):
        return self.src.split(":", 1)[1] if self.is_remote else self.src

    def to_json(self):
        d = OrderedDict()
        d["name"] = self.name
        d["code"] = "%04X" % self.code
        d["src"] = self.src
        if self.advance is not None:
            d["advance"] = self.advance
        if self.blank:
            d["blank"] = True
        if self.note:
            d["note"] = self.note
        return d


class Manifest:
    def __init__(self, path, font, sources, icons):
        self.path = path
        self.font = font
        self.sources = sources
        self.icons = icons

    @property
    def root(self):
        return os.path.dirname(os.path.abspath(self.path))

    def by_code(self):
        return {i.code: i for i in self.icons}

    def resolve(self, icon):
        """The icon that actually holds the artwork, following an alias."""
        seen = set()
        by_code = self.by_code()
        while icon is not None and icon.is_alias and icon.code not in seen:
            seen.add(icon.code)
            icon = by_code.get(icon.alias_code)
        return icon

    def by_name(self):
        return {i.name: i for i in self.icons}

    @classmethod
    def load(cls, path):
        with open(path, "r", encoding="utf-8") as fp:
            raw = json.load(fp, object_pairs_hook=OrderedDict)
        icons = [
            Icon(
                name=e["name"],
                code=int(e["code"], 16),
                src=e["src"],
                advance=e.get("advance"),
                note=e.get("note"),
                blank=bool(e.get("blank")),
            )
            for e in raw["icons"]
        ]
        return cls(path, raw["font"], raw.get("sources", {}), icons)

    def save(self, path=None):
        path = path or self.path
        icons = sorted(self.icons, key=lambda i: (i.code, i.name))
        # Hand-written by a human as often as by this tool, so it is formatted
        # one icon per line: a codepoint reassignment has to be obvious in a diff.
        lines = ["{"]
        lines.append('  "font": %s,' % json.dumps(self.font, indent=None))
        lines.append('  "sources": {')
        keys = list(self.sources)
        for n, key in enumerate(keys):
            comma = "," if n < len(keys) - 1 else ""
            lines.append('    "%s": %s%s' % (key, json.dumps(self.sources[key]), comma))
        lines.append("  },")
        lines.append('  "icons": [')
        for n, icon in enumerate(icons):
            comma = "," if n < len(icons) - 1 else ""
            lines.append("    %s%s" % (json.dumps(icon.to_json()), comma))
        lines.append("  ]")
        lines.append("}")
        with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
            fp.write("\n".join(lines) + "\n")

    def validate(self):
        problems = []
        seen_code = {}
        seen_name = {}
        for icon in self.icons:
            if icon.code in seen_code:
                problems.append(
                    "codepoint U+%04X claimed by both %s and %s"
                    % (icon.code, seen_code[icon.code], icon.name)
                )
            seen_code[icon.code] = icon.name
            if icon.name in seen_name:
                problems.append("duplicate icon name %s" % icon.name)
            seen_name[icon.name] = icon
            if icon.is_remote and icon.source_kind not in self.sources:
                problems.append(
                    "%s refers to unknown source %r" % (icon.name, icon.source_kind)
                )
        by_code = {i.code: i for i in self.icons}
        for icon in self.icons:
            if not icon.is_alias:
                continue
            target = by_code.get(icon.alias_code)
            if target is None:
                problems.append("%s aliases U+%04X, which is not in the font"
                                % (icon.name, icon.alias_code))
            elif target.is_alias:
                problems.append("%s aliases U+%04X, which is itself an alias"
                                % (icon.name, icon.alias_code))
        return problems

    def notes(self):
        """Oddities worth reporting that do not stop the font being built."""
        notes = []
        for icon in self.icons:
            if icon.code not in PRIVATE_USE:
                notes.append(
                    "%s sits at U+%04X, outside the private use area - IcoMoon parked "
                    "unassigned icons there and nothing references it"
                    % (icon.name, icon.code)
                )
        return notes
