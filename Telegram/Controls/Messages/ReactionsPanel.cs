//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Collections;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Controls.Messages
{
    public partial class ReactionsPanel : Panel, IDiffEqualityComparer<MessageReaction>
    {
        private readonly Dictionary<ReactionType, ReactionButton> _cache = new(new ReactionTypeEqualityComparer());

        private long _chatId;
        private long _messageId;

        private MessageReaction[] _prevValue;
        private bool _prevAsTags;

        public ReactionsPanel()
        {
            Telegram.Common.Instrumentation.Register(this);

            TabFocusNavigation = KeyboardNavigationMode.Once;

            ChildrenTransitions = new TransitionCollection
            {
                new RepositionThemeTransition()
            };

            ElementComposition.GetElementVisual(this);
        }

#if INSTRUMENTATION
        internal IEnumerable<object> DebugChildren() => _cache.Values;
#endif

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ReactionsPanelAutomationPeer(this);
        }

        public bool HasReactions => _cache.Count > 0;

        public void UpdateMessageReactions(MessageViewModel message, bool animate = false)
        {
            var reactions = message?.InteractionInfo?.Reactions;
            if (reactions == null || reactions.AreTags != _prevAsTags || message?.ChatId != _chatId || message?.Id != _messageId)
            {
                _prevValue = null;

                _cache.Clear();
                Children.Clear();
            }

            if (reactions?.Reactions.Count > 0)
            {
                if (Padding.Bottom > 0)
                {
                    Padding = new Thickness(reactions.AreTags ? 8 : 4, 0, 4, 4);
                }

                message.UnreadReactions
                    .Discern(out bool paid, out var unreadEmoji, out var unreadCustomEmoji);

                bool Animate(ReactionType reaction)
                {
                    if (reaction is ReactionTypeEmoji emoji)
                    {
                        return animate
                            && unreadEmoji != null
                            && unreadEmoji.Contains(emoji.Emoji);
                    }
                    else if (reaction is ReactionTypeCustomEmoji customEmoji)
                    {
                        return animate
                            && unreadCustomEmoji != null
                            && unreadCustomEmoji.Contains(customEmoji.CustomEmojiId);
                    }
                    else if (reaction is ReactionTypePaid)
                    {
                        return animate && paid;
                    }

                    return false;
                }

                void UpdateItem(MessageReaction oldItem, MessageReaction newItem, int index = 0)
                {
                    if (newItem != null)
                    {
                        oldItem.IsChosen = newItem.IsChosen;
                        oldItem.RecentSenderIds = newItem.RecentSenderIds;
                        oldItem.TotalCount = newItem.TotalCount;
                    }

                    var changed = Animate(oldItem.Type);
                    UpdateButton(message, oldItem, reactions.AreTags, changed, index);
                }

                if (_prevValue == null)
                {
                    for (int i = 0; i < reactions.Reactions.Count; i++)
                    {
                        UpdateItem(reactions.Reactions[i], null, i);
                    }
                }
                else
                {
                    // PERF: run diff asynchronously?
                    var prev = _prevValue ?? Array.Empty<MessageReaction>();
                    var diff = DiffCalculator.Create(prev, reactions.Reactions, this);

                    while (diff.Next())
                    {
                        if (diff.State == DiffState.Add)
                        {
                            UpdateItem(diff.NewValue, null, diff.NewIndex);
                        }
                        else if (diff.State == DiffState.Move && diff.OldIndex < Children.Count && diff.NewIndex < Children.Count)
                        {
                            UpdateItem(diff.OldValue, diff.NewValue);
                            Children.Move((uint)diff.OldIndex, (uint)diff.NewIndex);
                        }
                        else if (diff.State == DiffState.Remove && diff.OldIndex < Children.Count)
                        {
                            if (diff.OldValue is MessageReaction oldReaction)
                            {
                                _cache.Remove(oldReaction.Type);
                            }

                            Children.RemoveAt(diff.OldIndex);
                        }
                        else if (diff.State == DiffState.Unchanged)
                        {
                            UpdateItem(diff.OldValue, diff.NewValue);
                        }
                    }
                }

                _chatId = message?.ChatId ?? 0;
                _messageId = message?.Id ?? 0;

                _prevValue = reactions?.Reactions.ToArray();
                _prevAsTags = reactions?.AreTags ?? false;
            }
        }

        private void UpdateButton(MessageViewModel message, MessageReaction item, bool isTag, bool animate, int index)
        {
            var button = GetOrCreateButton(item.Type, isTag, index);
            button.SetReaction(message, item);

            if (animate)
            {
                button.SetUnread(new UnreadReaction(item.Type, null, false));
            }
        }

        private ReactionButton GetOrCreateButton(ReactionType key, bool isTag, int index)
        {
            if (_cache.TryGetValue(key, out ReactionButton button))
            {
                return button;
            }

            button = isTag
                ? new ReactionAsTagButton()
                : key is ReactionTypePaid
                ? new ReactionAsPaidButton()
                : new ReactionButton();

            _cache[key] = button;
            Children.Insert(Math.Min(index, Children.Count), button);

            return button;
        }

        public bool CompareItems(MessageReaction oldItem, MessageReaction newItem)
        {
            if (oldItem != null)
            {
                return oldItem.Type.AreTheSame(newItem?.Type);
            }

            return false;
        }

        private const double Spacing = 4;

        public Thickness Padding { get; set; }

        public Size Footer { get; set; }

        public HorizontalAlignment HorizontalContentAlignment { get; set; } = HorizontalAlignment.Left;

        protected override Size MeasureOverride(Size availableSize)
        {
            var totalMeasure = new Size();
            var lineMeasure = new Size(Padding.Left, 0);
            var count = 0;

            void Measure(double currentWidth, double currentHeight)
            {
                if (availableSize.Width > currentWidth + lineMeasure.Width)
                {
                    lineMeasure.Width += currentWidth + Spacing;
                    lineMeasure.Height = Math.Max(lineMeasure.Height, currentHeight);
                }
                else
                {
                    // new line should be added
                    // to get the max U to provide it correctly to ui width ex: ---| or -----|
                    totalMeasure.Width = Math.Max(lineMeasure.Width - Spacing, totalMeasure.Width);
                    totalMeasure.Height += lineMeasure.Height + Spacing;

                    // if the next new row still can handle more controls
                    if (availableSize.Width > currentWidth)
                    {
                        // set lineMeasure initial values to the currentMeasure to be calculated later on the new loop
                        lineMeasure.Width = currentWidth;
                        lineMeasure.Height = currentHeight;
                    }

                    // the control will take one row alone
                    else
                    {
                        // validate the new control measures
                        totalMeasure.Width = Math.Max(currentWidth, totalMeasure.Width);
                        totalMeasure.Height += currentHeight + Spacing;

                        // add new empty line
                        lineMeasure = new Size(Padding.Left, 0);
                    }
                }
            }

            // Indexed for the same reason as the arrange pass below: foreach over
            // UIElementCollection allocates an IEnumerator<UIElement> per pass.
            count = Children.Count;

            for (int i = 0; i < count; i++)
            {
                var child = Children[i];

                child.Measure(availableSize);
                Measure(child.DesiredSize.Width, child.DesiredSize.Height);
            }

            if (count > 0)
            {
                var footerWidth = Math.Max(Footer.Width - 8, 0);
                var footerHeight = Footer.Height;

                Measure(footerWidth, footerHeight);

                // update value with the last line
                // if the the last loop is(parentMeasure.U > currentMeasure.U + lineMeasure.U) the total isn't calculated then calculate it
                // if the last loop is (parentMeasure.U > currentMeasure.U) the currentMeasure isn't added to the total so add it here
                // for the last condition it is zeros so adding it will make no difference
                // this way is faster than an if condition in every loop for checking the last item
                totalMeasure.Width = Math.Max(lineMeasure.Width - Spacing, totalMeasure.Width) + Padding.Right;
                totalMeasure.Height += lineMeasure.Height + Padding.Bottom + Padding.Top;
            }

            if (count > 0)
            {
                return new Size(totalMeasure.Width, totalMeasure.Height);
            }

            return new Size(0, 0);
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            var position = new Size(Padding.Left, Padding.Top);

            var center = HorizontalContentAlignment != HorizontalAlignment.Left;

            // A row is a contiguous run of children, complete as soon as the next child
            // does not fit, at which point its right edge is known. Centring therefore
            // needs no buffer: each run is arranged in place, walking its children a
            // second time to recover their offsets. Buffering the rects instead cost a
            // List per row on every pass.
            var rowStart = 0;
            var rowTop = position.Height;

            // Local function, not a delegate: nothing converts it, so the capture stays
            // on the stack.
            void ArrangeRow(int from, int to, double y, double right)
            {
                var offset = center
                    ? HorizontalContentAlignment == HorizontalAlignment.Center
                        ? (finalSize.Width - right) / 2
                        : finalSize.Width - right
                    : 0;

                var left = Padding.Left;

                for (int i = from; i < to; i++)
                {
                    var child = Children[i];
                    child.Arrange(new Rect(left + offset, y, child.DesiredSize.Width, child.DesiredSize.Height));

                    left += child.DesiredSize.Width + Spacing;
                }
            }

            double currentV = 0;

            // Indexed: foreach over UIElementCollection goes through
            // IEnumerator<UIElement> and allocates one per pass.
            var count = Children.Count;

            for (int index = 0; index < count; index++)
            {
                var desiredMeasure = Children[index].DesiredSize;
                if ((desiredMeasure.Width + position.Width) > finalSize.Width)
                {
                    // Next row. Spacing was added after the last child of the completed
                    // row, so subtracting it gives that row's right edge.
                    ArrangeRow(rowStart, index, rowTop, position.Width - Spacing);

                    rowStart = index;
                    position.Width = Padding.Left;
                    position.Height += currentV + Spacing;
                    rowTop = position.Height;
                    currentV = 0;
                }

                // adjust the location for the next items
                position.Width += desiredMeasure.Width + Spacing;
                currentV = Math.Max(desiredMeasure.Height, currentV);
            }

            ArrangeRow(rowStart, count, rowTop, position.Width - Spacing);

            return finalSize;
        }
    }

    public partial class ReactionsPanelAutomationPeer : FrameworkElementAutomationPeer
    {
        public ReactionsPanelAutomationPeer(ReactionsPanel owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.List;
        }
    }
}
