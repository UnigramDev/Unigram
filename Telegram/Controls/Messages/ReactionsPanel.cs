//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;

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
            TabFocusNavigation = KeyboardNavigationMode.Once;

            ChildrenTransitions = new TransitionCollection
            {
                new RepositionThemeTransition()
            };
        }

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
                    .Select(x => x.Type)
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
                    var diff = DiffUtil.CalculateDiff(prev, reactions.Reactions, this, Constants.DiffOptions);

                    foreach (var step in diff.Steps)
                    {
                        if (step.Status == DiffStatus.Add)
                        {
                            UpdateItem(step.Items[0].NewValue, null, step.NewStartIndex);
                        }
                        else if (step.Status == DiffStatus.Move && step.OldStartIndex < Children.Count && step.NewStartIndex < Children.Count)
                        {
                            UpdateItem(step.Items[0].OldValue, step.Items[0].NewValue);
                            Children.Move((uint)step.OldStartIndex, (uint)step.NewStartIndex);
                        }
                        else if (step.Status == DiffStatus.Remove && step.OldStartIndex < Children.Count)
                        {
                            if (step.Items[0].OldValue is MessageReaction oldReaction)
                            {
                                _cache.Remove(oldReaction.Type);
                            }

                            Children.RemoveAt(step.OldStartIndex);
                        }
                    }

                    foreach (var item in diff.NotMovedItems)
                    {
                        UpdateItem(item.OldValue, item.NewValue);
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

        protected override Size MeasureOverride(Size availableSize)
        {
            var totalMeasure = new Size();
            var parentMeasure = new Size(availableSize.Width, availableSize.Height);
            var lineMeasure = new Size(Padding.Left, 0);
            var count = 0;

            void Measure(double currentWidth, double currentHeight)
            {
                if (parentMeasure.Width > currentWidth + lineMeasure.Width)
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
                    if (parentMeasure.Width > currentWidth)
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

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                Measure(child.DesiredSize.Width, child.DesiredSize.Height);

                count++;
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
            var parentMeasure = new Size(finalSize.Width, finalSize.Height);
            var position = new Size(Padding.Left, Padding.Top);
            var count = 0;

            double currentV = 0;
            foreach (var child in Children)
            {
                var desiredMeasure = new Size(child.DesiredSize.Width, child.DesiredSize.Height);
                if ((desiredMeasure.Width + position.Width) > parentMeasure.Width)
                {
                    // next row!
                    position.Width = Padding.Left;
                    position.Height += currentV + Spacing;
                    currentV = 0;
                }

                // Place the item
                child.Arrange(new Rect(position.Width, position.Height, child.DesiredSize.Width, child.DesiredSize.Height));

                // adjust the location for the next items
                position.Width += desiredMeasure.Width + Spacing;
                currentV = Math.Max(desiredMeasure.Height, currentV);
                count++;
            }

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
