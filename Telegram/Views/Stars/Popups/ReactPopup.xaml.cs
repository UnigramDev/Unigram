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
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReactPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly MessageViewModel _message;

        private List<PaidReactor> _reactors;
        private PaidReactor _self;
        private int _count;

        private bool _loaded;

        public ReactPopup(IClientService clientService, MessageViewModel message)
        {
            InitializeComponent();

            _clientService = clientService;
            _message = message;

            // TODO: of course value won't update
            OwnedStarCount.Text = clientService.OwnedStarCount.ToString("N0");

            if (clientService.TryGetChat(message.ChatId, out Chat chat))
            {
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.StarsReactionText, chat.Title));

                StarCountSlider.Initialize(_starCount = 50, clientService.Options.PaidReactionStarCountMax);

                _reactors = new List<PaidReactor>(message.InteractionInfo?.Reactions?.PaidReactors ?? Array.Empty<PaidReactor>());

                if (_reactors.Count > 0)
                {
                    UpdateOrder();
                }
                else
                {
                    TopReactorsRoot.Visibility = Visibility.Collapsed;
                }

                Anonymous.IsChecked = !(_self?.IsAnonymous ?? clientService.Options.IsPaidReactionAnonymous);
            }
        }

        private void StarCountSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_self != null && _starCount != StarCount)
            {
                UpdateOrder();
            }

            _starCount = StarCount;

            if (PurchaseText != null)
            {
                PurchaseText.Text = string.Format(Strings.StarsReactionSend, StarCount.ToString("N0")).Replace("\u2B50", Icons.Premium);
            }
        }

        private void Anonymous_Click(object sender, RoutedEventArgs e)
        {
            if (_self != null)
            {
                UpdateOrder();
            }
        }

        private void UpdateOrder()
        {
            if (_self == null)
            {
                _self = _reactors.FirstOrDefault(x => x.IsMe);
                _self ??= new PaidReactor(_clientService.MyId, 0, false, true, IsAnonymous);

                _count = _self.StarCount;
            }
            else
            {
                _self.StarCount = _count + StarCount;
                _self.IsAnonymous = IsAnonymous;
            }

            _reactors.Remove(_self);

            var missing = true;

            for (int i = 0; i < _reactors.Count; i++)
            {
                if (_self.StarCount > _reactors[i].StarCount)
                {
                    _reactors.Insert(i, _self);
                    missing = false;
                    break;
                }
            }

            if (missing)
            {
                _reactors.Add(_self);
            }

            TopReactors.UpdateMessageReactions(_clientService, _reactors, _self);
        }

        private int _starCount;
        public int StarCount => StarCountSlider.RealValue;

        public bool IsAnonymous => Anonymous.IsChecked != true;

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsReactionTermsLink);
        }
    }

    public partial class PaidReactorsPanel : Panel, IDiffEqualityComparer<PaidReactor>
    {
        private readonly Dictionary<PaidReactor, PaidReactorCell> _cache = new();

        private PaidReactor[] _prevValue;

        private int _offset;

        public void UpdateMessageReactions(IClientService clientService, IList<PaidReactor> reactors, PaidReactor self)
        {
            if (reactors == null)
            {
                _prevValue = null;

                _cache.Clear();
                Children.Clear();
            }

            if (reactors?.Count > 0)
            {
                void UpdateItem(PaidReactor oldItem, PaidReactor newItem, int index = 0)
                {
                    if (newItem != null)
                    {
                        oldItem.IsAnonymous = newItem.IsAnonymous;
                        oldItem.IsMe = newItem.IsMe;
                        oldItem.IsTop = newItem.IsTop;
                        oldItem.StarCount = newItem.StarCount;
                    }

                    //var changed = Animate(oldItem.Type);
                    UpdateButton(clientService, oldItem, index);
                }

                _offset = self?.StarCount > 0 && reactors?.Count < 4 ? 1 : 0;

                if (_prevValue == null)
                {
                    for (int i = 0; i < reactors.Count; i++)
                    {
                        UpdateItem(reactors[i], null, i);
                    }
                }
                else
                {
                    // PERF: run diff asynchronously?
                    var prev = _prevValue ?? Array.Empty<PaidReactor>();
                    var diff = DiffUtil.CalculateDiff(prev, reactors, this, Constants.DiffOptions);

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
                            if (step.Items[0].OldValue is PaidReactor oldReaction)
                            {
                                _cache.Remove(oldReaction);
                            }

                            Children.RemoveAt(step.OldStartIndex);
                        }
                    }

                    foreach (var item in diff.NotMovedItems)
                    {
                        UpdateItem(item.OldValue, item.NewValue);
                    }
                }

                _prevValue = reactors.ToArray();
            }
        }

        private void UpdateButton(IClientService clientService, PaidReactor item, int index)
        {
            var button = GetOrCreateButton(item, index);
            button.UpdateCell(clientService, item);
            //button.SetReaction(message, item);

            //if (animate)
            //{
            //    button.SetUnread(new UnreadReaction(item.Type, null, false));
            //}
        }

        private PaidReactorCell GetOrCreateButton(PaidReactor key, int index)
        {
            if (_cache.TryGetValue(key, out PaidReactorCell button))
            {
                return button;
            }

            //button = isTag
            //    ? new ReactionAsTagButton()
            //    : key is ReactionTypePaid
            //    ? new ReactionAsPaidButton()
            //    : new ReactionButton();

            button = new PaidReactorCell();

            _cache[key] = button;
            Children.Insert(Math.Min(index, Children.Count), button);

            return button;
        }

        public bool CompareItems(PaidReactor oldItem, PaidReactor newItem)
        {
            return oldItem == newItem;

            if (oldItem.IsMe)
            {
                return newItem.IsMe;
            }
            else if (oldItem.SenderId != null)
            {
                return oldItem.SenderId.AreTheSame(newItem.SenderId);
            }
            else if (oldItem.IsAnonymous)
            {
                return newItem.IsAnonymous && oldItem.StarCount == newItem.StarCount;
            }

            return false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = (availableSize.Width - 48) / (Children.Count - 1 + _offset);
            var height = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Measure(new Size(width, availableSize.Height));
                height = Math.Max(height, Children[i].DesiredSize.Height);
            }

            return new Size(availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = (finalSize.Width - 48) / (Children.Count - 1 + _offset);

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var center = (width - child.DesiredSize.Width) / 2;

                var j = i; // - 1;
                if (j < 0)
                {
                    child.Arrange(new Rect(-child.DesiredSize.Width - 12, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
                else if (j >= Children.Count - 1 + _offset)
                {
                    child.Arrange(new Rect(finalSize.Width + 12, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
                else
                {
                    child.Arrange(new Rect(j * width + 24 + center, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
            }

            return finalSize;
        }
    }

    public partial class SteppedValue
    {
        public double progress = 0;
        public double aprogress;
        public int steps;
        public int[] stops;

        public SteppedValue(int steps, IList<int> stops)
        {
            this.steps = steps;
            this.stops = stops.ToArray();
        }

        public void setValue(int value)
        {
            setValue(value, false);
        }
        public void setValue(int value, bool byScroll)
        {
            this.progress = getProgress(value);
            if (!byScroll)
            {
                this.aprogress = this.progress;
            }
            //updateText(true);
        }

        public int getValue()
        {
            return getValue(progress);
        }

        public double getProgress()
        {
            return progress;
        }

        public int getValue(double progress)
        {
            if (progress <= 0f) return stops[0];
            if (progress >= 1f) return stops[stops.Length - 1];
            double scaledProgress = progress * (stops.Length - 1);
            int index = (int)scaledProgress;
            double localProgress = scaledProgress - index;
            return (int)Math.Round(stops[index] + localProgress * (stops[index + 1] - stops[index]));
        }

        public float getProgress(int value)
        {
            for (int i = 1; i < stops.Length; ++i)
            {
                if (value <= stops[i])
                {
                    float local = (float)(value - stops[i - 1]) / (stops[i] - stops[i - 1]);
                    return (i - 1 + local) / (stops.Length - 1);
                }
            }
            return 1f;
        }
    }
}
