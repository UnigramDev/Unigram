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
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileAnimationsTabPage : ProfileTabPage
    {
        public ProfileAnimationsTabPage()
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

                if (message.Content is MessageAnimation animation)
                {
                    if (animation.Animation.Thumbnail is { Format: ThumbnailFormatJpeg })
                    {
                        photo.SetSource(ViewModel.ClientService, animation.Animation.Thumbnail.File);
                    }
                    else if (animation.Animation.Minithumbnail != null)
                    {
                        var bitmap = new BitmapImage();
                        PlaceholderHelper.GetBlurred(bitmap, animation.Animation.Minithumbnail.Data);
                        photo.Source = bitmap;
                    }
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
