//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;

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
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;

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

        private void Schedule_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            IsScheduleSelected = true;
            Hide(ContentDialogResult.Primary);
        }
    }
}
