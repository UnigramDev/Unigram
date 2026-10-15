//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// What a keystroke costs, measured in the editor it is typed into: the documents
    /// notes/native-editor-performance-plan.md is judged by, typed into a key a frame through the
    /// path the input service takes, and timed by stage. Offered on Ctrl+Alt+Shift+M in any
    /// editor while the diagnostics page's Editor Meter is on. The editor's own text is put back
    /// afterwards; its undo history is not.
    /// </summary>
    public sealed class RichEditorMeter
    {
        // Read by the view on every change: while nothing is measured, this load is the cost.
        private static bool _active;
        private static long _viewTicks;

        private const int KeysPerRun = 100;
        private const string Typed = "the quick brown fox jumps over the lazy dog ";

        private static readonly string[] Words =
        {
            "message", "editor", "line", "paragraph", "the", "a", "of", "and", "to", "in", "is",
            "text", "layout", "surface", "frame", "keystroke", "quick", "brown", "fox", "jumps",
            "over", "lazy", "dog", "Telegram", "draws", "every", "glyph", "itself", "while", "typing"
        };

        public enum Scenario
        {
            ThisText,
            MessageParagraph,
            MessageLines,
            ArticleParagraph,
            ArticleParagraphs,
            ArticleMixed
        }

        private readonly RichEditorView _view;

        private RichEditorMeter(RichEditorView view)
        {
            _view = view;
        }

        public static void ShowMenu(RichEditorView view)
        {
            if (_active || view.Engine == null)
            {
                return;
            }

            var meter = new RichEditorMeter(view);
            var flyout = new MenuFlyout();

            if (view.Engine.Schema != null)
            {
                flyout.CreateFlyoutItem(meter.Run, Scenario.MessageParagraph, "One Paragraph of 4,096 Characters", Icons.TopSpeed);
                flyout.CreateFlyoutItem(meter.Run, Scenario.MessageLines, "100 Lines", Icons.TopSpeed);
            }
            else
            {
                flyout.CreateFlyoutItem(meter.Run, Scenario.ArticleParagraph, "One Paragraph of 100,000 Characters", Icons.TopSpeed);
                flyout.CreateFlyoutItem(meter.Run, Scenario.ArticleParagraphs, "2,000 Paragraphs", Icons.TopSpeed);
                flyout.CreateFlyoutItem(meter.Run, Scenario.ArticleMixed, "An Article of 100,000 Characters", Icons.TopSpeed);
            }

            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(meter.Run, Scenario.ThisText, "This Text", Icons.Timer);


            flyout.ShowAt(view, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Auto });
        }

        // =====================================================================================
        // What the view reports
        // =====================================================================================

        /// <summary>Zero while nothing is measured, which <see cref="StopView"/> then ignores.</summary>
        internal static long StartView()
        {
            return _active ? Stopwatch.GetTimestamp() : 0;
        }

        internal static void StopView(long started)
        {
            if (started != 0)
            {
                _viewTicks += Stopwatch.GetTimestamp() - started;
            }
        }

        // =====================================================================================
        // A run
        // =====================================================================================

        private struct Key
        {
            public double Handlers;
            public double View;
            public double Layout;
            public double Frame;

            // Whether the handlers ran into a collection of any generation, and whether a full
            // one happened in the frame, which is the one the key before was drawn in.
            public bool Collected;
            public bool FrameCollectedFully;
        }

        private async void Run(Scenario scenario)
        {
            var engine = _view.Engine;
            var saved = engine.Document.Clone();
            var report = new StringBuilder();

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Editor meter, {0:yyyy-MM-dd HH:mm:ss}, {1}", DateTime.Now, scenario));
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} editor, {1:0}x{2:0} DIPs at {3:0.##}x",
                engine.Schema != null ? "Message" : "Article", _view.ActualWidth, _view.ActualHeight, _view.XamlRoot?.RasterizationScale ?? 1));

            try
            {
                _active = true;
                DirectTextLayout.ResetCounters();

                if (scenario != Scenario.ThisText)
                {
                    var document = Generate(scenario);
                    await NextFrameAsync();

                    var started = Stopwatch.GetTimestamp();
                    engine.Load(document);
                    _view.UpdateLayout();

                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Opened in {0:0.0} ms", Milliseconds(Stopwatch.GetTimestamp() - started)));
                }

                Describe(report, engine.Document);

                await RunAsync(report, "In the middle", PlaceInMiddle);
                await RunAsync(report, "At the end", PlaceAtEnd);

                report.AppendLine();
                report.Append(DirectTextLayout.Counters);
            }
            catch (Exception ex)
            {
                report.AppendLine("Failed: " + ex);
            }
            finally
            {
                _active = false;
                engine.Load(saved);
            }

            var text = report.ToString();
            Logger.Info(text);

            try
            {
                File.AppendAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path, "editor-meter.txt"), text + Environment.NewLine);
            }
            catch
            {
                // The report is in the log and on screen as well.
            }

            var confirm = await MessagePopup.ShowAsync(_view.XamlRoot, text, "Editor Meter", "OK", "Copy");
            if (confirm == ContentDialogResult.Secondary)
            {
                MessageHelper.CopyText(_view.XamlRoot, text);
            }
        }

        private async Task RunAsync(StringBuilder report, string title, Action<RichEditorEngine> place)
        {
            place(_view.Engine);

            // The caret moved and may have scrolled: settled before the first key is timed.
            await NextFrameAsync();
            _view.UpdateLayout();

            var keys = new Key[KeysPerRun];
            var previous = await NextFrameAsync();

            // A collection counts toward every younger generation too: a gen0 count that moved is
            // any collection at all, a gen2 count that moved is a full one.
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);
            var heap = GC.GetTotalMemory(false);

            var frameGen2 = gen2;

            for (int i = 0; i < keys.Length; i++)
            {
                // The time since the last frame is the frame the previous key was drawn in.
                var frame = await NextFrameAsync();
                var frameEndGen2 = GC.CollectionCount(2);

                _viewTicks = 0;

                var collections = GC.CollectionCount(0);
                var started = Stopwatch.GetTimestamp();
                _view.TypeForMeasure(Typed[i % Typed.Length].ToString());
                var typed = Stopwatch.GetTimestamp();
                _view.UpdateLayout();
                var laidOut = Stopwatch.GetTimestamp();

                keys[i] = new Key
                {
                    Handlers = Milliseconds(typed - started),
                    View = Milliseconds(_viewTicks),
                    Layout = Milliseconds(laidOut - typed),
                    Frame = Milliseconds(frame - previous),
                    Collected = GC.CollectionCount(0) != collections,
                    FrameCollectedFully = frameEndGen2 != frameGen2
                };

                previous = frame;
                frameGen2 = frameEndGen2;
            }

            var collected = string.Format(CultureInfo.InvariantCulture, "Collections: gen0 {0}, gen1 {1}, gen2 {2}; managed heap {3:0.0} MB to {4:0.0} MB",
                GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2, heap / 1048576.0, GC.GetTotalMemory(false) / 1048576.0);

            report.AppendLine();
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}, {1} keys, ms: median / p95 / max", title, keys.Length));

            Row(report, "Handlers, all", keys, static key => key.Handlers);
            Row(report, "  of which the view", keys, static key => key.View);
            Row(report, "  the rest: engine, box, page", keys, static key => key.Handlers - key.View);
            Row(report, "Layout and draw", keys, static key => key.Layout);
            Row(report, "Frame", keys, static key => key.Frame);

            report.AppendLine(collected);
            Split(report, "Keys with a collection in the handlers", keys, static key => key.Collected, static key => key.Handlers);
            Split(report, "Frames with a full collection", keys, static key => key.FrameCollectedFully, static key => key.Frame);

            var slow = 0;
            foreach (var key in keys)
            {
                if (key.Frame > 1000.0 / 60 * 1.5)
                {
                    slow++;
                }
            }

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Frames over 25 ms: {0}", slow));
        }

        private static void Row(StringBuilder report, string name, Key[] keys, Func<Key, double> value)
        {
            var values = new double[keys.Length];

            for (int i = 0; i < keys.Length; i++)
            {
                values[i] = value(keys[i]);
            }

            Array.Sort(values);

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-32} {1,7:0.00} {2,7:0.00} {3,7:0.00}",
                name, values[values.Length / 2], values[(int)(values.Length * 0.95)], values[values.Length - 1]));
        }

        // How many keys the condition holds for, and the median of the value with it and without.
        private static void Split(StringBuilder report, string name, Key[] keys, Func<Key, bool> condition, Func<Key, double> value)
        {
            var with = new List<double>();
            var without = new List<double>();

            foreach (var key in keys)
            {
                (condition(key) ? with : without).Add(value(key));
            }

            with.Sort();
            without.Sort();

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1}, median {2:0.00} ms against {3:0.00} ms without",
                name, with.Count, with.Count > 0 ? with[with.Count / 2] : 0, without.Count > 0 ? without[without.Count / 2] : 0));
        }

        private void Describe(StringBuilder report, RichDocument document)
        {
            var characters = 0;
            var longest = 0;

            foreach (var leaf in document.Leaves)
            {
                characters += leaf.Length;
                longest = Math.Max(longest, leaf.Length);
            }

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:N0} characters in {1:N0} leaves and {2:N0} blocks, the longest leaf {3:N0}; {4:N0} leaves and {5:N0} blocks realized",
                characters, document.Leaves.Count, document.Blocks.Count, longest, _view.RealizedLeafCount, _view.RealizedBlockCount));
        }

        // The longest text, which is where a keystroke costs most: the paragraph of a single
        // paragraph document, or one in the middle of a document of short ones.
        private static void PlaceInMiddle(RichEditorEngine engine)
        {
            var leaves = engine.Document.Leaves;
            RichTextLeaf target = null;

            foreach (var leaf in leaves)
            {
                if (leaf.Role == RichLeafRole.Text && (target == null || leaf.Length > target.Length))
                {
                    target = leaf;
                }
            }

            if (target != null && target.Length < 200)
            {
                for (int i = leaves.Count / 2; i < leaves.Count; i++)
                {
                    if (leaves[i].Role == RichLeafRole.Text)
                    {
                        target = leaves[i];
                        break;
                    }
                }
            }

            if (target != null)
            {
                engine.SetCaret(new RichPosition(target, target.Length / 2));
            }
        }

        private static void PlaceAtEnd(RichEditorEngine engine)
        {
            engine.MoveToDocumentEdge(1, false);
        }

        // =====================================================================================
        // Documents
        // =====================================================================================

        private static RichDocument Generate(Scenario scenario)
        {
            // Seeded, so every run of a scenario types into the same document.
            var random = new Random(15102026);
            var blocks = new List<RichBlock>();

            switch (scenario)
            {
                case Scenario.MessageParagraph:
                    blocks.Add(Paragraph(Text(random, 4096)));
                    break;
                case Scenario.MessageLines:
                    for (int i = 0; i < 100; i++)
                    {
                        blocks.Add(Paragraph(Text(random, 39)));
                    }
                    break;
                case Scenario.ArticleParagraph:
                    blocks.Add(Paragraph(Text(random, 100_000)));
                    break;
                case Scenario.ArticleParagraphs:
                    for (int i = 0; i < 2000; i++)
                    {
                        blocks.Add(Paragraph(Text(random, 60)));
                    }
                    break;
                case Scenario.ArticleMixed:
                    for (int section = 0, length = 0; length < 100_000; section++)
                    {
                        length += Section(random, blocks, section);
                    }
                    break;
            }

            return new RichDocument(blocks);
        }

        // A heading, paragraphs with some of their words marked, a quote, code, a list and,
        // every fifth section, a table: what an article is made of. Returns its length.
        private static int Section(Random random, List<RichBlock> blocks, int index)
        {
            var length = 0;

            blocks.Add(Paragraph(Text(random, 30), RichBlockKind.Heading));
            length += 30;

            for (int i = 0; i < 3; i++)
            {
                var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph);
                paragraph.Text.Insert(0, Text(random, 120), RichMarks.None, null);
                paragraph.Text.Insert(paragraph.Text.Length, Text(random, 30), RichMarks.Bold, null);
                paragraph.Text.Insert(paragraph.Text.Length, Text(random, 120), RichMarks.None, null);
                paragraph.Text.Insert(paragraph.Text.Length, Text(random, 30), RichMarks.Italic, null);

                blocks.Add(paragraph);
                length += paragraph.Text.Length;
            }

            blocks.Add(new RichBlockQuote(new List<RichBlock> { Paragraph(Text(random, 200)), Paragraph(Text(random, 200)) }));
            length += 400;

            var code = Paragraph("var length = 0;\nforeach (var leaf in document.Leaves)\n{\n    length += leaf.Length;\n}\nreturn length;", RichBlockKind.Preformatted);
            code.Language = "csharp";
            blocks.Add(code);
            length += code.Text.Length;

            var items = new List<RichListItem>();
            for (int i = 0; i < 3; i++)
            {
                items.Add(new RichListItem(new List<RichBlock> { Paragraph(Text(random, 80)) }));
            }

            blocks.Add(new RichListBlock(RichListStyle.Bullet, items));
            length += 240;

            if (index % 5 == 4)
            {
                var rows = new List<List<RichTableCell>>();

                for (int row = 0; row < 3; row++)
                {
                    var cells = new List<RichTableCell>();

                    for (int column = 0; column < 3; column++)
                    {
                        var cell = new RichTableCell();
                        cell.Text.Insert(0, Text(random, 20), RichMarks.None, null);
                        cells.Add(cell);
                    }

                    rows.Add(cells);
                }

                blocks.Add(new RichTableBlock(rows));
                length += 180;
            }

            return length;
        }

        private static RichParagraphBlock Paragraph(string text, RichBlockKind kind = RichBlockKind.Paragraph)
        {
            var block = new RichParagraphBlock(kind);
            block.Text.Insert(0, text, RichMarks.None, null);
            return block;
        }

        private static string Text(Random random, int length)
        {
            var builder = new StringBuilder(length + 16);

            while (builder.Length < length)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Words[random.Next(Words.Length)]);
            }

            builder.Length = length;
            return builder.ToString();
        }

        // =====================================================================================
        // Frames
        // =====================================================================================

        // The timestamp of the frame being started, once it starts.
        private static Task<long> NextFrameAsync()
        {
            return new FrameAwaiter().Completion;
        }

        private sealed class FrameAwaiter
        {
            private readonly TaskCompletionSource<long> _completion = new();

            public FrameAwaiter()
            {
                CompositionTarget.Rendering += OnRendering;
            }

            public Task<long> Completion => _completion.Task;

            private void OnRendering(object sender, object e)
            {
                CompositionTarget.Rendering -= OnRendering;
                _completion.TrySetResult(Stopwatch.GetTimestamp());
            }
        }

        private static double Milliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
