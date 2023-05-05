//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Controls.Messages
{
    public partial class ReactionsPanel : Panel, IDiffEqualityComparer<MessageReaction>
    {
        private readonly Dictionary<string, WeakReference> _reactions = new();
        private readonly Dictionary<long, WeakReference> _customReactions = new();

        private long _chatId;
        private long _messageId;

        private MessageReaction[] _prevValue;

        public ReactionsPanel()
        {
            ChildrenTransitions = new TransitionCollection
            {
                new RepositionThemeTransition()
            };
        }

        public bool HasReactions => _reactions.Count > 0 || _customReactions.Count > 0;

        public async void UpdateMessageReactions(MessageViewModel message, bool animate = false)
        {
            var reactions = message?.InteractionInfo?.Reactions;
            if (reactions == null || reactions.Count == 0 || message?.ChatId != _chatId || message?.Id != _messageId)
            {
                _prevValue = null;

                _reactions.Clear();
                _customReactions.Clear();

                Children.Clear();
            }

            if (reactions?.Count > 0)
            {
                List<long> missingCustomEmoji = null;
                List<string> missingEmoji = null;

                message.UnreadReactions
                    .Select(x => x.Type)
                    .Discern(out var unreadEmoji, out var unreadCustomEmoji);

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

                    if (oldItem.Type is ReactionTypeEmoji emoji)
                    {
                        var required = UpdateButton<string, EmojiReaction>(_reactions, emoji.Emoji, message, oldItem, message.ClientService.TryGetCachedReaction, changed, index);
                        if (required)
                        {
                            missingEmoji ??= new List<string>();
                            missingEmoji.Add(emoji.Emoji);
                        }
                    }
                    else if (oldItem.Type is ReactionTypeCustomEmoji customEmoji)
                    {
                        var required = UpdateButton<long, Sticker>(_customReactions, customEmoji.CustomEmojiId, message, oldItem, EmojiCache.TryGet, changed, index);
                        if (required)
                        {
                            missingCustomEmoji ??= new List<long>();
                            missingCustomEmoji.Add(customEmoji.CustomEmojiId);
                        }
                    }
                }

                var prev = _prevValue ?? Array.Empty<MessageReaction>();
                var diff = DiffUtil.CalculateDiff(prev, reactions, this, Constants.DiffOptions);

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
                            if (oldReaction.Type is ReactionTypeEmoji oldEmoji)
                            {
                                _reactions.Remove(oldEmoji.Emoji);
                            }
                            else if (oldReaction.Type is ReactionTypeCustomEmoji oldCustomEmoji)
                            {
                                _customReactions.Remove(oldCustomEmoji.CustomEmojiId);
                            }
                        }

                        Children.RemoveAt(step.OldStartIndex);
                    }
                }

                foreach (var item in diff.NotMovedItems)
                {
                    UpdateItem(item.OldValue, item.NewValue);
                }

                _chatId = message?.ChatId ?? 0;
                _messageId = message?.Id ?? 0;

                _prevValue = reactions?.ToArray();

                if (missingCustomEmoji != null)
                {
                    var response = await message.ClientService.SendAsync(new GetCustomEmojiStickers(missingCustomEmoji));
                    if (response is Stickers stickers)
                    {
                        foreach (var sticker in stickers.StickersValue)
                        {
                            if (sticker.FullType is not StickerFullTypeCustomEmoji customEmoji)
                            {
                                continue;
                            }

                            EmojiCache.AddOrUpdate(sticker);
                            UpdateButton<long, Sticker>(_customReactions, customEmoji.CustomEmojiId, message, sticker, Animate);
                        }
                    }
                }

                if (missingEmoji != null)
                {
                    foreach (var emoji in missingEmoji)
                    {
                        var response = await message.ClientService.SendAsync(new GetEmojiReaction(emoji));
                        if (response is EmojiReaction reaction)
                        {
                            UpdateButton<string, EmojiReaction>(_reactions, emoji, message, reaction, Animate);
                        }
                    }
                }
            }
        }

        delegate bool TryGetValue<TKey, T>(TKey key, out T value);

        private void UpdateButton<T, TValue>(IDictionary<T, WeakReference> cache, T key, MessageViewModel message, object value, Func<ReactionType, bool> animate)
        {
            if (cache.TryGetValue(key, out WeakReference reference)
                && reference.Target is ReactionButton button)
            {
                if (value is EmojiReaction reaction)
                {
                    button.SetReaction(message, button.Reaction, reaction);
                }
                else if (value is Sticker sticker)
                {
                    button.SetReaction(message, button.Reaction, sticker);
                }

                if (animate(button.Reaction.Type))
                {
                    button.SetUnread(new UnreadReaction(button.Reaction.Type, null, false));
                }
            }
        }

        private bool UpdateButton<T, TValue>(IDictionary<T, WeakReference> cache, T key, MessageViewModel message, MessageReaction item, TryGetValue<T, TValue> tryGet, bool animate, int index)
        {
            var required = false;

            var button = GetOrCreateButton(cache, key, message, index);
            if (button.EmojiReaction != null)
            {
                button.SetReaction(message, item, button.EmojiReaction);
            }
            else if (button.CustomReaction != null)
            {
                button.SetReaction(message, item, button.CustomReaction);
            }
            else if (tryGet(key, out TValue value))
            {
                if (value is EmojiReaction reaction)
                {
                    button.SetReaction(message, item, reaction);
                }
                else if (value is Sticker sticker)
                {
                    button.SetReaction(message, item, sticker);
                }
            }
            else
            {
                button.SetReaction(message, item, null as EmojiReaction);

                animate = false;
                required = true;
            }

            if (animate)
            {
                button.SetUnread(new UnreadReaction(item.Type, null, false));
            }

            return required;
        }

        private ReactionButton GetOrCreateButton<T>(IDictionary<T, WeakReference> cache, T key, MessageViewModel message, int index)
        {
            if (cache.TryGetValue(key, out WeakReference reference)
                && reference.Target is ReactionButton button)
            {
                return button;
            }

            button = new ReactionButton();
            cache[key] = new WeakReference(button);
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
            Logger.Debug();

            var totalMeasure = new Size();
            var parentMeasure = new Size(availableSize.Width, availableSize.Height);
            var lineMeasure = new Size(Padding.Left, 0);

            void Measure(Size currentMeasure)
            {
                if (parentMeasure.Width > currentMeasure.Width + lineMeasure.Width)
                {
                    lineMeasure.Width += currentMeasure.Width + Spacing;
                    lineMeasure.Height = Math.Max(lineMeasure.Height, currentMeasure.Height);
                }
                else
                {
                    // new line should be added
                    // to get the max U to provide it correctly to ui width ex: ---| or -----|
                    totalMeasure.Width = Math.Max(lineMeasure.Width - Spacing, totalMeasure.Width);
                    totalMeasure.Height += lineMeasure.Height + Spacing;

                    // if the next new row still can handle more controls
                    if (parentMeasure.Width > currentMeasure.Width)
                    {
                        // set lineMeasure initial values to the currentMeasure to be calculated later on the new loop
                        lineMeasure = currentMeasure;
                    }

                    // the control will take one row alone
                    else
                    {
                        // validate the new control measures
                        totalMeasure.Width = Math.Max(currentMeasure.Width, totalMeasure.Width);
                        totalMeasure.Height += currentMeasure.Height + Spacing;

                        // add new empty line
                        lineMeasure = new Size(Padding.Left, 0);
                    }
                }
            }

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                Measure(new Size(child.DesiredSize.Width, child.DesiredSize.Height));
            }

            if (Children.Count > 0)
            {
                Measure(Footer);

                // update value with the last line
                // if the the last loop is(parentMeasure.U > currentMeasure.U + lineMeasure.U) the total isn't calculated then calculate it
                // if the last loop is (parentMeasure.U > currentMeasure.U) the currentMeasure isn't added to the total so add it here
                // for the last condition it is zeros so adding it will make no difference
                // this way is faster than an if condition in every loop for checking the last item
                totalMeasure.Width = Math.Max(lineMeasure.Width - Spacing, totalMeasure.Width) + Padding.Right;
                totalMeasure.Height += lineMeasure.Height + Padding.Bottom + Padding.Top;
            }

            return new Size(totalMeasure.Width, totalMeasure.Height);
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            Logger.Debug();

            var parentMeasure = new Size(finalSize.Width, finalSize.Height);
            var position = new Size(Padding.Left, Padding.Top);
            var count = 1;

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
}
