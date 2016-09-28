using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
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

        public bool isLoading = false;
        public DialogPage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Instance.ResolverType<DialogViewModel>();
            Loaded += DialogPage_Loaded;
            CheckMessageBoxEmpty();
        }

        private void DialogPage_Loaded(object sender, RoutedEventArgs e)
        {
            lvDialogs.ScrollingHost.ViewChanged += LvScroller_ViewChanged;
        }

        private void LvScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (lvDialogs.ScrollingHost.VerticalOffset == 0)
                UpdateTask();
            lvDialogs.ScrollingHost.UpdateLayout();
        }

        public async Task UpdateTask()
        {
            isLoading = true;
            await ViewModel.FetchMessages(ViewModel.peer, ViewModel.inputPeer);
            isLoading = false;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private void CheckMessageBoxEmpty()
        {
            if (txtMessage.IsEmpty)
            {
                btnSendMessage.Visibility = Visibility.Collapsed;
                btnVoiceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                btnVoiceMessage.Visibility = Visibility.Collapsed;
                btnSendMessage.Visibility = Visibility.Visible;
            }
        }

        private void txtMessage_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            CheckMessageBoxEmpty();

            // TODO Prevent "Enter" from being added to message string when pressed for sending.
            // See "Dispatcher_AcceleratorKeyActivated" for more info.

            // TODO Save text to draft if not being send

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
            var user = ViewModel.user;
            var channel = ViewModel.channel;
            var chat = ViewModel.chat;
            if (user!=null) //Se non è zuppa allora è pan bagnato
                ViewModel.NavigationService.Navigate(typeof(UserInfoPage), user);
            else if (channel!=null)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), channel);
            else if (chat != null)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), chat);

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
                StorageFile file = await picker.PickSingleFileAsync();
                BitmapImage img = new BitmapImage();

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
            catch { }
        }

        private void btnRemoveSingleImgThumbnail_Click(object sender, RoutedEventArgs e)
        {
            imgSingleImgThumbnail.Visibility = Visibility.Collapsed;
            btnRemoveSingleImgThumbnail.Visibility = Visibility.Collapsed;
            imgSingleImgThumbnail.Source = null;
            CheckMessageBoxEmpty();
        }

        private void btnSendMessage_Focus(object sender, RoutedEventArgs e)
        {
            if (txtMessage.FocusState == FocusState.Unfocused)
            {
                txtMessage.Margin = new Thickness(48,0,0,0);
                btnMore.Visibility = Visibility.Visible;
            }
            else
            {
                txtMessage.Margin = new Thickness(0);
                btnMore.Visibility = Visibility.Collapsed;
            }
        }

        private void btnClosePinnedMessage_Click(object sender, RoutedEventArgs e)
        {
            grdPinnedMessage.Visibility = Visibility.Collapsed;
        }
    }
}
