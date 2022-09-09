using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Controls.Messages
{
    public partial class ReactionsPanel : Panel
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

        public async void UpdateMessageReactions(MessageViewModel message, bool? animate = false)
        {
            if (message?.ChatId != _chatId || message?.Id != _messageId)
            {
                _prevValue = null;

                _reactions.Clear();
                _customReactions.Clear();

                Children.Clear();
            }

            var reactions = message?.InteractionInfo?.Reactions;
            if (reactions != null)
            {
                var customEmojiIds = new Dictionary<long, MessageReaction>();
                var emojiIds = new Dictionary<string, MessageReaction>();

                var unread = message.UnreadReactions;

                foreach (var item in reactions)
                {
                    if (item.Type is ReactionTypeEmoji emoji)
                    {
                        var required = UpdateButton<string, Reaction>(_reactions, emoji.Emoji, message, item, message.ProtoService.TryGetCachedReaction, animate);
                        if (required)
                        {
                            emojiIds[emoji.Emoji] = item;
                        }
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmoji)
                    {
                        var required = UpdateButton<long, Sticker>(_customReactions, customEmoji.CustomEmojiId, message, item, EmojiRendererCache.TryGetValue, animate);
                        if (required)
                        {
                            customEmojiIds[customEmoji.CustomEmojiId] = item;
                        }
                    }
                }

                if (_prevValue != null)
                {
                    var prev = _prevValue;
                    var diff = Rg.DiffUtils.DiffUtil.CalculateDiff(prev, reactions, new ReactionDiffHandler(), new Rg.DiffUtils.DiffOptions { AllowBatching = false, DetectMoves = true });

                    foreach (var step in diff.Steps)
                    {
                        if (step.Status == Rg.DiffUtils.DiffStatus.Move && step.OldStartIndex < Children.Count && step.NewStartIndex < Children.Count)
                        {
                            Children.Move((uint)step.OldStartIndex, (uint)step.NewStartIndex);
                        }
                        else if (step.Status == Rg.DiffUtils.DiffStatus.Remove)
                        {
                            Children.RemoveAt(step.OldStartIndex);
                        }
                    }
                }

                var response = await message.ProtoService.SendAsync(new GetCustomEmojiStickers(customEmojiIds.Keys.ToArray()));
                if (response is Stickers stickers)
                {
                    foreach (var sticker in stickers.StickersValue)
                    {
                        if (customEmojiIds.TryGetValue(sticker.CustomEmojiId, out MessageReaction item)
                            && _customReactions.TryGetValue(sticker.CustomEmojiId, out WeakReference reference)
                            && reference.Target is ReactionButton button)
                        {
                            button.SetReaction(message, item, sticker);

                            if (animate is true && item.IsChosen)
                            {
                                button.SetUnread(new UnreadReaction(item.Type, null, false));
                            }
                        }
                    }
                }

                if (animate is null && unread != null)
                {
                    foreach (var item in unread)
                    {
                        if (item.Type is ReactionTypeEmoji emoji
                            && _reactions.TryGetValue(emoji.Emoji, out WeakReference reference)
                            && reference.Target is ReactionButton button)
                        {
                            button.SetUnread(item);
                        }
                        else if (item.Type is ReactionTypeCustomEmoji customEmoji
                            && _customReactions.TryGetValue(customEmoji.CustomEmojiId, out WeakReference customReference)
                            && customReference.Target is ReactionButton customButton)
                        {
                            customButton.SetUnread(item);
                        }
                    }
                }
            }

            _chatId = message?.ChatId ?? 0;
            _messageId = message?.Id ?? 0;

            _prevValue = reactions?.ToArray();
        }

        delegate bool TryGetValue<TKey, T>(TKey key, out T value);
        delegate void SetReaction<T>(MessageViewModel message, MessageReaction item, T reaction);

        private bool UpdateButton<T, TValue>(IDictionary<T, WeakReference> cache, T key, MessageViewModel message, MessageReaction item, TryGetValue<T, TValue> tryGet, bool? animate)
        {
            var required = false;
            if (false == tryGet(key, out TValue value))
            {
                required = true;
            }

            var button = GetOrCreateButton(cache, key, message, item);

            if (value != null)
            {
                if (value is Reaction reaction)
                {
                    button.SetReaction(message, item, reaction);
                }
                else if (value is Sticker sticker)
                {
                    button.SetReaction(message, item, sticker);
                }

                if (animate is true && item.IsChosen)
                {
                    button.SetUnread(new UnreadReaction(item.Type, null, false));
                }
            }

            return required;
        }

        private ReactionButton GetOrCreateButton<T>(IDictionary<T, WeakReference> cache, T key, MessageViewModel message, MessageReaction item)
        {
            if (cache.TryGetValue(key, out WeakReference reference)
                && reference.Target is ReactionButton button)
            {
                return button;
            }

            button = new ReactionButton();
            cache[key] = new WeakReference(button);
            Children.Add(button);

            return button;
        }

        private class ReactionDiffHandler : Rg.DiffUtils.IDiffEqualityComparer<MessageReaction>
        {
            public bool CompareItems(MessageReaction oldItem, MessageReaction newItem)
            {
                if (oldItem?.Type is ReactionTypeEmoji oldEmoji
                    && newItem?.Type is ReactionTypeEmoji newEmoji)
                {
                    return oldEmoji.Emoji == newEmoji.Emoji;
                }
                else if (oldItem?.Type is ReactionTypeCustomEmoji oldCustomEmoji
                    && newItem?.Type is ReactionTypeCustomEmoji newCustomEmoji)
                {
                    return oldCustomEmoji.CustomEmojiId == newCustomEmoji.CustomEmojiId;
                }

                return false;
            }

            public void UpdateItem(MessageReaction oldItem, MessageReaction newItem)
            {
                //throw new NotImplementedException();
            }
        }

        private const double Spacing = 4;

        public Thickness Padding { get; set; }

        public Size Footer { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
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
