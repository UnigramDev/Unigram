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
            public SpellingResult(int version, string text, int[] ranges, bool isChecked)
            {
                Version = version;
                Text = text;
                Ranges = ranges;
                IsChecked = isChecked;
            }

            /// <summary>The leaf's version <see cref="Ranges"/> were moved to.</summary>
            public int Version { get; }

            /// <summary>The leaf's text at that version: the next edit is found by comparing against it.</summary>
            public string Text { get; }

            // Start, length, start, length...
            public int[] Ranges { get; }

            /// <summary>False once an edit left part of the text unchecked.</summary>
            public bool IsChecked { get; }
        }

        private readonly struct SpellingWork
        {
            public SpellingWork(RichTextLeaf leaf, int version, string text)
            {
                Leaf = leaf;
                Version = version;
                Text = text;
            }

            public RichTextLeaf Leaf { get; }

            public int Version { get; }

            public string Text { get; }
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
                    _spelling[leaf] = new SpellingResult(leaf.Version, string.Empty, Array.Empty<int>(), true);
                    continue;
                }

                if (Rebase(leaf) && _spelling[leaf].IsChecked)
                {
                    continue;
                }

                (work ??= new List<SpellingWork>()).Add(new SpellingWork(leaf, leaf.Version, leaf.Text));
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

                    // Typed into meanwhile: what was found is moved onto the text as it is now,
                    // like any edit, and the leaf is checked again with the next word.
                    var ranges = item.Version == leaf.Version ? found[i] : Move(item.Text, leaf.Text, found[i], out _, out _);

                    _spelling[leaf] = new SpellingResult(leaf.Version, leaf.Text, Filter(leaf, ranges), item.Version == leaf.Version);
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
                var ranges = Move(result.Text, leaf.Text, result.Ranges, out bool changed, out bool boundary);

                _spelling[leaf] = new SpellingResult(leaf.Version, leaf.Text, ranges, result.IsChecked && !changed);
                _spellingDirty |= changed;
                _spellingDue |= boundary;
            }

            return true;
        }

        /// <summary>
        /// Word ranges found in <paramref name="from"/>, moved onto <paramref name="to"/>: those
        /// before the edited span stay, those after it shift, and one touching it goes, as that
        /// word is still being typed. <paramref name="boundary"/> says whether the edit typed or
        /// deleted anything that is not part of a word, which is what ends one.
        /// </summary>
        private static int[] Move(string from, string to, int[] ranges, out bool changed, out bool boundary)
        {
            var prefix = 0;
            var shortest = Math.Min(from.Length, to.Length);

            while (prefix < shortest && from[prefix] == to[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < shortest - prefix && from[from.Length - 1 - suffix] == to[to.Length - 1 - suffix])
            {
                suffix++;
            }

            var oldEnd = from.Length - suffix;
            var newEnd = to.Length - suffix;

            changed = prefix != oldEnd || prefix != newEnd;
            boundary = HasSeparator(from, prefix, oldEnd) || HasSeparator(to, prefix, newEnd);

            if (!changed || ranges.Length == 0)
            {
                return ranges;
            }

            var delta = newEnd - oldEnd;

            // A word right against the edit is still the same word if a separator stands between
            // them before and after it: a space typed after a misspelling keeps its squiggle.
            var before = IsWordEnd(from, prefix) && IsWordEnd(to, prefix) ? prefix : prefix - 1;
            var after = IsWordStart(from, oldEnd) && IsWordStart(to, newEnd) ? oldEnd : oldEnd + 1;

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

        private static bool IsWordEnd(string text, int offset) => offset >= text.Length || IsSeparator(text[offset]);

        private static bool IsWordStart(string text, int offset) => offset == 0 || IsSeparator(text[offset - 1]);

        private static bool HasSeparator(string text, int start, int end)
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

            word = leaf.Text.Substring(start, length);
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
                _spelling[leaf] = new SpellingResult(result.Version, result.Text, result.Ranges, false);
            }

            CheckSpelling();
        }
    }
}
