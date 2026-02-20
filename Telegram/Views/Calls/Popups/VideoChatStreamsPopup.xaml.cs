//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Calls.Popups
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

            if (start)
            {
                PrimaryButtonText = Strings.VoipChannelStartStreaming;
                SecondaryButtonText = Strings.Cancel;
            }
            else
            {
                IsDismissButtonVisible = true;
                Description.Visibility = Visibility.Collapsed;
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

        private void Schedule_Click(object sender, RoutedEventArgs e)
        {
            IsScheduleSelected = true;
            Hide(ContentDialogResult.Primary);
        }

        private void CopyServer_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyText(XamlRoot, ServerField.Text);
        }

        private void CopyKey_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyText(XamlRoot, StreamKeyField.Text);
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(Revoke, Strings.RevokeStreamKey, Icons.DismissCircle);
            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedRight);

        }

        private async void Revoke()
        {
            var confirm = await MessagePopup.ShowAsync(XamlRoot, target: null, Strings.RevokeStreamKeyAlert, Strings.RevokeStreamKey, Strings.RevokeButton, Strings.Cancel, destructive: true, requestedTheme: ElementTheme.Dark);
            if (confirm == ContentDialogResult.Primary)
            {
                StreamKeyField.Text = string.Empty;

                var response = await _clientService.SendAsync(new ReplaceVideoChatRtmpUrl(_chatId));
                if (response is RtmpUrl rtmp)
                {
                    ServerField.Text = rtmp.Url;
                    StreamKeyField.Text = rtmp.StreamKey;
                }
            }
        }
    }
}
