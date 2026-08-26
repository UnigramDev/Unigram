//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
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

        public void UpdateMessageReactions(ChatSearchViewModel viewModel, SavedMessagesTags tags)
        {
            var items = tags?.Tags.Where(x => x.Count > 0).ToArray() ?? Array.Empty<SavedMessagesTag>();
            if (items.Length > 0)
            {
                void UpdateItem(SavedMessagesTag oldItem, SavedMessagesTag newItem, int index = 0)
                {
                    if (newItem != null)
                    {
                        oldItem.Label = newItem.Label;
                        oldItem.Count = newItem.Count;
                    }

                    if (oldItem.Tag is ReactionTypeEmoji emoji)
                    {
                        UpdateButton<string>(_reactions, emoji.Emoji, viewModel, oldItem, index);
                    }
                    else if (oldItem.Tag is ReactionTypeCustomEmoji customEmoji)
                    {
                        UpdateButton<long>(_customReactions, customEmoji.CustomEmojiId, viewModel, oldItem, index);
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
                    var diff = DiffCalculator.Create(prev, items, this);

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
                            if (diff.OldValue is SavedMessagesTag oldReaction)
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

                            Children.RemoveAt(diff.OldIndex);
                        }
                        else if (diff.State == DiffState.Unchanged)
                        {
                            UpdateItem(diff.OldValue, diff.NewValue);
                        }
                    }
                }

                _prevValue = items;
            }
            else
            {
                _prevValue = null;

                _reactions.Clear();
                _customReactions.Clear();

                Children.Clear();
            }
        }

        private void UpdateButton<T>(IDictionary<T, SavedMessagesTagButton> cache, T key, ChatSearchViewModel viewModel, SavedMessagesTag item, int index)
        {
            var button = GetOrCreateButton(cache, key, index);
            button.SetReaction(viewModel, item);
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

    public partial class SavedMessagesTagsPanelAutomationPeer : FrameworkElementAutomationPeer
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
