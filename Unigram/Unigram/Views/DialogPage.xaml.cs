using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Dependency;
using Unigram.Models;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{

    public sealed partial class DialogPage : Page
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public BindConvert Convert => BindConvert.Current;

        public DialogPage()
        {
            InitializeComponent();

            //ListGallery.ItemsSource = new PicturesCollection();

            DataContext = UnigramContainer.Instance.ResolverType<DialogViewModel>();
            Loaded += DialogPage_Loaded;
            CheckMessageBoxEmpty();
        }

        private void DialogPage_Loaded(object sender, RoutedEventArgs e)
        {
            //_panel = (ItemsStackPanel)lvDialogs.ItemsPanelRoot;
            //lvDialogs.ScrollingHost.ViewChanged += OnViewChanged;

            lvDialogs.ScrollingHost.ViewChanged += LvScroller_ViewChanged;
        }

        //private bool _isAlreadyLoading;
        //private bool _isAlreadyCalled;

        private async void LvScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (lvDialogs.ScrollingHost.VerticalOffset < 120 && !e.IsIntermediate)
            {
                await ViewModel.LoadNextSliceAsync();
            }

            //if (lvDialogs.ScrollingHost.VerticalOffset < 1)
            //    UpdateTask();
        }

        //public async Task UpdateTask()
        //{
        //    await ViewModel.LoadNextSliceAsync();
        //}

        private void CheckMessageBoxEmpty()
        {
            if (txtMessage.IsEmpty)
            {
                btnSendMessage.Visibility = Visibility.Collapsed;
                btnStickers.Visibility = Visibility.Visible;
                btnVoiceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                btnStickers.Visibility = Visibility.Collapsed;
                btnVoiceMessage.Visibility = Visibility.Collapsed;
                btnSendMessage.Visibility = Visibility.Visible;
            }
        }

        private void txtMessage_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            CheckMessageBoxEmpty();
        }

        private void btnVoiceMessage_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await txtMessage.SendAsync();
        }

        private void btnDialogInfo_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.With is TLUserBase) //Se non è zuppa allora è pan bagnato
                ViewModel.NavigationService.Navigate(typeof(UserInfoPage), ViewModel.Peer.ToPeer());
            else if (ViewModel.With is TLChannel)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), ViewModel.Peer);
            else if (ViewModel.With is TLChat)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), ViewModel.Peer);
        }

        private async void fcbtnAttachPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                imgSingleImgThumbnail.Source = null;

                // Create the picker
                FileOpenPicker picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

                // Set the allowed filetypes
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");

                // Get the file
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var img = new BitmapImage();

                    // If image is big on mobile all will explode!
                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        await img.SetSourceAsync(stream);
                    }

                    imgSingleImgThumbnail.Source = img;
                    imgSingleImgThumbnail.Visibility = Visibility.Visible;
                    btnRemoveSingleImgThumbnail.Visibility = Visibility.Visible;
                    btnVoiceMessage.Visibility = Visibility.Collapsed;
                    btnSendMessage.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private void btnRemoveSingleImgThumbnail_Click(object sender, RoutedEventArgs e)
        {
            imgSingleImgThumbnail.Visibility = Visibility.Collapsed;
            btnRemoveSingleImgThumbnail.Visibility = Visibility.Collapsed;
            imgSingleImgThumbnail.Source = null;
            CheckMessageBoxEmpty();
        }

        private void btnClosePinnedMessage_Click(object sender, RoutedEventArgs e)
        {
            grdPinnedMessage.Visibility = Visibility.Collapsed;
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(Attach) as MenuFlyout;
            if (flyout != null)
            {
                //if (ActualWidth < 500)
                //{
                //    flyout.ShowAt((Button)sender, new Point(4, 44));
                //}
                //else
                {
                    flyout.ShowAt(Attach, new Point(4, -4));
                }
            }
        }

        private void AttachPickerFlyout_ItemClick(object sender, MediaSelectedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(Attach) as MenuFlyout;
            if (flyout != null)
            {
                flyout.Hide();
            }

            ViewModel.SendPhotoCommand.Execute(e.Item.Clone());
        }
    }

    public class MediaLibraryCollection : IncrementalCollection<StoragePhoto>, ISupportIncrementalLoading
    {
        public StorageFileQueryResult Query { get; private set; }

        public uint StartIndex { get; private set; }

        private CoreDispatcher _dispatcher;

        public MediaLibraryCollection()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled) return;

            var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
            queryOptions.FolderDepth = FolderDepth.Deep;

            Query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
            Query.ContentsChanged += OnContentsChanged;
            StartIndex = 0;

            _dispatcher = Window.Current.Dispatcher;
        }

        private async void OnContentsChanged(IStorageQueryResultBase sender, object args)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StartIndex = 0;
                Clear();
            });
        }

        public override async Task<IEnumerable<StoragePhoto>> LoadDataAsync()
        {
            var items = new List<StoragePhoto>();
            uint resultCount = 0;
            var result = await Query.GetFilesAsync(StartIndex, 10);
            StartIndex += (uint)result.Count;

            if (result.Count == 0)
            {
            }
            else
            {
                resultCount = (uint)result.Count();

                foreach (var file in result)
                {
                    items.Add(new StoragePhoto(file));
                }
            }

            return items;
        }
    }
}
