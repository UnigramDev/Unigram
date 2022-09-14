using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages;
using Unigram.ViewModels.Drawers;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsQuickReactionPage : HostedPage
    {
        public SettingsQuickReactionViewModel ViewModel => DataContext as SettingsQuickReactionViewModel;

        public SettingsQuickReactionPage()
        {
            InitializeComponent();
            Title = Strings.Resources.DoubleTapSetting;
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
                Icon.SetReaction(ViewModel.ClientService, await ViewModel.ClientService.GetReactionAsync(emoji.Emoji));
            }
            else if (reaction is ReactionTypeCustomEmoji customEmoji)
            {
                Icon.SetCustomEmoji(ViewModel.ClientService, customEmoji.CustomEmojiId);
            }
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutReactions.ShowAt(ViewModel.ClientService, EmojiDrawerMode.Reactions, IconPanel, HorizontalAlignment.Right);
        }
    }
}
