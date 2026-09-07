//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    /// <summary>
    /// What a text engine has to be, to show a date that keeps changing. The service owns the
    /// clock and the schedule; the host owns the pixels, and the two engines share nothing else
    /// about how a date is rendered.
    /// </summary>
    public interface IRelativeDateHost
    {
        XamlRoot XamlRoot { get; }

        /// <summary>
        /// The run's formatted text has just been rewritten in <paramref name="paragraph"/>.
        /// <paramref name="element"/> is the key the host subscribed with, <paramref name="segment"/>
        /// what it passed then, and <paramref name="delta"/> how much longer or shorter the
        /// text got this tick.
        /// </summary>
        void UpdateDate(object element, StyledParagraph paragraph, TextStyleRun run, int segment, string text, int delta);
    }

    public class RelativeDateService
    {
        // A dictionary value keyed by Element, so it never needs value equality - and .NET
        // Native doesn't do records anyway.
        class TextDate
        {
            public TextDate(object element, IRelativeDateHost host, StyledParagraph paragraph, TextStyleRun entity, TextEntityTypeDateTime entityType, int segment)
            {
                Element = element;
                Host = host;
                Paragraph = paragraph;
                Entity = entity;
                Date = Formatter.ToLocalTime(entityType.UnixTime);
                Segment = segment;
            }

            public object Element { get; }

            public IRelativeDateHost Host { get; }

            public StyledParagraph Paragraph { get; }

            public TextStyleRun Entity { get; }

            public DateTime Date { get; }

            // Where this date sits in the block's index map, captured when the block built
            // it. Only valid until the next SetText, which resubscribes.
            public int Segment { get; }

            public ulong NextUpdateAt { get; set; }

            public void Update()
            {
                // How much the displayed date grew or shrank THIS tick. Measuring against
                // Entity.Length - the source length - is what made the old patching wrong:
                // it is the total growth since the first render, so applying it again on
                // every tick, and once per date, compounded.
                var before = string.IsNullOrEmpty(Entity.FormattedText) ? Entity.Length : Entity.FormattedText.Length;
                var text = Entity.Update(Paragraph);
                var delta = (string.IsNullOrEmpty(text) ? Entity.Length : text.Length) - before;

                // Whatever renders it catches up its own way: the two engines have nothing in
                // common here beyond the moment.
                Host.UpdateDate(Element, Paragraph, Entity, Segment, text, delta);
            }
        }

        private readonly DispatcherTimer _timer = new();
        private readonly Dictionary<object, TextDate> _dates = new();

        private static readonly ConditionalWeakTable<XamlRoot, RelativeDateService> _instances = new();

#if NET9_0_OR_GREATER
        // The keys are XamlDirect handles owned by the blocks, and they are disposed by the
        // time this runs - so the dictionary only has to stop naming them.
        public static void Release(XamlRoot xamlRoot)
        {
            if (_instances.TryGetValue(xamlRoot, out RelativeDateService instance))
            {
                _instances.Remove(xamlRoot);

                instance._timer.Stop();
                instance._timer.Tick -= instance.OnTick;
                instance._dates.Clear();
            }
        }
#endif

        private RelativeDateService()
        {
            _timer.Tick += OnTick;
        }

        private void OnTick(object sender, object e)
        {
            _timer.Stop();

            _timer.Interval = GetNextUpdateInterval(_dates.Values, true);
            _timer.Start();
        }

        public static void Subscribe(object element, IRelativeDateHost host, StyledParagraph paragraph, TextStyleRun run, TextEntityTypeDateTime entity, int segment)
        {
            Debug.Assert(host.XamlRoot != null);

            _instances.TryGetValue(host.XamlRoot, out RelativeDateService instance);

            if (instance == null)
            {
                _instances.Add(host.XamlRoot, instance = new());
            }

            instance.SubscribeImpl(element, host, paragraph, run, entity, segment);
        }

        private void SubscribeImpl(object element, IRelativeDateHost host, StyledParagraph paragraph, TextStyleRun run, TextEntityTypeDateTime entity, int segment)
        {
            // Replaces rather than skips. The key is a Run from the shared pool, so the same
            // object comes back around attached to a different block, and a registration
            // that outlived its block would otherwise make that Run unsubscribable - its new
            // date silently never updating - for the rest of the session.
            _dates[element] = new TextDate(element, host, paragraph, run, entity, segment);
            _timer.Stop();

            _timer.Interval = GetNextUpdateInterval(_dates.Values, false);
            _timer.Start();
        }

        public static void Unsubscribe(object element, XamlRoot xamlRoot)
        {
            if (_instances.TryGetValue(xamlRoot, out var instance))
            {
                instance.UnsubscribeImpl(element);
            }
        }

        private void UnsubscribeImpl(object element)
        {
            if (_dates.ContainsKey(element))
            {
                _dates.Remove(element);
                _timer.Stop();

                if (_dates.Count > 0)
                {
                    _timer.Interval = GetNextUpdateInterval(_dates.Values, false);
                    _timer.Start();
                }
            }
        }

        private static TimeSpan GetNextUpdateInterval(IEnumerable<TextDate> dates, bool invalidate)
        {
            var minSeconds = int.MaxValue;

            var tickCount = Logger.TickCount;
            var currentTime = DateTime.Now;

            foreach (var item in dates)
            {
                var shouldReschedule = !invalidate;

                if (invalidate || item.NextUpdateAt == 0)
                {
                    if (item.NextUpdateAt <= tickCount)
                    {
                        shouldReschedule = true;
                        item.Update();
                    }
                }

                if (shouldReschedule)
                {
                    var nextForThisItem = GetNextUpdateIntervalSeconds(currentTime, item.Date);

                    // Each item gets its own update time
                    item.NextUpdateAt = tickCount + (ulong)(nextForThisItem * 1000);

                    // Track the global minimum for timer interval
                    if (nextForThisItem < minSeconds)
                    {
                        minSeconds = nextForThisItem;
                    }
                }
                else
                {
                    // Item doesn't need rescheduling, but still consider its existing schedule.
                    // Round up, never down to zero: an item due in under a second used to be
                    // dropped from the minimum entirely, and if every item was in that state
                    // - which is the norm for the one-second bucket, where the timer can fire
                    // a hair early - nothing set the minimum and the next tick was scheduled
                    // int.MaxValue seconds out.
                    var remainingSeconds = ((long)(item.NextUpdateAt - tickCount) + 999) / 1000;
                    if (remainingSeconds > 0 && remainingSeconds < minSeconds)
                    {
                        minSeconds = (int)remainingSeconds;
                    }
                }
            }

            // An empty set leaves the minimum untouched; a second is the shortest the
            // buckets below ever ask for anyway.
            return TimeSpan.FromSeconds(minSeconds == int.MaxValue ? 1 : minSeconds);
        }

        private static int GetNextUpdateIntervalSeconds(DateTime currentTime, DateTime relativeTime)
        {
            TimeSpan difference = currentTime - relativeTime;
            bool isPast = difference.TotalSeconds > 0;
            double absDifference = Math.Abs(difference.TotalSeconds);

            if (absDifference < 60)
            {
                return 1;
            }
            else if (absDifference < 3600)
            {
                double secondsPastMinute = absDifference % 60;

                if (isPast)
                {
                    return (int)Math.Ceiling(60 - secondsPastMinute);
                }
                else
                {
                    return (int)Math.Ceiling(secondsPastMinute);
                }
            }
            else if (absDifference < 86400)
            {
                double secondsPastHour = absDifference % 3600;

                if (isPast)
                {
                    return (int)Math.Ceiling(3600 - secondsPastHour);
                }
                else
                {
                    return (int)Math.Ceiling(secondsPastHour);
                }
            }
            else
            {
                double secondsPastDay = absDifference % 86400;

                if (isPast)
                {
                    return (int)Math.Ceiling(86400 - secondsPastDay);
                }
                else
                {
                    return (int)Math.Ceiling(secondsPastDay);
                }
            }
        }
    }
}
