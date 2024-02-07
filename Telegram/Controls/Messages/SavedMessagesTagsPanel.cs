//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Controls.Messages
{
    // Simplified version of ReactionsPanel
    public partial class SavedMessagesTagsPanel : StackPanel, IDiffEqualityComparer<SavedMessagesTag>
    {
        private readonly Dictionary<string, SavedMessagesTagButton> _reactions = new();
        private readonly Dictionary<long, SavedMessagesTagButton> _customReactions = new();

        private SavedMessagesTag[] _prevValue;

        public SavedMessagesTagsPanel()
        {
            Orientation = Orientation.Horizontal;

            TabFocusNavigation = KeyboardNavigationMode.Once;

            ChildrenTransitions = new TransitionCollection
            {
                new RepositionThemeTransition()
            };
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SavedMessagesTagsPanelAutomationPeer(this);
        }

        public bool HasReactions => _reactions.Count > 0 || _customReactions.Count > 0;

        public async void UpdateMessageReactions(ChatSearchViewModel viewModel, SavedMessagesTags tags)
        {
            var items = tags?.Tags.Where(x => x.Count > 0).ToArray() ?? Array.Empty<SavedMessagesTag>();
            if (items.Length > 0)
            {
                List<long> missingCustomEmoji = null;
                List<string> missingEmoji = null;

                void UpdateItem(SavedMessagesTag oldItem, SavedMessagesTag newItem, int index = 0)
                {
                    if (newItem != null)
                    {
                        oldItem.Label = newItem.Label;
                        oldItem.Count = newItem.Count;
                    }

                    if (oldItem.Tag is ReactionTypeEmoji emoji)
                    {
                        var required = UpdateButton<string, EmojiReaction>(_reactions, emoji.Emoji, viewModel, oldItem, viewModel.ClientService.TryGetCachedReaction, index);
                        if (required)
                        {
                            missingEmoji ??= new List<string>();
                            missingEmoji.Add(emoji.Emoji);
                        }
                    }
                    else if (oldItem.Tag is ReactionTypeCustomEmoji customEmoji)
                    {
                        var required = UpdateButton<long, Sticker>(_customReactions, customEmoji.CustomEmojiId, viewModel, oldItem, EmojiCache.TryGet, index);
                        if (required)
                        {
                            missingCustomEmoji ??= new List<long>();
                            missingCustomEmoji.Add(customEmoji.CustomEmojiId);
                        }
                    }
                }

                if (_prevValue == null)
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        UpdateItem(items[i], null, i);
                    }
                }
                else
                {
                    // PERF: run diff asynchronously?
                    var prev = _prevValue ?? Array.Empty<SavedMessagesTag>();
                    var diff = DiffUtil.CalculateDiff(prev, items, this, Constants.DiffOptions);

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
                            if (step.Items[0].OldValue is SavedMessagesTag oldReaction)
                            {
                                if (oldReaction.Tag is ReactionTypeEmoji oldEmoji)
                                {
                                    _reactions.Remove(oldEmoji.Emoji);
                                }
                                else if (oldReaction.Tag is ReactionTypeCustomEmoji oldCustomEmoji)
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
                }

                _prevValue = items;

                if (missingCustomEmoji != null)
                {
                    var response = await EmojiCache.GetAsync(viewModel.ClientService, missingCustomEmoji);
                    if (response != null)
                    {
                        foreach (var sticker in response)
                        {
                            if (sticker.FullType is not StickerFullTypeCustomEmoji customEmoji)
                            {
                                continue;
                            }

                            EmojiCache.AddOrUpdate(sticker);
                            UpdateButton<long, Sticker>(_customReactions, customEmoji.CustomEmojiId, viewModel, sticker);
                        }
                    }
                }

                if (missingEmoji != null)
                {
                    foreach (var emoji in missingEmoji)
                    {
                        var response = await viewModel.ClientService.SendAsync(new GetEmojiReaction(emoji));
                        if (response is EmojiReaction reaction)
                        {
                            UpdateButton<string, EmojiReaction>(_reactions, emoji, viewModel, reaction);
                        }
                    }
                }
            }
            else
            {
                _prevValue = null;

                _reactions.Clear();
                _customReactions.Clear();

                Children.Clear();
            }
        }

        delegate bool TryGetValue<TKey, T>(TKey key, out T value);

        private void UpdateButton<T, TValue>(IDictionary<T, SavedMessagesTagButton> cache, T key, ChatSearchViewModel viewModel, object value)
        {
            if (cache.TryGetValue(key, out SavedMessagesTagButton button))
            {
                if (value is EmojiReaction reaction)
                {
                    button.SetReaction(viewModel, button.Tag, reaction);
                }
                else if (value is Sticker sticker)
                {
                    button.SetReaction(viewModel, button.Tag, sticker);
                }
            }
        }

        private bool UpdateButton<T, TValue>(IDictionary<T, SavedMessagesTagButton> cache, T key, ChatSearchViewModel viewModel, SavedMessagesTag item, TryGetValue<T, TValue> tryGet, int index)
        {
            var required = false;

            var button = GetOrCreateButton(cache, key, index);
            if (button.EmojiReaction != null)
            {
                button.SetReaction(viewModel, item, button.EmojiReaction);
            }
            else if (button.CustomReaction != null)
            {
                button.SetReaction(viewModel, item, button.CustomReaction);
            }
            else if (tryGet(key, out TValue value))
            {
                if (value is EmojiReaction reaction)
                {
                    button.SetReaction(viewModel, item, reaction);
                }
                else if (value is Sticker sticker)
                {
                    button.SetReaction(viewModel, item, sticker);
                }
            }
            else
            {
                button.SetReaction(viewModel, item, null as EmojiReaction);

                required = true;
            }

            return required;
        }

        private SavedMessagesTagButton GetOrCreateButton<T>(IDictionary<T, SavedMessagesTagButton> cache, T key, int index)
        {
            if (cache.TryGetValue(key, out SavedMessagesTagButton button))
            {
                return button;
            }

            button = new SavedMessagesTagButton();
            cache[key] = button;
            Children.Insert(Math.Min(index, Children.Count), button);

            return button;
        }

        public bool CompareItems(SavedMessagesTag oldItem, SavedMessagesTag newItem)
        {
            if (oldItem != null)
            {
                return oldItem.Tag.AreTheSame(newItem?.Tag);
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

    public class SavedMessagesTagsPanelAutomationPeer : FrameworkElementAutomationPeer
    {
        public SavedMessagesTagsPanelAutomationPeer(SavedMessagesTagsPanel owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.List;
        }
    }
}
