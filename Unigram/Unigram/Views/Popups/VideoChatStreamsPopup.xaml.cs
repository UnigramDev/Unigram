using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class VideoChatStreamsPopup : ContentPopup
    {
        private readonly IProtoService _protoService;
        private readonly long _chatId;

        public VideoChatStreamsPopup(IProtoService protoService, long chatId, bool start)
        {
            InitializeComponent();

            _protoService = protoService;
            _chatId = chatId;

            Title = "Stream with...";

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

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            var response = await _protoService.SendAsync(new GetVideoChatRtmpUrl(_chatId));
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
    }
}
