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
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
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
            if (txtMessage.Text == "" || txtMessage.Text == null)
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

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {

            // Check if the "Enter" Key is pressed.
            if ((args.EventType == CoreAcceleratorKeyEventType.SystemKeyDown || args.EventType == CoreAcceleratorKeyEventType.KeyDown) && (args.VirtualKey == VirtualKey.Enter))
            {
                // Check if CTRL is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                // If there is text and CTRL is not pressed, send message. Else start new row.
                if (!ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down) && btnSendMessage.Visibility == Visibility.Visible)
                {

                    // TODO working but UGLY workaround: removal of the enter character from message.
                    // The character itself should not be added to the string from the begining.
                    // This will create a visual artefact for a fraction of a second, enlarging the 
                    // message box prior to sending and clearing the it.
                    txtMessage.Text = txtMessage.Text.Remove(txtMessage.Text.Length - 1);
                    ViewModel.SendTextHolder = txtMessage.Text;
                    if (ViewModel.SendCommand.CanExecute(null))
                        ViewModel.SendCommand.Execute(null);
                    txtMessage.Text = "";
                    args.Handled = true;
                }
            }
            
        }

        private void txtMessage_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            CheckMessageBoxEmpty();

            // TODO Prevent "Enter" from being added to message string when pressed for sending.
            // See "Dispatcher_AcceleratorKeyActivated" for more info.

            // TODO Save text to draft if not being send

        }

        private void btnVoiceMessage_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SendTextHolder = txtMessage.Text;
            txtMessage.Text = "";
        }

        private void btnDialogInfo_Click(object sender, RoutedEventArgs e)
        {
            var user = ViewModel.user;
            var channel = ViewModel.channel;
            var chat = ViewModel.chat;
            if(user!=null)
                ViewModel.NavigationService.Navigate(typeof(UserInfoPage), user);
            if(channel!=null)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), channel);
            if (chat != null)
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

        private async void ForwardCancel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ForwardMenuHideStoryboard.Begin();

            await Task.Delay(200);

            FContactsList.SelectedItem = null;

            ViewModel.CancelForward();

            ForwardHeader.Visibility = Visibility.Visible;
            ForwardSearchBox.Visibility = Visibility.Collapsed;
        }

        private void ForwardSearchButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ForwardHeader.Visibility = Visibility.Collapsed;
            ForwardSearchBox.Visibility = Visibility.Visible;
            ForwardSearchBox.Focus(FocusState.Pointer);
        }

        private void ForwardSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ForwardSearchBox.Text != "")
            {
                if (FContactsList.ItemsSource != ViewModel.FSearchDialogs)
                {
                    FContactsList.ItemsSource = ViewModel.FSearchDialogs;
                }
                ViewModel.GetSearchDialogs(ForwardSearchBox.Text);
            }
            else
            {
                FContactsList.ItemsSource = ViewModel.FDialogs;
            }
        }

        private void FContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ForwardButton.IsEnabled = (FContactsList.SelectedItems.Count != 0);
        }
    }
}
