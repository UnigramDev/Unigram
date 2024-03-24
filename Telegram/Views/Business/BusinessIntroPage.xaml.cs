using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Streams;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessIntroPage : HostedPage
    {
        public BusinessIntroViewModel ViewModel => DataContext as BusinessIntroViewModel;

        public BusinessIntroPage()
        {
            InitializeComponent();
            Title = Strings.BusinessIntro;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);

            StickerPanel.DataContext = StickerDrawerViewModel.Create(ViewModel.SessionId);
            UpdateSticker();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Sticker))
            {
                UpdateSticker();
            }
            else if (e.PropertyName == "TITLE_INVALID")
            {
                VisualUtilities.ShakeView(TitleText);
            }
            else if (e.PropertyName == "MESSAGE_INVALID")
            {
                VisualUtilities.ShakeView(MessageText);
            }
        }

        private void UpdateSticker()
        {
            var sticker = ViewModel.Sticker ?? ViewModel.ClientService.NextGreetingSticker();
            if (sticker != null)
            {
                Animated.Source = new DelayedFileSource(ViewModel.ClientService, sticker);
            }
            else
            {
                Animated.Source = null;
            }

            if (ViewModel.Sticker != null)
            {
                StickerButton.Badge = new AnimatedImage
                {
                    Width = 32,
                    Height = 32,
                    FrameSize = new Windows.Foundation.Size(32, 32),
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    Source = new DelayedFileSource(ViewModel.ClientService, sticker)
                };
            }
            else
            {
                StickerButton.Badge = Strings.BusinessIntroStickerRandom;
            }
        }

        #region Binding

        private string ConvertTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Strings.NoMessages;
            }

            return value;
        }

        private string ConvertMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Strings.NoMessagesGreetingsDescription;
            }

            return value;
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TitleText.Focus(FocusState.Pointer);
            TitleText.SelectionStart = int.MaxValue;
        }

        private void Sticker_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            StickerPanel.ViewModel.Update(null);
            StickerFlyout.ShowAt(sender as UIElement, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Sticker_ItemClick(object sender, StickerDrawerItemClickEventArgs e)
        {
            StickerFlyout.Hide();
            ViewModel.Sticker = e.Sticker;
        }
    }
}
