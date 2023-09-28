//
// Copyright Fela Ameghino 2015-2023
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
            ScrollingHost.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;

            var message = args.Item as MessageWithOwner;
            if (message == null)
            {
                return;
            }

            AutomationProperties.SetName(args.ItemContainer,
                Automation.GetSummary(message, true));

            if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                var photo = content.Children[0] as ImageView;
                photo.Tag = message;
                content.Tag = message;

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
            }
        }

        public override float TopPadding => 0;

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.Tag as MessageWithOwner;

            var viewModel = new ChatGalleryViewModel(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, message.ChatId, 0, message.Get(), true);
            viewModel.NavigationService = ViewModel.NavigationService;
            await GalleryWindow.ShowAsync(viewModel, () => element);
        }
    }
}
