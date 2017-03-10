using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Unigram.Helpers;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ShareTargetPage : Page, IHandle, IHandle<TLUpdateWebPage>
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        private ShareTargetHelper sth;

        public ShareTargetPage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<MainViewModel>();

            var aggregator = UnigramContainer.Current.ResolveType<ITelegramEventAggregator>();
            aggregator.Subscribe(this);

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateView(sth.ShareOperation);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            sth = new ShareTargetHelper(App.ShareOperation);
        }

        public void Handle(TLUpdateWebPage message)
        {

        }

        private async void UpdateView(ShareOperation operation)
        {
            if (operation.Data.Contains(StandardDataFormats.Bitmap))
            {
                var bitmaps = await operation.Data.GetBitmapAsync();
            }

            if (operation.Data.Contains(StandardDataFormats.Text))
            {
                var text = await operation.Data.GetTextAsync();
                txtMessage.Text = await operation.Data.GetTextAsync();
            }

            if (operation.Data.Contains(StandardDataFormats.WebLink))
            {
                var link = await operation.Data.GetWebLinkAsync();
                txtMessage.Text += Environment.NewLine + Environment.NewLine + link;

                var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
                var preview = await protoService.GetWebPagePreviewAsync(link.AbsoluteUri);
                if (preview.IsSucceeded)
                {

                }
            }

            if (operation.Data.Contains(StandardDataFormats.StorageItems))
            {
                var thumbnails = await operation.Data.GetStorageItemsAsync();
                if (thumbnails.Count > 0)
                {
                    Thumbnails.Visibility = Visibility.Visible;

                    Thumb1.Visibility = Visibility.Visible;
                    Thumb1.Background = new ImageBrush { ImageSource = await GetThumbnail(thumbnails[0] as StorageFile), Stretch = Stretch.UniformToFill };
                }

                if (thumbnails.Count > 1)
                {
                    Thumb2.Visibility = Visibility.Visible;
                    Thumb2.Background = new ImageBrush { ImageSource = await GetThumbnail(thumbnails[1] as StorageFile), Stretch = Stretch.UniformToFill };
                }

                if (thumbnails.Count > 2)
                {
                    Thumb3.Visibility = Visibility.Visible;
                    Thumb3.Background = new ImageBrush { ImageSource = await GetThumbnail(thumbnails[2] as StorageFile), Stretch = Stretch.UniformToFill };
                }
            }
        }

        private async Task<BitmapImage> GetThumbnail(StorageFile file)
        {
            //return new BitmapImage(new Uri(path));

            //var file = await StorageFile.GetFileFromPathAsync(path.Replace("file:///", string.Empty).Replace("/", "\\"));
            var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 100, ThumbnailOptions.ResizeThumbnail);
            if (thumb != null)
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(thumb.CloneStream());

                return bitmapImage;
            }

            return null;
        }

        // Button
        private async void lvMasterChats_ItemClick(object sender, ItemClickEventArgs e)
        {
            prgSendStatus.Value = 0;

            // Show dialog
            ContentDialog openShareDialog = new ContentDialog()
            {
                Title = "Share",
                Content = "Share with this chat?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };

            ContentDialogResult result = await openShareDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // TODO: disable user interaction

                // TODO: Rework entire sending mechanism
                prgSendStatus.Value = 10;

                var dialog = e.ClickedItem as TLDialog;

                var manualResetEvent = new ManualResetEvent(false);
                var cacheService = UnigramContainer.Current.ResolveType<ICacheService>();
                var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>() as MTProtoService;
                prgSendStatus.Value = 20;

                protoService.Initialized += (s, args) =>
                {
                    prgSendStatus.Value = 30;

                    // Now, prepare the message with the correct date and message itself.
                    var date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);
                    prgSendStatus.Value = 40;

                    // Send the correct message according to the send content
                    var message = TLUtils.GetMessage(SettingsHelper.UserId, dialog.Peer, TLMessageState.Sending, true, true, date, txtMessage.Text.Trim(), new TLMessageMediaEmpty(), TLLong.Random(), 0);
                    prgSendStatus.Value = 50;
                    cacheService.SyncSendingMessage(message, null, async (m) =>
                    {
                        await protoService.SendMessageAsync(message, () =>
                        {
                            // TODO: fast callback
                        });
                        manualResetEvent.Set();
                        prgSendStatus.Value = 60;
                    });

                    prgSendStatus.Value = 70;
                };
                protoService.InitializationFailed += (s, args) =>
                {
                    manualResetEvent.Set();
                };
                cacheService.Init();
                prgSendStatus.Value = 80;
                protoService.Initialize();
                prgSendStatus.Value = 90;
                manualResetEvent.WaitOne(4000);
                prgSendStatus.Value = 100;

                // Now close the shareoperation
                sth.CloseShareTarget();
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            sth.ShareOperation.ReportCompleted();
        }
    }
}
