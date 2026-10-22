//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Services;
using Windows.Foundation;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Spelling, by the system's checker in the keyboard's language, checked when a word ends as
    /// the system's text boxes do: a separator typed or deleted, the caret moved away, a block
    /// added. Only the leaves that changed are checked; between checks, what was found follows
    /// the edits, and only the word being edited loses its squiggle.
    /// </summary>
    public sealed partial class RichEditorView
    {
        private readonly Dictionary<RichTextLeaf, SpellingResult> _spelling = new();
        private readonly List<Polyline> _spellingLines = new();

        // The width each line's points were made for: redrawing follows every caret move, and a
        // squiggle that only moved keeps its points.
        private readonly List<double> _spellingWidths = new();

        private bool _spellingRunning;
        private bool _spellingPending;

        // Some leaf changed since it was checked; and whether a word was ended by that change.
        private bool _spellingDirty;
        private bool _spellingDue;

        // Made and used on the thread pool only: the system's checker lives in the multithreaded
        // apartment. The input language it was picked for, so it is picked again only when the
        // keyboard changes; null with no checker means there is no dictionary to check against.
        private SpellChecker _spellChecker;
        private string _spellingInput;

        private Rectangle SpellingSwatch;

        private readonly struct SpellingResult
        {
            public SpellingResult(int version, int[] ranges, int dirtyStart = -1, int dirtyEnd = -1)
            {
                Version = version;
                Ranges = ranges;
                DirtyStart = dirtyStart;
                DirtyEnd = dirtyEnd;
            }

            /// <summary>The leaf's version <see cref="Ranges"/> were moved to: the edits since are the leaf's to say.</summary>
            public int Version { get; }

            // Start, length, start, length...
            public int[] Ranges { get; }

            /// <summary>
            /// What edits left unchecked, [DirtyStart, DirtyEnd), which is what the next check
            /// checks; nothing when DirtyStart is negative, and to the end at <see cref="ToTheEnd"/>.
            /// </summary>
            public int DirtyStart { get; }

            public int DirtyEnd { get; }

            public bool IsChecked => DirtyStart < 0;
        }

        private const int ToTheEnd = int.MaxValue;

        /// <summary>
        /// A leaf's text to check, from <see cref="Start"/>, and what was found in the rest of it,
        /// which stands.
        /// </summary>
        private readonly struct SpellingWork
        {
            public SpellingWork(RichTextLeaf leaf, int version, int start, string text, int[] kept)
            {
                Leaf = leaf;
                Version = version;
                Start = start;
                Text = text;
                Kept = kept;
            }

            public RichTextLeaf Leaf { get; }

            public int Version { get; }

            public int Start { get; }

            public string Text { get; }

            public int[] Kept { get; }
        }

        private void UpdateSpelling(RichEditorChanges changes, RichTextLeaf leaf)
        {
            if (!AppSettings.UseSystemSpellChecker)
            {
                if (_spelling.Count > 0)
                {
                    _spelling.Clear();
                    DrawSpelling();
                }

                return;
            }

            if ((changes & RichEditorChanges.Structure) != 0 || ((changes & RichEditorChanges.Text) != 0 && leaf == null))
            {
                CheckSpelling();
            }
            else if ((changes & RichEditorChanges.Text) != 0)
            {
                // Drawing may have moved the leaf's squiggles already, and noted what the edit was.
                // A leaf with no result was missed by the last check: one running when it was made.
                if ((!Rebase(leaf) && !leaf.IsPlain) || _spellingDue)
                {
                    CheckSpelling();
                }
            }
            else if ((changes & RichEditorChanges.Selection) != 0 && _spellingDirty)
            {
                // The caret left what was being typed.
                CheckSpelling();
            }
        }

        private void CheckSpelling()
        {
            if (_engine == null || !AppSettings.UseSystemSpellChecker)
            {
                return;
            }

            _spellingDue = false;

            // One check at a time; what changed meanwhile is checked after it.
            if (_spellingRunning)
            {
                _spellingPending = true;
                return;
            }

            _spellingDirty = false;

            var leaves = _engine.Document.Leaves;

            // Leaves gone from the document.
            List<RichTextLeaf> gone = null;
            foreach (var leaf in _spelling.Keys)
            {
                if (leaf.Index >= leaves.Count || !ReferenceEquals(leaves[leaf.Index], leaf) || leaf.IsPlain)
                {
                    (gone ??= new List<RichTextLeaf>()).Add(leaf);
                }
            }

            for (int i = 0; i < gone?.Count; i++)
            {
                _spelling.Remove(gone[i]);
            }

            List<SpellingWork> work = null;

            foreach (var leaf in leaves)
            {
                if (leaf.IsPlain)
                {
                    _spelling.Remove(leaf);
                    continue;
                }

                // Nothing to check, but a result all the same: the first word typed is noticed
                // by comparing against it.
                if (leaf.Length == 0)
                {
                    _spelling[leaf] = new SpellingResult(leaf.Version, Array.Empty<int>());
                    continue;
                }

                if (!Rebase(leaf))
                {
                    (work ??= new List<SpellingWork>()).Add(new SpellingWork(leaf, leaf.Version, 0, leaf.Text, Array.Empty<int>()));
                    continue;
                }

                var result = _spelling[leaf];
                if (result.IsChecked)
                {
                    continue;
                }

                // Only what edits left unchecked, out to the ends of its words: a check of the
                // whole leaf for every word typed made a string of all of it each time.
                GetDirtyWords(leaf, result, out int start, out int end);

                (work ??= new List<SpellingWork>()).Add(new SpellingWork(leaf, leaf.Version, start, leaf.Substring(start, end - start), Outside(result.Ranges, start, end)));
            }

            if (work == null)
            {
                DrawSpelling();
                return;
            }

            _spellingRunning = true;
            _ = CheckSpellingAsync(work, Windows.Globalization.Language.CurrentInputMethodLanguageTag);
        }

        private async Task CheckSpellingAsync(List<SpellingWork> work, string input)
        {
            var checker = _spellChecker;
            var resolved = _spellingInput;
            List<int[]> found = null;

            try
            {
                found = await Task.Run(() =>
                {
                    if (resolved != input)
                    {
                        checker = CreateSpellChecker(input);
                        resolved = input;
                    }

                    var results = new List<int[]>(work.Count);

                    foreach (var item in work)
                    {
                        if (checker == null)
                        {
                            results.Add(Array.Empty<int>());
                            continue;
                        }

                        var ranges = checker.Check(item.Text);
                        var copy = new int[ranges.Count];
                        ranges.CopyTo(copy, 0);
                        results.Add(copy);
                    }

                    return results;
                });
            }
            catch
            {
                // A checker that failed checks nothing; the text is not worse for it.
                found = null;
            }

            _spellChecker = checker;
            _spellingInput = resolved;
            _spellingRunning = false;

            if (found != null && _engine != null)
            {
                var leaves = _engine.Document.Leaves;

                for (int i = 0; i < work.Count; i++)
                {
                    var item = work[i];
                    var leaf = item.Leaf;

                    if (leaf.Index >= leaves.Count || !ReferenceEquals(leaves[leaf.Index], leaf))
                    {
                        continue;
                    }

                    var ranges = Combine(item.Kept, found[i], item.Start);
                    var dirtyStart = -1;
                    var dirtyEnd = -1;

                    // Typed into meanwhile: what was found is moved onto the text as it is now,
                    // like any edit, and the leaf is checked again with the next word.
                    if (item.Version != leaf.Version)
                    {
                        ranges = Move(leaf, item.Version, ranges, ref dirtyStart, ref dirtyEnd, out _, out _);
                    }

                    _spelling[leaf] = new SpellingResult(leaf.Version, Filter(leaf, ranges), dirtyStart, dirtyEnd);
                    _spellingDirty |= item.Version != leaf.Version;
                }

                DrawSpelling();
            }

            if (_spellingPending)
            {
                _spellingPending = false;
                CheckSpelling();
            }
        }

        /// <summary>
        /// Brings a leaf's result up to the leaf's text; false if the leaf has none. An edit is
        /// what differs between the text the result was for and the leaf's, which costs a pass
        /// over the leaf and nothing else; a version bumped by a style alone moves nothing.
        /// </summary>
        private bool Rebase(RichTextLeaf leaf)
        {
            if (!_spelling.TryGetValue(leaf, out SpellingResult result))
            {
                return false;
            }

            if (result.Version != leaf.Version)
            {
                var dirtyStart = result.DirtyStart;
                var dirtyEnd = result.DirtyEnd;

                var ranges = Move(leaf, result.Version, result.Ranges, ref dirtyStart, ref dirtyEnd, out bool changed, out bool boundary);

                _spelling[leaf] = new SpellingResult(leaf.Version, ranges, dirtyStart, dirtyEnd);
                _spellingDirty |= changed;
                _spellingDue |= boundary;
            }

            return true;
        }

        /// <summary>
        /// Word ranges found in the leaf's text at <paramref name="version"/>, moved onto the text
        /// as it is now by the edits the leaf says it took since: those before the edited span
        /// stay, those after it shift, and one touching it goes, as that word is still being
        /// typed. <paramref name="boundary"/> says whether the edit typed or deleted anything that
        /// is not part of a word, which is what ends one. What was left unchecked follows the
        /// edit too, and takes in what it changed. Edits the leaf no longer knows of are taken as
        /// the whole text changed.
        /// </summary>
        private static int[] Move(RichTextLeaf leaf, int version, int[] ranges, ref int dirtyStart, ref int dirtyEnd, out bool changed, out bool boundary)
        {
            if (!leaf.TryGetEdit(version, out RichTextEdit edit))
            {
                changed = true;
                boundary = true;
                dirtyStart = 0;
                dirtyEnd = ToTheEnd;
                return Array.Empty<int>();
            }

            changed = !edit.IsEmpty;
            boundary = false;

            if (!changed)
            {
                return ranges;
            }

            var to = leaf.Chars;
            var prefix = edit.Start;
            var oldEnd = edit.OldEnd;
            var newEnd = edit.NewEnd;

            if (dirtyStart < 0)
            {
                dirtyStart = prefix;
                dirtyEnd = newEnd;
            }
            else
            {
                dirtyStart = Math.Min(dirtyStart < oldEnd ? Math.Min(dirtyStart, prefix) : dirtyStart + newEnd - oldEnd, prefix);
                dirtyEnd = dirtyEnd == ToTheEnd ? ToTheEnd : Math.Max(dirtyEnd <= prefix ? dirtyEnd : dirtyEnd < oldEnd ? newEnd : dirtyEnd + newEnd - oldEnd, newEnd);
            }

            // What the edit took out, when the leaf still knows: the old text's characters at
            // either end of the edit are those, or, when nothing was taken out, the characters
            // around it, which are the same now as then.
            var removed = edit.Removed;

            boundary = removed == null || HasSeparator(removed.AsSpan(), 0, removed.Length) || HasSeparator(to, prefix, newEnd);

            if (ranges.Length == 0)
            {
                return ranges;
            }

            var delta = newEnd - oldEnd;

            // A word right against the edit is still the same word if a separator stands between
            // them before and after it: a space typed after a misspelling keeps its squiggle.
            var before = prefix - 1;
            var after = oldEnd + 1;

            if (removed != null)
            {
                var endedBefore = removed.Length > 0 ? IsSeparator(removed[0]) : newEnd >= to.Length || IsSeparator(to[newEnd]);
                var startedAfter = removed.Length > 0 ? IsSeparator(removed[removed.Length - 1]) : prefix == 0 || IsSeparator(to[prefix - 1]);

                if (endedBefore && IsWordEnd(to, prefix))
                {
                    before = prefix;
                }

                if ((oldEnd == 0 || startedAfter) && IsWordStart(to, newEnd))
                {
                    after = oldEnd;
                }
            }

            var count = 0;

            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                if (ranges[i] + ranges[i + 1] <= before || ranges[i] >= after)
                {
                    count += 2;
                }
            }

            var moved = count == 0 ? Array.Empty<int>() : new int[count];
            var j = 0;

            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                var start = ranges[i];
                var length = ranges[i + 1];

                if (start + length <= before)
                {
                    moved[j++] = start;
                    moved[j++] = length;
                }
                else if (start >= after)
                {
                    moved[j++] = start + delta;
                    moved[j++] = length;
                }
            }

            return moved;
        }

        // The range what edits left unchecked spans, a character either side for the words that
        // touched it, out to the ends of the words at either end.
        private static void GetDirtyWords(RichTextLeaf leaf, in SpellingResult result, out int start, out int end)
        {
            var length = leaf.Length;

            start = Math.Clamp(result.DirtyStart - 1, 0, length);
            end = result.DirtyEnd == ToTheEnd ? length : Math.Clamp(result.DirtyEnd + 1, start, length);

            while (start > 0 && !IsSeparator(leaf[start - 1]))
            {
                start--;
            }

            while (end < length && !IsSeparator(leaf[end]))
            {
                end++;
            }
        }

        // The ranges wholly outside [start, end): what a check of that range leaves standing.
        private static int[] Outside(int[] ranges, int start, int end)
        {
            var count = 0;

            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                if (ranges[i] + ranges[i + 1] <= start || ranges[i] >= end)
                {
                    count += 2;
                }
            }

            if (count == ranges.Length)
            {
                return ranges;
            }

            var outside = new int[count];
            var j = 0;

            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                if (ranges[i] + ranges[i + 1] <= start || ranges[i] >= end)
                {
                    outside[j++] = ranges[i];
                    outside[j++] = ranges[i + 1];
                }
            }

            return outside;
        }

        // What stood before the checked text, what was found in it, and what stood after it, in
        // order. Found is in the checked text's own offsets.
        private static int[] Combine(int[] kept, int[] found, int start)
        {
            if (kept.Length == 0 && start == 0)
            {
                return found;
            }

            var combined = new int[kept.Length + found.Length];
            var j = 0;
            var k = 0;

            while (k + 1 < kept.Length && kept[k] < start)
            {
                combined[j++] = kept[k++];
                combined[j++] = kept[k++];
            }

            for (int i = 0; i + 1 < found.Length; i += 2)
            {
                combined[j++] = found[i] + start;
                combined[j++] = found[i + 1];
            }

            while (k + 1 < kept.Length)
            {
                combined[j++] = kept[k++];
                combined[j++] = kept[k++];
            }

            return combined;
        }

        private static bool IsWordEnd(ReadOnlySpan<char> text, int offset) => offset >= text.Length || IsSeparator(text[offset]);

        private static bool IsWordStart(ReadOnlySpan<char> text, int offset) => offset == 0 || IsSeparator(text[offset - 1]);

        private static bool HasSeparator(ReadOnlySpan<char> text, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (IsSeparator(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // An apostrophe is inside a word: "don't" is one, and is not ended halfway.
        private static bool IsSeparator(char c)
        {
            return !char.IsLetterOrDigit(c) && c != '\'' && c != '’' && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark;
        }

        /// <summary>
        /// The keyboard's language when the system has a dictionary for it, and otherwise the first
        /// of the user's languages that has one. On the thread pool.
        /// </summary>
        private static SpellChecker CreateSpellChecker(string input)
        {
            if (!string.IsNullOrEmpty(input) && SpellChecker.Create(input) is SpellChecker checker)
            {
                return checker;
            }

            foreach (var language in GlobalizationPreferences.Languages)
            {
                if (language != input && SpellChecker.Create(language) is SpellChecker fallback)
                {
                    return fallback;
                }
            }

            return null;
        }

        /// <summary>
        /// What the checker found, without the words that are not prose: a link or a mention, an
        /// inline object, monospace.
        /// </summary>
        private static int[] Filter(RichTextLeaf leaf, int[] ranges)
        {
            if (ranges.Length == 0)
            {
                return ranges;
            }

            List<int> kept = null;
            var runs = leaf.Runs;

            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                var start = ranges[i];
                var end = start + ranges[i + 1];
                var prose = true;
                var position = 0;

                foreach (var run in runs)
                {
                    var next = position + run.Length;
                    if (next > start && position < end && (run.Link != null || run.Object != null || (run.Marks & RichMarks.Monospace) != 0))
                    {
                        prose = false;
                        break;
                    }

                    if (next >= end)
                    {
                        break;
                    }

                    position = next;
                }

                if (prose)
                {
                    kept ??= new List<int>(ranges.Length);
                    kept.Add(ranges[i]);
                    kept.Add(ranges[i + 1]);
                }
            }

            return kept?.ToArray() ?? Array.Empty<int>();
        }

        /// <summary>A wavy line under every word found in a leaf that has not changed since.</summary>
        private void DrawSpelling()
        {
            if (Decorations == null || SpellingSwatch == null)
            {
                return;
            }

            var used = 0;

            if (_spelling.Count > 0)
            {
                foreach (var view in _leafViews.Values)
                {
                    var leaf = view.Leaf;
                    if (leaf == null || view.Visibility != Visibility.Visible || !Rebase(leaf))
                    {
                        continue;
                    }

                    var result = _spelling[leaf];
                    if (result.Ranges.Length == 0)
                    {
                        continue;
                    }

                    var transform = view.TransformToVisual(Surface);

                    for (int i = 0; i + 1 < result.Ranges.Length; i += 2)
                    {
                        foreach (var rect in view.GetRangeRects(result.Ranges[i], result.Ranges[i] + result.Ranges[i + 1]))
                        {
                            var origin = transform.TransformPoint(new Point(rect.X, rect.Bottom - 2));
                            DrawSquiggle(used++, origin, rect.Width);
                        }
                    }
                }
            }

            for (int i = used; i < _spellingLines.Count; i++)
            {
                _spellingLines[i].Visibility = Visibility.Collapsed;
            }
        }

        private void DrawSquiggle(int index, Point origin, double width)
        {
            Polyline line;
            if (index < _spellingLines.Count)
            {
                line = _spellingLines[index];
            }
            else
            {
                line = new Polyline
                {
                    Stroke = SpellingSwatch.Fill,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };

                _spellingLines.Add(line);
                _spellingWidths.Add(-1);
                Decorations.Children.Add(line);
            }

            if (_spellingWidths[index] != width)
            {
                _spellingWidths[index] = width;

                // Two pixels a step, a pixel and a half up and down: what text boxes draw.
                var points = new PointCollection();
                for (double x = 0; x <= width; x += 2)
                {
                    points.Add(new Point(x, ((int)(x / 2) & 1) == 0 ? 0 : 1.5));
                }

                line.Points = points;
            }

            Canvas.SetLeft(line, origin.X);
            Canvas.SetTop(line, origin.Y);
            line.Visibility = Visibility.Visible;
        }

        // =====================================================================================
        // What the menu offers for a word
        // =====================================================================================

        /// <summary>
        /// Whether the character at <paramref name="offset"/> is in a word the last check found
        /// misspelled, with <paramref name="limit"/> brought in to where that next changes: for
        /// a screen reader, which says so as it reads.
        /// </summary>
        internal bool IsMisspelled(RichTextLeaf leaf, int offset, ref int limit)
        {
            if (!Rebase(leaf))
            {
                return false;
            }

            var ranges = _spelling[leaf].Ranges;
            var misspelled = false;

            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                var start = ranges[i];
                var end = start + ranges[i + 1];

                if (offset >= start && offset < end)
                {
                    misspelled = true;
                    limit = Math.Min(limit, end);
                }
                else if (start > offset)
                {
                    limit = Math.Min(limit, start);
                }
            }

            return misspelled;
        }

        /// <summary>
        /// The misspelled word at the caret, or the one selected, as its last check found it; the
        /// host offers suggestions for it.
        /// </summary>
        public bool TryGetMisspelling(out string word)
        {
            word = null;

            if (!TryGetMisspelling(out RichTextLeaf leaf, out int start, out int length))
            {
                return false;
            }

            word = leaf.Substring(start, length);
            return true;
        }

        private bool TryGetMisspelling(out RichTextLeaf leaf, out int start, out int length)
        {
            leaf = null;
            start = length = 0;

            if (_engine == null || _engine.SelectedNode != null || !ReferenceEquals(_engine.Start.Leaf, _engine.End.Leaf))
            {
                return false;
            }

            var focus = _engine.Focus.Leaf;
            if (!Rebase(focus))
            {
                return false;
            }

            var result = _spelling[focus];

            var from = _engine.Start.Offset;
            var to = _engine.End.Offset;

            for (int i = 0; i + 1 < result.Ranges.Length; i += 2)
            {
                var wordStart = result.Ranges[i];
                var wordEnd = wordStart + result.Ranges[i + 1];

                if (from >= wordStart && to <= wordEnd)
                {
                    leaf = focus;
                    start = wordStart;
                    length = result.Ranges[i + 1];
                    return true;
                }
            }

            return false;
        }

        public async Task<IList<string>> SuggestAsync(string word)
        {
            var checker = _spellChecker;
            if (checker == null || string.IsNullOrEmpty(word))
            {
                return Array.Empty<string>();
            }

            try
            {
                return await Task.Run(() =>
                {
                    var suggestions = checker.Suggest(word);
                    var copy = new string[Math.Min(suggestions.Count, 5)];

                    for (int i = 0; i < copy.Length; i++)
                    {
                        copy[i] = suggestions[i];
                    }

                    return (IList<string>)copy;
                });
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>The misspelled word at the caret replaced with <paramref name="text"/>.</summary>
        public void ReplaceMisspelling(string text)
        {
            if (TryGetMisspelling(out RichTextLeaf leaf, out int start, out int length))
            {
                _engine.ReplaceText(leaf, start, start + length, text);
            }
        }

        /// <summary>The word at the caret into the user's dictionary, which every app shares.</summary>
        public void AddToDictionary() => Teach(true);

        /// <summary>The word at the caret left alone for as long as the checker lives.</summary>
        public void IgnoreMisspelling() => Teach(false);

        private void Teach(bool add)
        {
            var checker = _spellChecker;
            if (checker == null || !TryGetMisspelling(out string word))
            {
                return;
            }

            _ = TeachAsync(checker, word, add);
        }

        private async Task TeachAsync(SpellChecker checker, string word, bool add)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (add)
                    {
                        checker.Add(word);
                    }
                    else
                    {
                        checker.Ignore(word);
                    }
                });
            }
            catch
            {
                return;
            }

            // The word may be anywhere else too: everything is checked again, and what is shown
            // stays until then.
            foreach (var leaf in new List<RichTextLeaf>(_spelling.Keys))
            {
                var result = _spelling[leaf];
                _spelling[leaf] = new SpellingResult(result.Version, result.Ranges, 0, ToTheEnd);
            }

            CheckSpelling();
        }
    }
}
