using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class VideoChatStreamsPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;

        public VideoChatStreamsPopup(IClientService clientService, long chatId, bool start)
        {
            InitializeComponent();

            _clientService = clientService;
            _chatId = chatId;

            Title = "Stream with...";
            Schedule.Visibility = start
                ? Windows.UI.Xaml.Visibility.Visible
                : Windows.UI.Xaml.Visibility.Collapsed;

            if (start)
            {
                PrimaryButtonText = Strings.Resources.Start;
                SecondaryButtonText = Strings.Resources.Cancel;
            }
            else
            {
                PrimaryButtonText = Strings.Resources.OK;
            }
        }

        public bool IsScheduleSelected { get; private set; }

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            var response = await _clientService.SendAsync(new GetVideoChatRtmpUrl(_chatId));
            if (response is RtmpUrl rtmp)
            {
                ServerField.Text = rtmp.Url;
                StreamKeyField.Text = rtmp.StreamKey;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Schedule_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            IsScheduleSelected = true;
            Hide(ContentDialogResult.Primary);
        }
    }
}
