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
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Dialogs;

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
            Download_Click(sender, null);
        }

        private void InstantView_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            var message = image.DataContext as TLMessage;

            var bubble = image.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble == null)
            {
                return;
            }

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

        private async void Sticker_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null && stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty)
                {
                    var page = element.Ancestors<DialogPage>().FirstOrDefault() as DialogPage;
                    if (page != null)
                    {
                        await StickerSetView.Current.ShowAsync(stickerAttribute.StickerSet, page.Stickers_ItemClick);
                    }
                    else
                    {
                        await StickerSetView.Current.ShowAsync(stickerAttribute.StickerSet);
                    }
                }
            }
        }

        private async void SingleMedia_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as ImageView;
            if (image.DataContext is TLMessage message && message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
            {
                if (webPage.HasEmbedUrl)
                {
                    await WebPageView.Current.ShowAsync(webPage);
                    return;
                }
                else if (webPage.IsInstantGallery())
                {
                    var viewModel = new InstantGalleryViewModel(message, webPage);
                    await GalleryView.Current.ShowAsync(viewModel, () => image.Parent as FrameworkElement);
                    return;
                }
            }

            if (image.Constraint is TLPhoto photo)
            {
                var viewModel = new SingleGalleryViewModel(new GalleryPhotoItem(photo, null as string));
                await GalleryView.Current.ShowAsync(viewModel, () => image.Parent as FrameworkElement);
            }
        }

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            Download_Click(sender as FrameworkElement, e);
        }

        public static async void Download_Click(FrameworkElement sender, TransferCompletedEventArgs e)
        {
            var element = sender as FrameworkElement;

            var bubble = element.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble == null)
            {
                return;
            }

            if (element.DataContext is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionChatEditPhoto editPhotoAction)
            {
                var media = element.Parent as FrameworkElement;
                if (media == null)
                {
                    media = element;
                }

                var chat = serviceMessage.Parent as TLChatBase;
                if (chat == null)
                {
                    return;
                }

                var chatFull = InMemoryCacheService.Current.GetFullChat(chat.Id);
                if (chatFull != null && chatFull.ChatPhoto is TLPhoto && chat != null)
                {
                    var viewModel = new ChatPhotosViewModel(bubble.ContextBase.ProtoService, bubble.ContextBase.CacheService, chatFull, chat, serviceMessage);
                    await GalleryView.Current.ShowAsync(viewModel, () => media);
                }

                return;
            }

            var message = element.DataContext as TLMessage;
            if (message == null)
            {
                return;
            }

            var document = message.GetDocument();
            if (TLMessage.IsGif(document) && !ApplicationSettings.Current.IsAutoPlayEnabled)
            {
                var page = bubble.Ancestors<IGifPlayback>().FirstOrDefault() as IGifPlayback;
                if (page == null)
                {
                    return;
                }

                if (bubble.ViewModel is TLMessage inner)
                {
                    page.Play(inner);
                }
            }
            else if (TLMessage.IsVideo(document) || TLMessage.IsRoundVideo(document) || TLMessage.IsGif(document) || message.IsPhoto())
            {
                var media = element.Ancestors().FirstOrDefault(x => x is FrameworkElement && ((FrameworkElement)x).Name.Equals("Media")) as FrameworkElement;
                if (media == null)
                {
                    media = element;
                }

                //ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", media);

                GalleryViewModelBase viewModel;
                if (message.Parent == null || TLMessage.IsRoundVideo(document) || TLMessage.IsGif(document))
                {
                    viewModel = new SingleGalleryViewModel(new GalleryMessageItem(message));
                }
                else
                {
                    viewModel = new DialogGalleryViewModel(bubble.ContextBase.ProtoService, bubble.ContextBase.CacheService, message.Parent.ToInputPeer(), message);
                }

                await GalleryView.Current.ShowAsync(viewModel, () => media);
            }
            else if (e != null)
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(FileUtils.GetTempFileUri(e.FileName));
                await Launcher.LaunchFileAsync(file);
            }
        }

        private void SecretDownload_Click(object sender, TransferCompletedEventArgs e)
        {
            SecretDownload_Click(sender as FrameworkElement, e);
        }

        public static async void SecretDownload_Click(FrameworkElement sender, TransferCompletedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message == null)
            {
                return;
            }

            var bubble = element.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble == null)
            {
                return;
            }

            if (message.IsMediaUnread && !message.IsOut)
            {
                var vector = new TLVector<int> { message.Id };
                if (message.Parent is TLChannel channel)
                {
                    bubble.ContextBase.Aggregator.Publish(new TLUpdateChannelReadMessagesContents { ChannelId = channel.Id, Messages = vector });
                    bubble.ContextBase.ProtoService.ReadMessageContentsAsync(channel.ToInputChannel(), vector, result =>
                    {
                        message.IsMediaUnread = false;
                        message.RaisePropertyChanged(() => message.IsMediaUnread);
                    });
                }
                else
                {
                    bubble.ContextBase.Aggregator.Publish(new TLUpdateReadMessagesContents { Messages = vector });
                    bubble.ContextBase.ProtoService.ReadMessageContentsAsync(vector, result =>
                    {
                        message.IsMediaUnread = false;
                        message.RaisePropertyChanged(() => message.IsMediaUnread);
                    });
                }
            }

            var media = element.Ancestors().FirstOrDefault(x => x is FrameworkElement && ((FrameworkElement)x).Name.Equals("Media")) as FrameworkElement;
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
                var viewModel = new GallerySecretViewModel(message.Parent.ToInputPeer(), message, bubble.ContextBase.ProtoService, bubble.ContextBase.CacheService, bubble.ContextBase.Aggregator);
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
            MessageHelper.NavigateToUsername("unigram", null, null, null);
        }

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            var bubble = element.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble == null)
            {
                return;
            }

            if (message.Media is TLMessageMediaContact contactMedia && contactMedia.User.HasAccessHash)
            {
                bubble.Context.NavigationService.Navigate(typeof(UserDetailsPage), contactMedia.User.ToPeer());
            }
        }
    }
}
