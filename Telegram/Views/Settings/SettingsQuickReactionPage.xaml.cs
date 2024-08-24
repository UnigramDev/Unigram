//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Settings;
using Windows.Foundation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsQuickReactionPage : HostedPage
    {
        public SettingsQuickReactionViewModel ViewModel => DataContext as SettingsQuickReactionViewModel;

        public SettingsQuickReactionPage()
        {
            InitializeComponent();
            Title = Strings.DoubleTapSetting;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Handle();
            ViewModel.Aggregator.Subscribe<UpdateDefaultReactionType>(this, Handle);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private void Handle(UpdateDefaultReactionType update)
        {
            this.BeginOnUIThread(Handle);
        }

        private async void Handle()
        {
            var reaction = ViewModel.ClientService.DefaultReaction;
            if (reaction is ReactionTypeEmoji emoji)
            {
                var response = await ViewModel.ClientService.SendAsync(new GetEmojiReaction(emoji.Emoji));
                if (response is EmojiReaction emojiReaction)
                {
                    Icon.Width = Icon.Height = 32;
                    Icon.Margin = new Thickness(0, 0, -6, 0);

                    using (Icon.BeginBatchUpdate())
                    {
                        Icon.FrameSize = new Size(32, 32);
                        Icon.Source = new DelayedFileSource(ViewModel.ClientService, emojiReaction.CenterAnimation);
                    }
                }
            }
            else if (reaction is ReactionTypeCustomEmoji customEmoji)
            {
                Icon.Width = Icon.Height = 20;
                Icon.Margin = new Thickness();

                using (Icon.BeginBatchUpdate())
                {
                    Icon.FrameSize = new Size(20, 20);
                    Icon.Source = new CustomEmojiFileSource(ViewModel.ClientService, customEmoji.CustomEmojiId);
                }
            }
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.Reactions, IconPanel, EmojiFlyoutAlignment.TopRight);
        }
    }
}
