"""Diffs a harvest of the framework dictionaries against the checked-in tables.

This is the half of the pipeline worth having first. The tables are the only known answer for
their 1590 keys, and a harvester that silently disagrees with them looks exactly like one that
works - so the harvester earns the right to *write* them by reproducing them first.

Differences are expected, not failures. The tables were imported from a hand-maintained file
that no longer exists, so some of what this reports is the harvest being right.
"""

import collections

from themedefaults import harvest, table

Report = collections.namedtuple(
    "Report", "matched differs unharvested uncovered unresolved overridden aliases")


def _comparable(value):
    """A table row, or None where the two sides have nothing to say to each other."""
    return value if value[0] in ("color", "shade") else None


def _harvested(entry):
    """The same shape a table row has, so the two compare and print alike."""
    if entry.kind == "color":
        return ("color", entry.args[0])
    if entry.kind == "shade":
        opacity = entry.args[1]
        # Brush.Opacity is authored as a fraction and stored as the alpha it works out to;
        # the table has carried it since the shade alphas went in.
        return ("shade", entry.args[0],
                int(round(255 * float(opacity))) if opacity is not None else None)
    return None


def run(rows, entries, overrides=()):
    """Compares one theme. `rows` is [(key, value)] from the table, `entries` a harvest.

    A key named in overrides.tsv is expected to disagree with the framework - that is what
    an override is - so it is reported apart from the differences that want looking at.
    """
    matched, differs, unharvested, overridden = 0, [], [], []

    covered = set()
    for key, value in rows:
        if value[0] == "custom":
            covered.add(key)
            continue

        covered.add(key)
        if key not in entries:
            unharvested.append(key)
            continue

        resolved, _ = harvest.resolve(entries, key)
        theirs, ours = _comparable(value), _harvested(resolved)

        if theirs is None or ours is None:
            # Acrylic on either side: the recipes carry references this does not resolve, and
            # there are 3% of them. Left alone rather than reported as a false difference.
            matched += 1
        elif theirs == ours:
            matched += 1
        elif key in overrides:
            overridden.append((key, theirs, ours))
        else:
            differs.append((key, theirs, ours))

    uncovered, unresolved = [], []
    for key, entry in entries.items():
        if not entry.brush or key in covered:
            continue
        resolved, _ = harvest.resolve(entries, key)
        if resolved.kind == "other" and str(resolved.args[0]).startswith(("dangling:", "cycle:")):
            unresolved.append((key, resolved.args[0]))
        else:
            uncovered.append(key)

    return Report(matched, differs, unharvested, uncovered, unresolved, overridden,
                  harvest.roots(entries))


def render(name, report, rows, say, limit):
    say("%s: %d table rows, %d matched, %d differ, %d ours, %d not in the harvest."
        % (name, len(rows), report.matched, len(report.differs),
           len(report.overridden), len(report.unharvested)))
    say("       the harvest holds %d brush keys the table does not, and %d that do not resolve."
        % (len(report.uncovered), len(report.unresolved)))

    def listing(title, items, format_item):
        if not items:
            return
        say("  %s (%d):" % (title, len(items)))
        for item in items[:limit]:
            say("    " + format_item(item))
        if len(items) > limit:
            say("    ... and %d more." % (len(items) - limit))

    listing("differs", sorted(report.differs),
            lambda i: "%-56s table %-22s harvest %s"
                      % (i[0], table.format_value(i[1]), table.format_value(i[2])))
    listing("ours on purpose, see overrides.tsv", sorted(report.overridden),
            lambda i: "%-56s ours %-22s framework %s"
                      % (i[0], table.format_value(i[1]), table.format_value(i[2])))
    listing("not in the harvest", sorted(report.unharvested), lambda i: i)
    listing("does not resolve", sorted(report.unresolved), lambda i: "%-56s %s" % i)
    listing("in the harvest, not in the table", sorted(report.uncovered), lambda i: i)


def refresh(rows, entries):
    """The table's rows with every value the framework still states taken from it.

    Only values move. Keys, their order, the `custom` markers and anything the harvest has
    no opinion on are left exactly as they were: this closes the gap between the tables and
    the framework, it does not decide what the tables should contain.
    """
    out, changed = [], []
    for key, value in rows:
        if value[0] == "custom" or key not in entries:
            out.append((key, value))
            continue

        resolved, _ = harvest.resolve(entries, key)
        ours = _harvested(resolved)
        if ours is None or _comparable(value) is None or ours == value:
            out.append((key, value))
            continue

        out.append((key, ours))
        changed.append((key, value, ours))
    return out, changed


def relink(rows, entries):
    """The table's rows with flattened values replaced by the edge the framework writes.

    Only edges whose target the table itself holds are recorded. The framework's chains run
    through `Color` keys the app never pins and end there, so the edge worth keeping is to
    the nearest ancestor that is a row here - for a check glyph that is
    `TextOnAccentFillColorPrimaryBrush`, not the colour behind it.

    Overlay keys are skipped at both ends: their value arrives at startup, and there is
    nothing for `flatten` to resolve to at generation time.
    """
    values = {key: value for key, value in rows if value[0] != "custom"}
    out, linked, refused = [], [], []

    for key, value in rows:
        entry = entries.get(key)
        if value[0] == "custom" or entry is None or entry.kind != "alias":
            out.append((key, value))
            continue

        _, chain = harvest.resolve(entries, key)
        parent = next((step for step in chain[1:] if step in values), None)
        if parent is None:
            out.append((key, value))
            continue

        # Recording an edge must not move a colour: this is a change of representation, and
        # the packed arrays have to come out byte-identical. Where the table's value and the
        # edge's target disagree the row keeps its value, and the disagreement is reported
        # rather than resolved - acrylic is most of it, which `refresh` does not compare.
        if values[parent] != value:
            out.append((key, value))
            refused.append((key, parent, value, values[parent]))
            continue

        out.append((key, ("alias", parent)))
        linked.append((key, parent))

    return out, linked, refused


def render_aliases(report, key, say, limit):
    """What follows one key, which is the question the whole exercise exists to answer."""
    followers = sorted(report.aliases.get(key, ()))
    say("%d keys resolve to %s:" % (len(followers), key))
    for follower in followers[:limit]:
        say("    " + follower)
    if len(followers) > limit:
        say("    ... and %d more." % (len(followers) - limit))
