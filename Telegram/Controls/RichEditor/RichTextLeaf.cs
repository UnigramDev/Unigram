//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Native;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    [Flags]
    public enum RichMarks : ushort
    {
        None = 0,
        Bold = 1 << 0,
        Italic = 1 << 1,
        Underline = 1 << 2,
        Strikethrough = 1 << 3,
        Spoiler = 1 << 4,
        Monospace = 1 << 5,
        Marked = 1 << 6,
        Subscript = 1 << 7,
        Superscript = 1 << 8,
    }

    public enum RichLinkKind
    {
        Url,
        EmailAddress,
        PhoneNumber,
        MentionName,
        DateTime,
    }

    /// <summary>
    /// A payload mark: at most one per character, unlike <see cref="RichMarks"/>. Immutable, so
    /// runs and undo snapshots share instances.
    /// </summary>
    public sealed class RichLink : IEquatable<RichLink>
    {
        public RichLinkKind Kind { get; }
        public string Url { get; }
        public long UserId { get; }
        public int UnixTime { get; }
        public DateTimeFormattingType FormattingType { get; }

        private RichLink(RichLinkKind kind, string url, long userId, int unixTime, DateTimeFormattingType formattingType)
        {
            Kind = kind;
            Url = url ?? string.Empty;
            UserId = userId;
            UnixTime = unixTime;
            FormattingType = formattingType;
        }

        public static RichLink FromUrl(string url) => new(RichLinkKind.Url, url, 0, 0, null);
        public static RichLink FromEmailAddress(string address) => new(RichLinkKind.EmailAddress, address, 0, 0, null);
        public static RichLink FromPhoneNumber(string number) => new(RichLinkKind.PhoneNumber, number, 0, 0, null);
        public static RichLink FromMentionName(long userId) => new(RichLinkKind.MentionName, null, userId, 0, null);
        public static RichLink FromDateTime(int unixTime, DateTimeFormattingType formattingType) => new(RichLinkKind.DateTime, null, 0, unixTime, formattingType);

        public bool Equals(RichLink other)
        {
            if (other is null)
            {
                return false;
            }

            // FormattingType is compared by reference: it is only ever carried across from the
            // source, never built per character, so equal runs share it.
            return ReferenceEquals(this, other)
                || (Kind == other.Kind && Url == other.Url && UserId == other.UserId && UnixTime == other.UnixTime && ReferenceEquals(FormattingType, other.FormattingType));
        }

        public override bool Equals(object obj) => Equals(obj as RichLink);

        public override int GetHashCode() => HashCode.Combine(Kind, Url, UserId, UnixTime);

        public static bool AreEqual(RichLink x, RichLink y) => x is null ? y is null : x.Equals(y);
    }

    public enum RichInlineKind
    {
        CustomEmoji,
        Math,
        Button,
        Icon,
        Anchor,
    }

    /// <summary>
    /// Something in a line of text that is not text: one <see cref="RichTextLeaf.ObjectChar"/>
    /// in the leaf, so a caret steps over it once and an IME sees one character. Immutable.
    /// </summary>
    public sealed class RichInlineObject
    {
        public RichInlineKind Kind { get; }

        public long CustomEmojiId { get; }

        /// <summary>The custom emoji's alternative text, the formula's LaTeX, or the anchor's name.</summary>
        public string Text { get; }

        public InlineButton Button { get; }

        /// <summary>What an object the editor cannot author was read from, written back unchanged.</summary>
        public RichText Original { get; }

        private RichInlineObject(RichInlineKind kind, long customEmojiId, string text, InlineButton button, RichText original)
        {
            Kind = kind;
            CustomEmojiId = customEmojiId;
            Text = text ?? string.Empty;
            Button = button;
            Original = original;
        }

        public static RichInlineObject FromCustomEmoji(long customEmojiId, string alternativeText) => new(RichInlineKind.CustomEmoji, customEmojiId, alternativeText, null, null);
        public static RichInlineObject FromMath(string expression) => new(RichInlineKind.Math, 0, expression, null, null);
        public static RichInlineObject FromButton(InlineButton button) => new(RichInlineKind.Button, 0, null, button, null);
        public static RichInlineObject FromIcon(RichTextIcon icon) => new(RichInlineKind.Icon, 0, null, null, icon);
        public static RichInlineObject FromAnchor(string name) => new(RichInlineKind.Anchor, 0, name, null, null);

        /// <summary>What the object reads as in plain text: copy, the UIA text, the length limit.</summary>
        public string PlainText => Kind switch
        {
            RichInlineKind.Button => Button?.Text is RichText text ? RichDocumentConverter.GetPlainText(text) : string.Empty,
            RichInlineKind.Icon or RichInlineKind.Anchor => string.Empty,
            _ => Text
        };
    }

    public struct RichTextRun
    {
        public int Length;
        public RichMarks Marks;
        public RichLink Link;

        /// <summary>Set only on a run of length 1 over an <see cref="RichTextLeaf.ObjectChar"/>.</summary>
        public RichInlineObject Object;

        /// <summary>
        /// The text this run was put in for, as an emoticon or a suggestion replaces what was
        /// typed: deleting the run gives it back. Never merged with a neighbour, which would
        /// make the neighbour part of what is given back.
        /// </summary>
        public string Replaced;

        public RichTextRun(int length, RichMarks marks, RichLink link, RichInlineObject inline = null, string replaced = null)
        {
            Length = length;
            Marks = marks;
            Link = link;
            Object = inline;
            Replaced = replaced;
        }

        public readonly bool HasSameStyle(in RichTextRun other)
        {
            return Object == null && other.Object == null && Replaced == null && other.Replaced == null && Marks == other.Marks && RichLink.AreEqual(Link, other.Link);
        }
    }

    public enum RichLeafRole
    {
        Text,
        Credit,
        Caption,
        Header,
        Cell,
    }

    /// <summary>
    /// A place a caret can be: a string and the runs that style it, covering it exactly and
    /// merged wherever two neighbours look the same.
    /// </summary>
    public sealed class RichTextLeaf
    {
        public const char ObjectChar = '￼';

        private const string ObjectString = "￼";

        // The text, edited in place: a string per edit was a copy of the whole line on every key,
        // on the large object heap past 42,500 characters, and the full collections that brings.
        // Shared with a clone - every undo step is one - until either of them writes.
        private char[] _chars;
        private int _length;
        private bool _shared;

        // The text as a string, made when it is asked for and kept until the next edit.
        private string _string;

        private readonly List<RichTextRun> _runs;

        public RichTextLeaf(RichLeafRole role = RichLeafRole.Text, bool isPlain = false)
        {
            Role = role;
            IsPlain = isPlain;
            _chars = Array.Empty<char>();
            _string = string.Empty;
            _runs = new List<RichTextRun>();
        }

        private RichTextLeaf(RichTextLeaf source)
        {
            Role = source.Role;
            IsPlain = source.IsPlain;
            _chars = source._chars;
            _length = source._length;
            _string = source._string;
            _shared = source._shared = true;
            _runs = new List<RichTextRun>(source._runs);
        }

        public RichLeafRole Role { get; }

        /// <summary>No marks and no objects: code blocks, where the text is the content.</summary>
        public bool IsPlain { get; private set; }

        /// <summary>The block this leaf belongs to. Set by the document when it indexes itself.</summary>
        public RichBlock Owner { get; internal set; }

        /// <summary>Position in <see cref="RichDocument.Leaves"/>; valid until the next structural change.</summary>
        public int Index { get; internal set; }

        /// <summary>Position in <see cref="RichDocument.Stops"/>; valid until the next structural change.</summary>
        public int StopIndex { get; internal set; }

        /// <summary>Bumped on every change, so a presenter can skip re-rendering an unchanged leaf.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// The way its first strong character reads, worked out once a version: the line's view
        /// and the document's direction both ask on every change, and a line with no strong
        /// character at all is read to its end to answer.
        /// </summary>
        public TextDirectionality Direction
        {
            get
            {
                if (_directionVersion != Version)
                {
                    // Decided by the first strong character, so an edit past where that was found
                    // cannot change it.
                    if (!TryGetEdit(_directionVersion, out RichTextEdit edit) || (!edit.IsEmpty && edit.Start < _directionReach))
                    {
                        ComputeDirection();
                    }

                    _directionVersion = Version;
                }

                return _direction;
            }
        }

        private TextDirectionality _direction;
        private int _directionVersion = -1;

        // Where an edit has to start to leave the direction as it is: past the strong character
        // that decided it. Nowhere while none has - the next one typed, anywhere, is the first.
        private int _directionReach;

        // Read from the start: a prefix is enough for nearly every line, and the whole text is
        // only made into a string when that prefix has no strong character in it.
        private const int DirectionPrefix = 256;

        private void ComputeDirection()
        {
            _directionReach = int.MaxValue;

            if (_length == 0)
            {
                _direction = TextDirectionality.Neutral;
                return;
            }

            if (_length <= DirectionPrefix)
            {
                _direction = NativeUtils.GetDirectionality(Text);

                if (_direction != TextDirectionality.Neutral)
                {
                    _directionReach = _length;
                }

                return;
            }

            _direction = NativeUtils.GetDirectionality(new string(_chars, 0, DirectionPrefix));

            if (_direction == TextDirectionality.Neutral)
            {
                _direction = NativeUtils.GetDirectionality(Text);
            }
            else
            {
                _directionReach = DirectionPrefix;
            }
        }

        /// <summary>The text as a string. Made on the first read after an edit: what runs on every key reads <see cref="Chars"/> instead.</summary>
        public string Text => _string ??= new string(_chars, 0, _length);

        /// <summary>The text, without making a string of it; valid until the next edit.</summary>
        public ReadOnlySpan<char> Chars => new(_chars, 0, _length);

        public char this[int index] => (uint)index < (uint)_length ? _chars[index] : throw new ArgumentOutOfRangeException(nameof(index));

        public int Length => _length;

        public bool IsEmpty => _length == 0;

        public string Substring(int start, int length) => length == 0 ? string.Empty : new string(_chars, start, length);

        public int IndexOf(char value, int start, int count) => Array.IndexOf(_chars, value, start, count);

        public bool EndsWith(char value) => _length > 0 && _chars[_length - 1] == value;

        public IReadOnlyList<RichTextRun> Runs => _runs;

        public RichTextLeaf Clone() => new(this);

        // ---------------------------------------------------------------------------------
        // Queries
        // ---------------------------------------------------------------------------------

        /// <summary>The run holding the character at <paramref name="offset"/>, and where it starts.</summary>
        public int FindRun(int offset, out int runStart)
        {
            var position = 0;

            for (int i = 0; i < _runs.Count; i++)
            {
                var end = position + _runs[i].Length;
                if (offset < end)
                {
                    runStart = position;
                    return i;
                }

                position = end;
            }

            runStart = position;
            return -1;
        }

        public RichInlineObject GetObjectAt(int offset)
        {
            if (offset < 0 || offset >= _length || _chars[offset] != ObjectChar)
            {
                return null;
            }

            var index = FindRun(offset, out _);
            return index >= 0 ? _runs[index].Object : null;
        }

        /// <summary>
        /// The run that ends at <paramref name="offset"/> (or, with <paramref name="after"/>,
        /// starts there) and remembers the text it replaced.
        /// </summary>
        public bool TryGetReplaced(int offset, bool after, out int start, out int end, out string replaced)
        {
            var index = FindRun(after ? offset : offset - 1, out start);
            if (index >= 0 && _runs[index].Replaced is string text)
            {
                end = start + _runs[index].Length;

                if (after ? start == offset : end == offset)
                {
                    replaced = text;
                    return true;
                }
            }

            start = end = offset;
            replaced = null;
            return false;
        }

        /// <summary>
        /// The style typing at <paramref name="offset"/> continues: the character before it, or
        /// after it at the start of the leaf. Links do not grow at their end - a link is typed
        /// into, not appended to - and an object lends its marks but never itself.
        /// </summary>
        public void GetInsertionStyle(int offset, out RichMarks marks, out RichLink link)
        {
            marks = RichMarks.None;
            link = null;

            if (_runs.Count == 0 || IsPlain)
            {
                return;
            }

            var index = FindRun(Math.Max(0, offset - 1), out int runStart);
            if (index < 0)
            {
                index = _runs.Count - 1;
                runStart = _length - _runs[index].Length;
            }

            var run = _runs[index];
            marks = run.Marks;

            var runEnd = runStart + run.Length;
            if (offset > runStart && offset < runEnd && run.Object == null)
            {
                link = run.Link;
            }
        }

        /// <summary>The marks every character of [start, end) carries; the character before an empty range.</summary>
        public RichMarks GetCommonMarks(int start, int end)
        {
            if (start >= end)
            {
                GetInsertionStyle(start, out RichMarks marks, out _);
                return marks;
            }

            var common = (RichMarks)ushort.MaxValue;
            var position = 0;

            foreach (var run in _runs)
            {
                var runEnd = position + run.Length;
                if (runEnd > start && position < end)
                {
                    common &= run.Marks;
                }

                position = runEnd;
            }

            return common == (RichMarks)ushort.MaxValue ? RichMarks.None : common;
        }

        /// <summary>The link over all of [start, end), or null when it changes or stops inside it.</summary>
        public RichLink GetCommonLink(int start, int end)
        {
            if (start >= end)
            {
                var index = FindRun(start, out _);
                return index >= 0 ? _runs[index].Link : null;
            }

            RichLink common = null;
            var first = true;
            var position = 0;

            foreach (var run in _runs)
            {
                var runEnd = position + run.Length;
                if (runEnd > start && position < end)
                {
                    if (first)
                    {
                        common = run.Link;
                        first = false;
                    }
                    else if (!RichLink.AreEqual(common, run.Link))
                    {
                        return null;
                    }
                }

                position = runEnd;
            }

            return common;
        }

        /// <summary>The span of the link at <paramref name="offset"/>, for editing the whole link from a caret inside it.</summary>
        public bool TryGetLinkRange(int offset, out int start, out int end)
        {
            start = end = offset;

            var index = FindRun(offset, out int runStart);
            if (index < 0 || _runs[index].Link == null)
            {
                return false;
            }

            var link = _runs[index].Link;
            start = runStart;
            end = runStart + _runs[index].Length;

            for (int i = index - 1; i >= 0 && RichLink.AreEqual(_runs[i].Link, link); i--)
            {
                start -= _runs[i].Length;
            }

            for (int i = index + 1; i < _runs.Count && RichLink.AreEqual(_runs[i].Link, link); i++)
            {
                end += _runs[i].Length;
            }

            return true;
        }

        // ---------------------------------------------------------------------------------
        // Mutations. Offsets are clamped by the caller; these assume they are valid.
        // ---------------------------------------------------------------------------------

        public void Insert(int offset, string text, RichMarks marks, RichLink link)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (IsPlain)
            {
                marks = RichMarks.None;
                link = null;
            }

            // An object character can only arrive with its object; typed or pasted text that
            // happens to hold one would otherwise create an object with nothing behind it.
            if (text.IndexOf(ObjectChar) >= 0)
            {
                text = text.Replace(ObjectChar.ToString(), string.Empty);

                if (text.Length == 0)
                {
                    return;
                }
            }

            Splice(offset, 0, text);
            InsertRun(offset, new RichTextRun(text.Length, marks, link));
        }

        public void InsertObject(int offset, RichInlineObject inline, RichMarks marks)
        {
            if (IsPlain || inline == null)
            {
                return;
            }

            Splice(offset, 0, ObjectString);
            InsertRun(offset, new RichTextRun(1, marks, null, inline));
        }

        /// <summary>
        /// Puts <paramref name="text"/>, or <paramref name="inline"/> when it is set, in place of
        /// what is between <paramref name="start"/> and <paramref name="end"/>, remembering what
        /// that was (see <see cref="RichTextRun.Replaced"/>). Returns the replacement's length.
        /// </summary>
        public int Substitute(int start, int end, string text, RichInlineObject inline, RichMarks marks, RichLink link)
        {
            var replaced = Substring(start, end - start);

            // Code holds no objects, and keeps what was typed in it.
            if (IsPlain || replaced.IndexOf(ObjectChar) >= 0 || (inline == null && string.IsNullOrEmpty(text)))
            {
                return 0;
            }

            Delete(start, end);

            if (inline != null)
            {
                Splice(start, 0, ObjectString);
                InsertRun(start, new RichTextRun(1, marks, null, inline, replaced));
                return 1;
            }

            text = text.Replace(ObjectString, string.Empty);

            Splice(start, 0, text);
            InsertRun(start, new RichTextRun(text.Length, marks, link, null, replaced));
            return text.Length;
        }

        /// <summary>Inserts another leaf's content, styles and objects included.</summary>
        public void Insert(int offset, RichTextLeaf content)
        {
            if (content == null || content.IsEmpty)
            {
                return;
            }

            if (IsPlain && !content.IsPlain)
            {
                Insert(offset, content.ToPlainText(), RichMarks.None, null);
                return;
            }

            // A leaf's own text, or one it shares, is read while it is being moved.
            if (ReferenceEquals(content._chars, _chars))
            {
                Splice(offset, 0, content.Text);
            }
            else
            {
                Splice(offset, 0, content.Chars);
            }

            var position = offset;
            foreach (var run in content._runs)
            {
                InsertRun(position, run);
                position += run.Length;
            }
        }

        public void Delete(int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            Splice(start, end - start, ReadOnlySpan<char>.Empty);

            var position = 0;
            for (int i = 0; i < _runs.Count; i++)
            {
                var run = _runs[i];
                var runEnd = position + run.Length;

                var overlap = Math.Min(runEnd, end) - Math.Max(position, start);
                if (overlap > 0)
                {
                    run.Length -= overlap;

                    if (run.Length == 0)
                    {
                        _runs.RemoveAt(i--);
                    }
                    else
                    {
                        _runs[i] = run;
                    }
                }

                position = runEnd;
            }

            Normalize();
        }

        /// <summary>A new leaf with [start, end) of this one.</summary>
        public RichTextLeaf Slice(int start, int end, RichLeafRole role)
        {
            var slice = new RichTextLeaf(role, IsPlain);
            if (end <= start)
            {
                return slice;
            }

            slice._chars = Chars.Slice(start, end - start).ToArray();
            slice._length = end - start;
            slice._string = null;

            var position = 0;
            foreach (var run in _runs)
            {
                var runEnd = position + run.Length;
                var overlap = Math.Min(runEnd, end) - Math.Max(position, start);

                if (overlap > 0)
                {
                    var copy = run;
                    copy.Length = overlap;
                    slice._runs.Add(copy);
                }

                position = runEnd;
            }

            slice.Normalize();
            return slice;
        }

        /// <summary>Cuts [offset, end) off into a new leaf, which is returned: what Enter does.</summary>
        public RichTextLeaf Split(int offset, RichLeafRole role)
        {
            var tail = Slice(offset, _length, role);
            Delete(offset, _length);
            return tail;
        }

        public void Append(RichTextLeaf other) => Insert(_length, other);

        public void SetMark(int start, int end, RichMarks mark, bool value)
        {
            if (IsPlain || end <= start)
            {
                return;
            }

            // Sub- and superscript exclude each other: a character cannot sit both above and
            // below the line, and the server accepts both only to render one.
            var exclude = value
                ? mark switch
                {
                    RichMarks.Subscript => RichMarks.Superscript,
                    RichMarks.Superscript => RichMarks.Subscript,
                    _ => RichMarks.None
                }
                : RichMarks.None;

            UpdateRange(start, end, run =>
            {
                run.Marks = value ? (run.Marks | mark) & ~exclude : run.Marks & ~mark;
                return run;
            });
        }

        public void ClearMarks(int start, int end)
        {
            UpdateRange(start, end, run =>
            {
                run.Marks = RichMarks.None;
                return run;
            });
        }

        /// <summary>Sets or (with null) removes the link over [start, end). Objects carry no link.</summary>
        public void SetLink(int start, int end, RichLink link)
        {
            if (IsPlain || end <= start)
            {
                return;
            }

            UpdateRange(start, end, run =>
            {
                if (run.Object == null)
                {
                    run.Link = link;
                }

                return run;
            });
        }

        /// <summary>Replaces the content wholesale; used when converting the block between kinds.</summary>
        public void SetContent(RichTextLeaf content)
        {
            var previous = _length;

            if (content == null)
            {
                _chars = Array.Empty<char>();
                _length = 0;
                _string = string.Empty;
                _shared = false;
                _runs.Clear();
            }
            else if (IsPlain && !content.IsPlain)
            {
                SetChars(content.ToPlainText());
                _runs.Clear();

                if (_length > 0)
                {
                    _runs.Add(new RichTextRun(_length, RichMarks.None, null));
                }
            }
            else if (!ReferenceEquals(content, this))
            {
                _chars = content._chars;
                _length = content._length;
                _string = content._string;
                _shared = content._shared = true;

                _runs.Clear();
                _runs.AddRange(content._runs);
            }

            Changed(0, previous, _length, null);
        }

        // The whole text replaced by one that is already a string, which is kept as it is.
        private void SetChars(string text)
        {
            _chars = text.ToCharArray();
            _length = text.Length;
            _string = text;
            _shared = false;
        }

        /// <summary>
        /// Into a code block or out of one. The leaf stays the same object, so a selection in it
        /// survives; into plain it loses its styles and its objects read as their text.
        /// </summary>
        public void SetPlain(bool value)
        {
            if (IsPlain == value)
            {
                return;
            }

            if (value)
            {
                var previous = _length;
                var plain = ToPlainText();

                IsPlain = true;

                SetChars(plain);
                _runs.Clear();

                if (_length > 0)
                {
                    _runs.Add(new RichTextRun(_length, RichMarks.None, null));
                }

                Changed(0, previous, _length, null);
            }
            else
            {
                IsPlain = false;
                Changed();
            }
        }

        /// <summary>Appends a styled run at the end; how converters build a leaf.</summary>
        internal void AppendRun(string text, RichMarks marks, RichLink link)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Insert(_length, text, marks, link);
            }
        }

        internal void AppendObject(RichInlineObject inline, RichMarks marks)
        {
            InsertObject(_length, inline, marks);
        }

        /// <summary>The text with each object read as its plain text.</summary>
        public string ToPlainText() => ToPlainText(0, _length);

        public string ToPlainText(int start, int end)
        {
            if (end <= start)
            {
                return string.Empty;
            }

            if (IndexOf(ObjectChar, start, end - start) < 0)
            {
                return start == 0 && end == _length ? Text : Substring(start, end - start);
            }

            var builder = new System.Text.StringBuilder(end - start);
            for (int i = start; i < end; i++)
            {
                if (_chars[i] == ObjectChar)
                {
                    builder.Append(GetObjectAt(i)?.PlainText);
                }
                else
                {
                    builder.Append(_chars[i]);
                }
            }

            return builder.ToString();
        }

        // ---------------------------------------------------------------------------------

        private void InsertRun(int offset, RichTextRun insert)
        {
            var position = 0;

            for (int i = 0; i < _runs.Count; i++)
            {
                var run = _runs[i];
                var runEnd = position + run.Length;

                if (offset == position)
                {
                    _runs.Insert(i, insert);
                    Normalize();
                    return;
                }
                else if (offset < runEnd)
                {
                    // Inside a run: an object cannot be split, but it is length 1, so offset
                    // can only be its start, handled above.
                    var head = run;
                    head.Length = offset - position;

                    var tail = run;
                    tail.Length = runEnd - offset;

                    _runs[i] = head;
                    _runs.Insert(i + 1, insert);
                    _runs.Insert(i + 2, tail);
                    Normalize();
                    return;
                }

                position = runEnd;
            }

            _runs.Add(insert);
            Normalize();
        }

        private void UpdateRange(int start, int end, Func<RichTextRun, RichTextRun> update)
        {
            if (end <= start)
            {
                return;
            }

            SplitAt(start);
            SplitAt(end);

            var position = 0;
            for (int i = 0; i < _runs.Count; i++)
            {
                var run = _runs[i];
                if (position >= start && position + run.Length <= end)
                {
                    _runs[i] = update(run);
                }

                position += run.Length;
            }

            Normalize();
            Changed();
        }

        private void SplitAt(int offset)
        {
            var position = 0;

            for (int i = 0; i < _runs.Count; i++)
            {
                var run = _runs[i];
                var runEnd = position + run.Length;

                if (offset > position && offset < runEnd)
                {
                    var head = run;
                    head.Length = offset - position;

                    var tail = run;
                    tail.Length = runEnd - offset;

                    _runs[i] = head;
                    _runs.Insert(i + 1, tail);
                    return;
                }

                position = runEnd;
            }
        }

        private void Normalize()
        {
            for (int i = _runs.Count - 1; i > 0; i--)
            {
                var run = _runs[i];

                if (run.Length == 0)
                {
                    _runs.RemoveAt(i);
                }
                else if (_runs[i - 1].HasSameStyle(run))
                {
                    var previous = _runs[i - 1];
                    previous.Length += run.Length;
                    _runs[i - 1] = previous;
                    _runs.RemoveAt(i);
                }
            }

            if (_runs.Count > 0 && _runs[0].Length == 0)
            {
                _runs.RemoveAt(0);
            }
        }

        /// <summary>Puts <paramref name="inserted"/> in place of [start, start + removed), in the buffer, and records it.</summary>
        private void Splice(int start, int removed, ReadOnlySpan<char> inserted)
        {
            var length = _length - removed + inserted.Length;
            var tail = _length - start - removed;

            // Kept from the text before: what the edit took out, while it is short enough to be
            // worth keeping, for whoever has to know what was there.
            var taken = removed == 0 ? string.Empty : removed <= MaxRemembered ? new string(_chars, start, removed) : null;

            if (_shared || length > _chars.Length)
            {
                // Grown by half again, so a line typed to the end of is copied a few times over,
                // not once a key.
                var capacity = length > _chars.Length ? Math.Max(Math.Max(length, _chars.Length + _chars.Length / 2), 16) : _chars.Length;
                var chars = new char[capacity];

                Array.Copy(_chars, 0, chars, 0, start);
                Array.Copy(_chars, start + removed, chars, start + inserted.Length, tail);

                _chars = chars;
                _shared = false;
            }
            else if (removed != inserted.Length)
            {
                Array.Copy(_chars, start + removed, _chars, start + inserted.Length, tail);
            }

            inserted.CopyTo(new Span<char>(_chars, start, inserted.Length));

            _length = length;
            _string = null;

            Changed(start, start + removed, start + inserted.Length, taken);
        }

        private void Splice(int start, int removed, string inserted)
        {
            Splice(start, removed, inserted.AsSpan());
        }

        // Longer than this, what an edit took out is not kept: a selection deleted or a block
        // pasted over, where a reader can as well take the whole text as changed.
        private const int MaxRemembered = 64;

        // The last edits, a ring, for whoever kept something about an earlier version: the
        // line's view and the spell checker move what they hold by them rather than compare
        // the old text with the new. Made on the first edit; most leaves are never edited.
        private RichTextEdit[] _edits;
        private int _editHead;
        private int _editCount;

        private const int MaxEdits = 8;

        // A change of style only: the text stands as it was.
        private void Changed()
        {
            Changed(-1, -1, -1, null);
        }

        private void Changed(int start, int oldEnd, int newEnd, string removed)
        {
            Version++;

            _edits ??= new RichTextEdit[MaxEdits];
            _edits[_editHead] = new RichTextEdit(Version, start, oldEnd, newEnd, removed);
            _editHead = (_editHead + 1) % MaxEdits;
            _editCount = Math.Min(_editCount + 1, MaxEdits);
        }

        /// <summary>
        /// What changed in the text from <paramref name="fromVersion"/> to <see cref="Version"/>,
        /// as one edit: [Start, OldEnd) of the text then is [Start, NewEnd) of the text now. Empty
        /// when only styles changed, or nothing did; false when the edits are no longer known,
        /// and the text has to be taken as changed throughout.
        /// </summary>
        public bool TryGetEdit(int fromVersion, out RichTextEdit edit)
        {
            edit = new RichTextEdit(Version, -1, -1, -1, null);

            if (fromVersion == Version)
            {
                return true;
            }

            if (fromVersion > Version || Version - fromVersion > _editCount)
            {
                return false;
            }

            var first = true;
            var restyled = false;

            for (int version = fromVersion + 1; version <= Version; version++)
            {
                var next = _edits[(_editHead - (Version - version) - 1 + MaxEdits) % MaxEdits];

                if (next.IsEmpty)
                {
                    restyled = true;
                    continue;
                }

                edit = first ? next : edit.Then(next);
                first = false;
            }

            if (restyled)
            {
                edit = edit.WithRestyled();
            }

            return true;
        }
    }

    /// <summary>
    /// An edit of a leaf's text: [Start, OldEnd) of the text before it became [Start, NewEnd).
    /// <see cref="Removed"/> is what was there, when a single short edit took it out.
    /// </summary>
    public readonly struct RichTextEdit
    {
        public RichTextEdit(int version, int start, int oldEnd, int newEnd, string removed, bool restyled = false)
        {
            Version = version;
            Start = start;
            OldEnd = oldEnd;
            NewEnd = newEnd;
            Removed = removed;
            Restyled = restyled;
        }

        /// <summary>Whether a style changed on its own among the edits, which can be anywhere in the text, not only in [Start, NewEnd).</summary>
        public bool Restyled { get; }

        internal RichTextEdit WithRestyled() => new(Version, Start, OldEnd, NewEnd, Removed, true);

        public int Version { get; }

        public int Start { get; }

        public int OldEnd { get; }

        public int NewEnd { get; }

        public string Removed { get; }

        /// <summary>No text changed: a style did, or nothing.</summary>
        public bool IsEmpty => Start < 0;

        /// <summary>
        /// This edit followed by <paramref name="next"/>, which is in the text this one left, as
        /// one. What it removed is known only when that is nothing: otherwise the range can hold
        /// text neither of them touched.
        /// </summary>
        public RichTextEdit Then(in RichTextEdit next)
        {
            var start = Math.Min(Start, next.Start);
            var middle = Math.Max(NewEnd, next.OldEnd);

            var oldEnd = middle - (NewEnd - OldEnd);
            var newEnd = middle + (next.NewEnd - next.OldEnd);

            return new RichTextEdit(next.Version, start, oldEnd, newEnd, oldEnd == start ? string.Empty : null);
        }
    }
}
