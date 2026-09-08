//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Diagnostics;
using Telegram.Services;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    // What a text block costs in the running app: the call that gives it its text, and the two
    // layout passes. Both engines are counted the same way and from the same three places,
    // because the question is which of them is cheaper - and only one of them is alive in a
    // process, so the comparison is between two runs of the same chat rather than two columns.
    //
    // A runtime switch rather than [Conditional], for the reason TdThroughput gives: the build
    // whose numbers mean anything is the shipping one, which no debug define reaches. Off it
    // costs a static read per call, on it two timestamps.
    public static class TextThroughput
    {
        // Read at type init rather than assigned at startup: nothing here runs before the first
        // block is built anyway, and the setter keeps it in step from there.
        public static volatile bool Enabled = AppSettings.Diagnostics.MeasureTextLayout;

        // Written from every UI thread there is and read from one, without interlocks: a second
        // window can lose a call to a race, and a meter is not worth a locked bus cycle per
        // layout pass. The peak is what a single message did, which an average hides.
        public struct Counter
        {
            public long Calls;
            public long Ticks;
            public long Peak;

            public readonly double Seconds => Ticks / (double)Stopwatch.Frequency;

            public readonly double PeakSeconds => Peak / (double)Stopwatch.Frequency;
        }

        public static Counter DirectSetText;
        public static Counter DirectMeasure;
        public static Counter DirectArrange;

        // How many blocks were built, and how many were torn down and why. A block that
        // survives a recycle keeps its DirectWrite layout and the region of the atlas it draws
        // into; one that does not pays for both again, and the difference between these two
        // numbers is how much of that recycling is actually saving.
        public static long BlocksMade;
        public static long BlocksCleared;
        public static long BlocksClearedComplex;

        // Of the ones that were not a quote or a code block: how many had nothing to tear down
        // because the host had never built any - a container that is new rather than recycled -
        // against how many threw away blocks that were there.
        public static long BlocksClearedEmpty;

        // Every block built, wherever it came from: a message, a page block in an instant view
        // preview, the label of an inline button. Against BlocksMade, which counts only the
        // ones a message asked for, it says how many of the layouts and surfaces belong to
        // something else entirely.
        public static long ControlsMade;

        // Layouts, which is what a surface belongs to: one is built the first time a block is
        // measured and lives until the block is cleared. Against the number of surfaces the
        // native side creates, it says whether the surfaces are going with their layouts or
        // being given up for some other reason.
        public static long LayoutsMade;
        public static long LayoutsDisposed;

        // Hosts that reached the text with no blocks in hand. If this is close to the number of
        // messages scrolled past, nothing is being recycled above this control and there is
        // nothing to fix down here.
        public static long HostsMade;

        // The three things giving a block its text can spend real time on: a player per custom
        // emoji, a control per inline button, and a parse per formula. The rest of it is
        // reading entities into ranges.
        public static Counter DirectEmoji;
        public static Counter DirectButtons;
        public static Counter DirectMath;

        // The part of the measure pass that is the layout itself - one call per Measure the
        // block makes, which is more than one per pass. What is left is the block's own
        // bookkeeping: the buttons and emoji it measures, and the questions it asks the layout
        // afterwards.
        public static Counter DirectLayout;

        // The part of the draw that is the layout's own: the extent of the text, the surface
        // it goes into, and the drawing itself. What is left over is this control's - sizing
        // the visual it hangs on, and moving the selection and the search highlight with it.
        public static Counter DirectSurface;

        // The part of the arrange pass that is the draw into the surface. The inline engine has
        // no counterpart: XAML rasterizes its text in the render pass, where nothing here can
        // see it - so this is the number that says what the surface costs, not a comparison.
        public static Counter DirectRender;

        // The text work that lands between two frames, which is the only form of these numbers
        // that says whether anything stuttered: a block costing half a millisecond is nothing,
        // and thirty of them in one frame is a frame that did not happen. Calls counts every
        // frame, Ticks the text work in all of them, Peak the worst single frame.
        public static Counter Frame;

        // How many frames had any text work in them at all, and how many blocks the worst one
        // carried - which is what says whether a peak is one bad message or a pile-up.
        public static long BusyFrames;
        public static long PeakFrameBlocks;

        // The display's own period: the gap most frames are apart, which is what a frame is
        // worth - 16.7 ms on one machine and 4.2 on the next, and the whole point of these
        // numbers is what fits in one.
        //
        // The most common gap rather than the shortest: two windows composing, or two raises
        // inside one frame, produce gaps no display ever has, and a minimum takes the worst of
        // them as the truth. Buckets of a quarter of a millisecond, and the mode is kept as it
        // goes rather than searched for.
        private static readonly long[] _periods = new long[128];
        private static int _period;

        public static long FramePeriod => (long)((_period + 0.5) * Stopwatch.Frequency / 4000);

        // Frames where the text alone took longer than that - not proof of a dropped frame,
        // since the rest of the interface was doing something too, but the count of frames this
        // engine could have dropped on its own.
        public static long LateFrames;

        private static long _frameTicks;
        private static long _frameBlocks;
        private static long _frameLast;
        private static bool _ticking;

        /// <summary>
        /// The timestamp to pass back to <see cref="Record"/>, or 0 when measuring is off -
        /// which is what makes Record a branch rather than a call.
        /// </summary>
        public static long Begin()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        /// <summary>
        /// <paramref name="frame"/> for the three calls a block makes rather than the parts of
        /// them: the parts are inside those, and counting both would count the work twice.
        /// </summary>
        public static void Record(ref Counter counter, long started, bool frame = false)
        {
            if (started == 0)
            {
                return;
            }

            var elapsed = Stopwatch.GetTimestamp() - started;

            counter.Calls++;
            counter.Ticks += elapsed;

            if (elapsed > counter.Peak)
            {
                counter.Peak = elapsed;
            }

            if (frame)
            {
                _frameTicks += elapsed;
                _frameBlocks++;

                EnsureTicker();
            }
        }

        // Subscribed on the first block that measures anything rather than when the flag is
        // set: the flag is set on the settings page's thread, and the frames worth counting are
        // the ones the chat is on. A static event and a static handler, so there is nothing
        // here for it to hold.
        //
        // Rendering fires for as long as anything is subscribed, which keeps the interface
        // ticking at the display's rate whether or not it has anything to draw - so this stays
        // off unless the flag is on, and the flag is not something to leave on.
        private static void EnsureTicker()
        {
            if (_ticking)
            {
                return;
            }

            _ticking = true;
            CompositionTarget.Rendering += OnRendering;
        }

        private static void OnRendering(object sender, object e)
        {
            var now = Stopwatch.GetTimestamp();

            if (_frameLast != 0)
            {
                var bucket = (int)((now - _frameLast) * 4000 / Stopwatch.Frequency);

                if (bucket >= 0 && bucket < _periods.Length)
                {
                    _periods[bucket]++;

                    if (_periods[bucket] > _periods[_period])
                    {
                        _period = bucket;
                    }
                }
            }

            _frameLast = now;

            Frame.Calls++;
            Frame.Ticks += _frameTicks;

            if (_frameTicks > Frame.Peak)
            {
                Frame.Peak = _frameTicks;
                PeakFrameBlocks = _frameBlocks;
            }

            if (_frameBlocks > 0)
            {
                BusyFrames++;

                if (_frameTicks > FramePeriod)
                {
                    LateFrames++;
                }
            }

            _frameTicks = 0;
            _frameBlocks = 0;
        }

        public static void Reset()
        {
            DirectSetText = default;
            DirectMeasure = default;
            DirectArrange = default;
            BlocksMade = 0;
            BlocksCleared = 0;
            BlocksClearedComplex = 0;
            BlocksClearedEmpty = 0;
            HostsMade = 0;

            ControlsMade = 0;

            LayoutsMade = 0;
            LayoutsDisposed = 0;


            DirectEmoji = default;
            DirectButtons = default;
            DirectMath = default;
            DirectLayout = default;
            DirectRender = default;
            DirectSurface = default;

            Frame = default;
            BusyFrames = 0;
            PeakFrameBlocks = 0;
            LateFrames = 0;

            _frameTicks = 0;
            _frameBlocks = 0;
            _frameLast = 0;
            _period = 0;

            Array.Clear(_periods);
        }
    }
}
