using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Unigram.Controls.Messages;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Unigram.Controls.Views;
using Windows.UI.Xaml.Media.Animation;
using Telegram.Api.Services.FileManager;
using Windows.Storage;
using Windows.System;
using Unigram.Views;
using Telegram.Api.Helpers;
using Unigram.Controls;
using System.Diagnostics;
using Windows.UI.Popups;
using System.Threading.Tasks;
using System.Globalization;
using System.Net;
using Unigram.Common;
using Telegram.Api.Services;
using Unigram.Views.Users;
using Unigram.ViewModels.Users;
using Telegram.Api.Services.Cache;
using Telegram.Api.Aggregator;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Themes
{
    public sealed partial class Media : ResourceDictionary
    {
        public Media()
        {
            this.InitializeComponent();
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            Photo_Click(sender);
        }

        public static async void Photo_Click(object sender)
        {
            Download(sender, null);
            return;

            var image = sender as FrameworkElement;
            var message = image.DataContext as TLMessage;

            if (message != null)
            {
                //ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                var viewModel = new DialogGalleryViewModel(message.Parent.ToInputPeer(), message, MTProtoService.Current);
                await GalleryView.Current.ShowAsync(viewModel, () => image);
            }
        }

        private void InstantView_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            var message = image.DataContext as TLMessage;
            var bubble = image.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;

            if (bubble != null && bubble.Context != null)
            {
                if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    if (webPage.HasCachedPage)
                    {
                        bubble.Context.NavigationService.Navigate(typeof(InstantPage), message.Media);
                    }
                    else if (webPage.HasType && (webPage.Type.Equals("telegram_megagroup", StringComparison.OrdinalIgnoreCase) ||
                                                 webPage.Type.Equals("telegram_channel", StringComparison.OrdinalIgnoreCase) ||
                                                 webPage.Type.Equals("telegram_message", StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageHelper.HandleTelegramUrl(webPage.Url);
                    }
                }
            }
        }

        private async void SingleMedia_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as ImageView;
            var item = image.Constraint as TLPhoto;
            var message = image.DataContext as TLMessage;
            if (message != null && message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage && webPage.HasEmbedUrl)
            {
                await WebPageView.Current.ShowAsync(webPage);
            }
            else if (item != null)
            {
                //ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                var viewModel = new SingleGalleryViewModel(new GalleryPhotoItem(item, null as string));
                await GalleryView.Current.ShowAsync(viewModel, () => image);
            }
        }

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            Download(sender, e);
        }

        private void SecretDownload_Click(object sender, TransferCompletedEventArgs e)
        {
            SecretDownload(sender, e);
        }

        public static async void Download(object sender, TransferCompletedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message == null)
            {
                return;
            }

            var document = message.GetDocument();
            if (TLMessage.IsGif(document))
            {
                var bubble = element.Ancestors<MessageBubble>().FirstOrDefault() as MessageBubble;
                if (bubble == null)
                {
                    return;
                }

                var page = bubble.Ancestors<DialogPage>().FirstOrDefault() as DialogPage;
                if (page == null)
                {
                    return;
                }

                page.Play(bubble.ViewModel);
            }
            else if (TLMessage.IsVideo(document) || TLMessage.IsRoundVideo(document) || TLMessage.IsGif(document) || message.IsPhoto())
            {
                var media = element.Ancestors().FirstOrDefault(x => x is FrameworkElement && ((FrameworkElement)x).Name.Equals("MediaControl")) as FrameworkElement;
                if (media == null)
                {
                    media = element;
                }

                //ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", media);

                GalleryViewModelBase viewModel;
                if (message.Parent != null)
                {
                    viewModel = new DialogGalleryViewModel(message.Parent.ToInputPeer(), message, MTProtoService.Current);
                }
                else
                {
                    viewModel = new SingleGalleryViewModel(new GalleryMessageItem(message));
                }

                await GalleryView.Current.ShowAsync(viewModel, () => media);
            }
            else if (e != null)
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(FileUtils.GetTempFileUri(e.FileName));
                await Launcher.LaunchFileAsync(file);
            }
        }

        public static async void SecretDownload(object sender, TransferCompletedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message == null)
            {
                return;
            }

            if (message.IsMediaUnread && !message.IsOut)
            {
                var vector = new TLVector<int> { message.Id };
                if (message.Parent is TLChannel channel)
                {
                    TelegramEventAggregator.Instance.Publish(new TLUpdateChannelReadMessagesContents { ChannelId = channel.Id, Messages = vector });
                    MTProtoService.Current.ReadMessageContentsAsync(channel.ToInputChannel(), vector, result =>
                    {
                        message.IsMediaUnread = false;
                        message.RaisePropertyChanged(() => message.IsMediaUnread);
                    });
                }
                else
                {
                    TelegramEventAggregator.Instance.Publish(new TLUpdateReadMessagesContents { Messages = vector });
                    MTProtoService.Current.ReadMessageContentsAsync(vector, result =>
                    {
                        message.IsMediaUnread = false;
                        message.RaisePropertyChanged(() => message.IsMediaUnread);
                    });
                }
            }

            var media = element.Ancestors().FirstOrDefault(x => x is FrameworkElement && ((FrameworkElement)x).Name.Equals("MediaControl")) as FrameworkElement;
            if (media == null)
            {
                media = element;
            }

            if (media is Grid grid)
            {
                // TODO: WARNING!!!
                media = grid.Children[1] as FrameworkElement;
            }

            if (message.Parent != null)
            {
                var viewModel = new GallerySecretViewModel(message.Parent.ToInputPeer(), message, MTProtoService.Current, InMemoryCacheService.Current, TelegramEventAggregator.Instance);
                await GallerySecretView.Current.ShowAsync(viewModel, () => media);
            }
        }

        private async void Geo_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message != null)
            {
                if (message.Media is TLMessageMediaGeo geoMedia)
                {
                    await LaunchGeoPointAsync(message.From?.FullName ?? string.Empty, geoMedia.Geo as TLGeoPoint);
                }
                else if (message.Media is TLMessageMediaGeoLive geoLiveMedia)
                {
                    await LaunchGeoPointAsync(message.From?.FullName ?? string.Empty, geoLiveMedia.Geo as TLGeoPoint);
                }
                else if (message.Media is TLMessageMediaVenue venueMedia)
                {
                    await LaunchGeoPointAsync(message.From?.FullName ?? string.Empty, venueMedia.Geo as TLGeoPoint);
                }
            }
        }

        private IAsyncOperation<bool> LaunchGeoPointAsync(string title, TLGeoPoint point)
        {
            if (point != null)
            {
                return Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "bingmaps:?collection=point.{0}_{1}_{2}", point.Lat, point.Long, WebUtility.UrlEncode(title))));
            }

            return Task.FromResult(true).AsAsyncOperation();
        }

        private void Unsupported_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            MessageHelper.HandleTelegramUrl("t.me/unigram");
        }

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;
            var bubble = element.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble != null && bubble.Context != null)
            {
                if (message.Media is TLMessageMediaContact contactMedia && contactMedia.User.HasAccessHash)
                {
                    bubble.Context.NavigationService.Navigate(typeof(UserDetailsPage), contactMedia.User.ToPeer());
                }
            }
        }
    }
}
