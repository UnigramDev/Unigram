"""Sends the FormattedTextBlock test messages described in formatted-text-block-test-plan.md.

    python formatted-text-block-test-messages.py <bot-token> <chat-id>

Run it immediately before testing rather than once and for all: the relative dates are anchored
a few seconds old so they tick while you are looking at them, and a set sent yesterday changes
width once a day.

Entities are given explicitly instead of through markdown because the interesting cases -
a date inside a spoiler, two dates in front of one spoiler - have no markdown spelling.

The date entity is Bot API `date_time`:

    {"type": "date_time", "offset": N, "length": L, "unix_time": <int>, "date_time_format": "r"}

`date_time_format` follows the grammar in TdExtensions.cs: `r` on its own is relative, or `w`
(weekday) with `d`/`D` (short/long date) and `t`/`T` (short/long time). An absent or empty
format leaves the entity with no formatting type, and the app then renders the source text
as-is - which is a useful negative case in itself (T14).
"""

import json
import sys
import time
import urllib.error
import urllib.request


def u16(text):
    """Length in UTF-16 code units, which is what Bot API offsets count."""
    return len(text.encode('utf-16-le')) // 2


def at(text, sub, **kwargs):
    """An entity over the first occurrence of `sub`, offsets in UTF-16 units."""
    index = text.index(sub)
    entity = {'offset': u16(text[:index]), 'length': u16(sub)}
    entity.update(kwargs)
    return entity


# Every date is anchored a few seconds old, because the relative formatter counts seconds
# (Formatter.RelativeDateAgo ends in Declension(SecondsAgo, value)). The text therefore
# re-renders every second, and changes WIDTH at each of these:
#
#     1 -> 2s     "1 second ago" (12)   -> "2 seconds ago" (13)    +1
#     9 -> 10s    "9 seconds ago" (13)  -> "10 seconds ago" (14)   +1
#     59 -> 60s   "59 seconds ago" (14) -> "a minute ago" (12)     -2
#     2 min       "a minute ago" (12)   -> "2 minutes ago" (13)    +1
#     10 min      "9 minutes ago" (13)  -> "10 minutes ago" (14)   +1
#     1 hour      "59 minutes ago" (14) -> "an hour ago" (11)      -3
#
# Only a width change moves anything, so the default of 8 seconds gives three of them inside
# two minutes - at +2s, +52s and +112s - with the counter visibly ticking in between.
SECONDS, TEN_MINUTES, HOUR = 8, 590, 3590


def date(text, sub, seconds_ago=SECONDS, fmt='r'):
    """A date entity anchored `seconds_ago` in the past."""
    return at(text, sub, type='date_time', unix_time=int(time.time()) - seconds_ago,
              date_time_format=fmt)


def messages():
    """(label, text, entities) for each case in the test plan."""
    out = []

    # T1 - a single plain paragraph between two typed ones. The block rendering it takes the
    # fast path, which is where the index map used to be dropped.
    text = ('[T1] select the middle line, copy it, and check what you get\n'
            'var before = 1;\n'
            'the middle line, plain text\n'
            'var after = 2;')
    out.append(('T1', text, [
        at(text, 'var before = 1;', type='pre', language='csharp'),
        at(text, 'var after = 2;', type='pre', language='csharp'),
    ]))

    # T2 - same shape with a quote leading, since quotes are split out the same way.
    text = ('[T2] same, with a quote first\n'
            'quoted opening line\n'
            'the middle line, plain text\n'
            'var after = 2;')
    out.append(('T2', text, [
        at(text, 'quoted opening line', type='blockquote'),
        at(text, 'var after = 2;', type='pre', language='csharp'),
    ]))

    # T3 - the monospace chain, including the emoji that only renders since the six other
    # call sites gained the font fallback tail.
    text = '[T3] inline code span and code with an emoji 🙂 inside'
    out.append(('T3', text, [
        at(text, 'code span', type='code'),
        at(text, 'code with an emoji 🙂 inside', type='code'),
    ]))

    # T4 - syntax highlighting, for the colour tables and the theme switch.
    text = ('[T4] switch light/dark with this on screen\n'
            'def greet(name):\n'
            '    return f"hello {name}"  # comment\n')
    out.append(('T4', text, [
        at(text, 'def greet(name):', type='pre', language='python'),
    ]))

    # T5 - every run flavour at once, to be scrolled in and out of the recycling pool.
    text = '[T5] bold italic strike mono and spoiler all in one line'
    out.append(('T5', text, [
        at(text, 'bold', type='bold'),
        at(text, 'italic', type='italic'),
        at(text, 'strike', type='strikethrough'),
        at(text, 'mono', type='code'),
        at(text, 'spoiler', type='spoiler'),
    ]))

    # T6 - an expandable quote past three lines, for the measure memo.
    text = ('[T6] resize the window with this collapsed\n'
            'line one of a long quotation that should wrap around\n'
            'line two of a long quotation that should wrap around\n'
            'line three of a long quotation that should wrap around\n'
            'line four of a long quotation that should wrap around')
    quote = ('line one of a long quotation that should wrap around\n'
             'line two of a long quotation that should wrap around\n'
             'line three of a long quotation that should wrap around\n'
             'line four of a long quotation that should wrap around')
    out.append(('T6', text, [
        at(text, quote, type='expandable_blockquote'),
    ]))

    # T7 - a spoiler on its own, for the cover on first render.
    text = '[T7] open the chat fresh and watch this: hidden until tapped'
    out.append(('T7', text, [
        at(text, 'hidden until tapped', type='spoiler'),
    ]))

    # T8 - a link, for the I-beam going back to text after the hand.
    text = '[T8] move the pointer across this link and back onto the text'
    out.append(('T8', text, [
        at(text, 'this link', type='text_link', url='https://telegram.org'),
    ]))

    # T9 - a date in FRONT of a spoiler. The date is one character of source text and about
    # fourteen of displayed text, so the shift is impossible to miss.
    text = '[T9] meeting X and the answer is 42'
    out.append(('T9', text, [
        date(text, 'X'),
        at(text, '42', type='spoiler'),
    ]))

    # T10 - a spoiler that spans a date. The server does not allow the overlap and splits the
    # spoiler around it, which is the more interesting case anyway: one cover in front of the
    # date that must not move when it reformats, and one behind it that must.
    text = '[T10] the answer is A X B and that is all'
    out.append(('T10', text, [
        at(text, 'A X B', type='spoiler'),
        date(text, 'X'),
    ]))

    # T11 - two dates in front of one spoiler, the case a single per-date delta cannot express.
    # Both dates cross at the same moment, by different amounts (+4 and +1), which is the
    # case a single delta per date could not represent: the spoiler has to move by five.
    text = '[T11] from X until Y the answer is 42'
    out.append(('T11', text, [
        date(text, 'X'),
        date(text, 'Y', TEN_MINUTES),
        at(text, '42', type='spoiler'),
    ]))

    # T12 - no spoiler at all: this one is about copy after the date reformats.
    text = '[T12] X and then the last word'
    out.append(('T12', text, [
        date(text, 'X', HOUR),
    ]))

    # T13 - a spoiler starting exactly where the date ends, with no gap. That is the boundary
    # the shift is written against: a range at the date's end moves, one that ends there
    # does not.
    text = '[T13] Xhidden and the rest of the line'
    out.append(('T13', text, [
        date(text, 'X'),
        at(text, 'hidden', type='spoiler'),
    ]))

    # T14 - a date entity with no formatting type. Renders its source text and never updates,
    # so nothing here should ever move.
    text = '[T14] this date has no format and must render as written: 2026-01-01'
    out.append(('T14', text, [
        at(text, '2026-01-01', type='date_time', unix_time=1767225600, date_time_format=''),
    ]))

    return out


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    token, chat = sys.argv[1], sys.argv[2]
    url = 'https://api.telegram.org/bot' + token + '/sendMessage'

    for label, text, entities in messages():
        payload = json.dumps({'chat_id': chat, 'text': text, 'entities': entities},
                             ensure_ascii=False).encode('utf-8')
        request = urllib.request.Request(url, data=payload,
                                         headers={'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                result = json.loads(response.read().decode('utf-8'))
            print('%-4s sent as %s' % (label, result['result']['message_id']))
        except urllib.error.HTTPError as error:
            body = json.loads(error.read().decode('utf-8'))
            print('%-4s FAILED: %s' % (label, body.get('description')))

    return 0


if __name__ == '__main__':
    sys.exit(main())
