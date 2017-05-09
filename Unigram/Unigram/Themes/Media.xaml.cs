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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Themes
{
    public sealed partial class Media : ResourceDictionary
    {
        public Media()
        {
            this.InitializeComponent();
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            var message = image.DataContext as TLMessage;

            if (message != null)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                var viewModel = new DialogPhotosViewModel(message.Parent.ToInputPeer(), message, MTProtoService.Current);
                await GalleryView.Current.ShowAsync(viewModel, (s, args) =>
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                    if (animation != null)
                    {
                        animation.TryStart(image);
                    }
                });
            }
        }

        private void InstantView_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            var message = image.DataContext as TLMessage;
            var bubble = image.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;

            if (bubble != null)
            {
                if (bubble.Context != null)
                {
                    bubble.Context.NavigationService.Navigate(typeof(InstantPage), message.Media);
                }
            }
        }

        private async void SingleMedia_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as ImageView;
            var item = image.Constraint as TLPhoto;

            if (item != null)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                var viewModel = new SingleGalleryViewModel(new GalleryPhotoItem(item, null as string));
                await GalleryView.Current.ShowAsync(viewModel, (s, args) =>
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                    if (animation != null)
                    {
                        animation.TryStart(image);
                    }
                });
            }
        }

        private async void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;
            var bubble = element.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble != null)
            {
                if (bubble.Context != null && message.IsVideo())
                {
                    var media = bubble.FindName("MediaControl") as UIElement;

                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", media);

                    var viewModel = new DialogPhotosViewModel(bubble.Context.Peer, message, bubble.Context.ProtoService);
                    await GalleryView.Current.ShowAsync(viewModel, (s, args) =>
                    {
                        var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                        if (animation != null)
                        {
                            animation.TryStart(media);
                        }
                    });
                }
                else
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(FileUtils.GetTempFileUri(e.FileName));
                    await Launcher.LaunchFileAsync(file);
                }
            }
        }

        private async void GeoPoint_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message != null)
            {
                if (message.Media is TLMessageMediaGeo geoMedia)
                {
                    await LaunchGeoPointAsync(message.From?.FullName ?? string.Empty, geoMedia.Geo as TLGeoPoint);
                }
            }
        }

        private async void Venue_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message != null)
            {
                if (message.Media is TLMessageMediaVenue venueMedia)
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
            MessageHelper.HandleTelegramUrl("t.me/unigramchannel");
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
                    bubble.Context.NavigationService.Navigate(typeof(DialogPage), contactMedia.User.ToPeer());
                }
            }
        }
    }
}
