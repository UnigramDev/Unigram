using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Stories
{
    public sealed partial class StoryInteractionBar : UserControl
    {
        private StoryViewModel _viewModel;
        public StoryViewModel ViewModel => _viewModel;

        public StoryInteractionBar()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler ViewersClick
        {
            add => ViewersButton.Click += value;
            remove => ViewersButton.Click -= value;
        }

        public event RoutedEventHandler DeleteClick
        {
            add => DeleteButton.Click += value;
            remove => DeleteButton.Click -= value;
        }

        public void Update(StoryViewModel story)
        {
            _viewModel = story;

            if (story.InteractionInfo != null)
            {
                Viewers.Items.ReplaceDiff(story.InteractionInfo.RecentViewerUserIds.Select(x => new MessageSenderUser(x)));
                Viewers.Visibility = story.InteractionInfo.RecentViewerUserIds.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                ViewersCount.Text = Locale.Declension(Strings.R.Views, story.InteractionInfo.ViewCount);

                ReactionCount.Text = story.InteractionInfo.ReactionCount.ToString("N0");
                ReactionCount.Visibility =
                    ReactionIcon.Visibility = story.InteractionInfo.ReactionCount > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
            else
            {
                Viewers.Items.Clear();
                Viewers.Visibility = Visibility.Collapsed;

                ViewersCount.Text = Strings.NobodyViews;

                ReactionCount.Visibility =
                    ReactionIcon.Visibility = Visibility.Collapsed;
            }
        }

        private void Viewers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            if (ViewModel.ClientService.TryGetUser(messageSender, out User user))
            {
                sender.SetUser(ViewModel.ClientService, user, 28);
            }
            else if (ViewModel.ClientService.TryGetChat(messageSender, out Chat chat))
            {
                sender.SetChat(ViewModel.ClientService, chat, 28);
            }
        }
    }
}
