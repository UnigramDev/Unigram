//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls.Messages
{
    public class ReactionAsTagButton : ReactionButton
    {
        private MessageTag _tag;

        public ReactionAsTagButton()
        {
            DefaultStyleKey = typeof(ReactionAsTagButton);

            Connected += OnConnected;
            Disconnected += OnDisconnected;
        }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            if (_tag != null)
            {
                _tag.PropertyChanged += OnPropertyChanged;
            }
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (_tag != null)
            {
                _tag.PropertyChanged -= OnPropertyChanged;
            }
        }

        protected override void UpdateInteraction(MessageViewModel message, MessageReaction interaction, bool recycled)
        {
            IsChecked = interaction.IsChosen;

            if (_tag != null)
            {
                _tag.PropertyChanged -= OnPropertyChanged;
            }

            _tag = message.ClientService.GetSavedMessagesTag(interaction.Type);

            if (_tag != null && IsConnected)
            {
                _tag.PropertyChanged += OnPropertyChanged;
            }

            if (string.IsNullOrEmpty(_tag?.Label))
            {
                if (Count != null)
                {
                    Count.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Count ??= GetTemplateChild(nameof(Count)) as AnimatedTextBlock;
                Count.Visibility = Visibility.Visible;

                Count.Text = _tag.Label;
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender == _tag && e.PropertyName == nameof(MessageTag.Label))
            {
                this.BeginOnUIThread(UpdateLabel);
            }
        }

        private void UpdateLabel()
        {
            if (string.IsNullOrEmpty(_tag?.Label))
            {
                if (Count != null)
                {
                    Count.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Count ??= GetTemplateChild(nameof(Count)) as AnimatedTextBlock;
                Count.Visibility = Visibility.Visible;

                Count.Text = _tag.Label;
            }
        }

        public void OnContextRequested()
        {
            var chosen = _interaction;
            if (chosen != null)
            {
                OnClick(_message, chosen);
            }
        }

        protected override void OnClick(MessageViewModel message, MessageReaction chosen)
        {
            if (!message.ClientService.IsPremium)
            {
                if (message.ClientService.IsPremiumAvailable)
                {
                    message.Delegate.NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureSavedMessagesTags()));
                }

                return;
            }

            var flyout = new MenuFlyout();

            var tag = _message.ClientService.GetSavedMessagesTag(chosen.Type);
            var selected = _message.Delegate.SavedMessagesTag;

            var edit = string.IsNullOrEmpty(tag?.Label)
                ? Strings.SavedTagLabelTag
                : Strings.SavedTagRenameTag;

            if (selected == null || !chosen.Type.AreTheSame(selected))
            {
                flyout.CreateFlyoutItem(FilterByTag, Strings.SavedTagFilterByTag, Icons.TagFilter);
            }

            flyout.CreateFlyoutItem(RenameTag, edit, Icons.TagEdit);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(RemoveTag, Strings.SavedTagRemoveTag, Icons.TagOff, destructive: true);

            FlyoutPlacementMode placement;
            if (Parent is FrameworkElement parent)
            {
                var transform = TransformToVisual(parent);
                var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                placement = point.X < (parent.ActualWidth - (point.X + ActualWidth))
                        ? FlyoutPlacementMode.BottomEdgeAlignedLeft
                        : FlyoutPlacementMode.BottomEdgeAlignedRight;
            }
            else
            {
                placement = FlyoutPlacementMode.BottomEdgeAlignedLeft;
            }

            flyout.ShowAt(this, placement);
        }

        private void FilterByTag()
        {
            var chosen = _interaction;
            if (chosen == null)
            {
                return;
            }

            _message.Delegate.SavedMessagesTag = chosen.Type;
        }

        private async void RenameTag()
        {
            var chosen = _interaction;
            if (chosen == null || !_message.ClientService.TryGetSavedMessagesTag(chosen.Type, out MessageTag tag))
            {
                return;
            }

            var popup = new InputPopup();
            popup.Title = string.IsNullOrEmpty(tag.Label) ? Strings.SavedTagLabelTag : Strings.SavedTagRenameTag;
            popup.Header = Strings.SavedTagLabelTagText;
            popup.PlaceholderText = Strings.SavedTagLabelPlaceholder;
            popup.Text = tag.Label;
            popup.MinLength = 0;
            popup.MaxLength = 12;
            popup.IsPrimaryButtonEnabled = true;
            popup.IsSecondaryButtonEnabled = true;
            popup.PrimaryButtonText = Strings.Save;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _message.ClientService.Send(new SetSavedMessagesTagLabel(chosen.Type, popup.Text));
            }
        }

        private void RemoveTag()
        {
            var chosen = _interaction;
            if (chosen == null)
            {
                return;
            }

            _message.ClientService.Send(new RemoveMessageReaction(_message.ChatId, _message.Id, chosen.Type));
        }
    }
}
