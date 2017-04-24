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
            var bubble = image.Ancestors<MessageBubbleBase>().FirstOrDefault() as MessageBubbleBase;
            if (bubble != null)
            {
                if (bubble.Context != null)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                    var test = new DialogPhotosViewModel(bubble.Context.Peer, message, bubble.Context.ProtoService);
                    var dialog = new GalleryView { DataContext = test };
                    dialog.Background = null;
                    dialog.OverlayBrush = null;
                    dialog.Closing += (s, args) =>
                    {
                        var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                        if (animation != null)
                        {
                            animation.TryStart(image);
                        }
                    };

                    await dialog.ShowAsync();
                }
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
                var dialog = new GalleryView { DataContext = viewModel };
                dialog.Background = null;
                dialog.OverlayBrush = null;
                dialog.Closing += (s, args) =>
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                    if (animation != null)
                    {
                        animation.TryStart(image);
                    }
                };

                await dialog.ShowAsync();
            }
        }

        private void DownloadDocument_Click(object sender, RoutedEventArgs e)
        {
            DownloadDocument(sender);
        }

        public static async void DownloadDocument(object sender)
        {
            var border = sender as TransferButton;
            var message = border.DataContext as TLMessage;
            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var fileName = document.GetFileName();

                    if (File.Exists(FileUtils.GetTempFileName(fileName)))
                    {
                        if (message.IsVideo())
                        {
                            Photo_Click(sender, EventArgs.Empty);
                        }
                        else
                        {
                            var file = await StorageFile.GetFileFromApplicationUriAsync(FileUtils.GetTempFileUri(fileName));
                            await Launcher.LaunchFileAsync(file);
                        }
                    }
                    else
                    {
                        if (documentMedia.DownloadingProgress > 0 && documentMedia.DownloadingProgress < 1)
                        {
                            var manager = UnigramContainer.Current.ResolveType<IDownloadDocumentFileManager>();
                            manager.CancelDownloadFile(document);

                            documentMedia.DownloadingProgress = 0;
                            border.Update();
                        }
                        else if (documentMedia.UploadingProgress > 0 && documentMedia.UploadingProgress < 1)
                        {
                            var manager = UnigramContainer.Current.ResolveType<IUploadDocumentManager>();
                            manager.CancelUploadFile(document.Id);

                            documentMedia.UploadingProgress = 0;
                            border.Update();
                        }
                        else
                        {
                            //var watch = Stopwatch.StartNew();

                            //var download = await manager.DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), document.Size).AsTask(documentMedia.Download());

                            var manager = UnigramContainer.Current.ResolveType<IDownloadDocumentFileManager>();
                            var operation = manager.DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), document.Size);

                            documentMedia.DownloadingProgress = 0.02;
                            border.Update();

                            var download = await operation.AsTask(documentMedia.Download());
                            if (download != null)
                            {
                                border.Update();

                                //await new MessageDialog(watch.Elapsed.ToString()).ShowAsync();
                                //return;

                                if (message.IsMediaUnread && !message.IsOut)
                                {
                                    MTProtoService.Current.ReadMessageContentsAsync(new TLVector<int> { message.Id }, affected =>
                                    {
                                        message.IsMediaUnread = false;
                                        message.RaisePropertyChanged(() => message.IsMediaUnread);
                                    });
                                }

                                if (message.IsVideo())
                                {
                                    Photo_Click(sender, EventArgs.Empty);
                                }
                                else
                                {
                                    var file = await StorageFile.GetFileFromApplicationUriAsync(FileUtils.GetTempFileUri(fileName));
                                    await Launcher.LaunchFileAsync(file);
                                }
                            }
                        }
                    }
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
    }
}
