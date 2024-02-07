//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Gallery;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileMediaTabPage : ProfileTabPage
    {
        public ProfileMediaTabPage()
        {
            InitializeComponent();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is MessageWithOwner message)
            {
                AutomationProperties.SetName(args.ItemContainer, Automation.GetSummaryWithName(message, true));

                var photo = content.Children[0] as ImageView;

                // TODO: justified because of Photo_Click
                photo.Tag = message;

                var panel = content.Children[1] as Border;
                var duration = panel.Child as TextBlock;

                if (message.Content is MessagePhoto photoMessage)
                {
                    var small = photoMessage.Photo.GetSmall();

                    photo.SetSource(ViewModel.ClientService, small.Photo);
                    panel.Visibility = Visibility.Collapsed;
                }
                else if (message.Content is MessageVideo videoMessage && videoMessage.Video.Thumbnail != null)
                {
                    photo.SetSource(ViewModel.ClientService, videoMessage.Video.Thumbnail.File);
                    panel.Visibility = Visibility.Visible;

                    duration.Text = videoMessage.Video.GetDuration();
                }

                args.Handled = true;
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.Tag as MessageWithOwner;

            var viewModel = new ChatGalleryViewModel(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, message.ChatId, ViewModel.ThreadId, ViewModel.SavedMessagesTopicId, message, true);
            viewModel.NavigationService = ViewModel.NavigationService;
            await GalleryWindow.ShowAsync(viewModel, () => element);
        }
    }
}
