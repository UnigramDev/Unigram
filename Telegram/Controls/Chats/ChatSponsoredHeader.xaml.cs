//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Telegram.Views.Monetization.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatSponsoredHeader : HyperlinkButton, ISlidePanelChild
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private IClientService _clientService;

        private ChatView _chatView;
        private UIElement _parent;

        private long _thumbnailToken;

        public ChatSponsoredHeader()
        {
            InitializeComponent();

            _collapsed = new SlidePanel.SlideState(this, false, 0);
        }

        public float AnimatedHeight => _collapsed ? 0 : ActualSize.Y;

        public void InitializeParent(ChatView chatView, UIElement parent)
        {
            _chatView = chatView;
            //_collapsed = new SlidePanel.SlideState(_parent = parent, false, 0);
        }

        public void UpdateSponsoredMessage(IClientService clientService, Chat chat, SponsoredMessage message)
        {
            _clientService = clientService;

            if (message == null || chat.Type is not ChatTypePrivate)
            {
                ShowHide(false);
                return;
            }

            ShowHide(true);

            var caption = message.Content.GetCaption();

            TitleText.Text = message.Title;
            MessageText.SetText(_clientService, caption);

            var small = message.Sponsor.Photo?.GetSmall();
            if (small != null)
            {
                UpdateManager.Subscribe(this, _clientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                UpdateThumbnail(small.Photo);

                ThumbRoot.Visibility = Visibility.Visible;
            }
            else
            {
                ThumbRoot.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFile(File file)
        {
            UpdateThumbnail(file);
        }

        private void UpdateThumbnail(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                ThumbImage.ImageSource = UriEx.ToBitmap(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                _clientService.DownloadFile(file.Id, 1);
            }
        }

        private void SponsoredMessage_Click(object sender, RoutedEventArgs e)
        {
            var message = ViewModel.SponsoredMessage;
            if (message == null)
            {
                return;
            }

            ViewModel.ClientService.Send(new ClickChatSponsoredMessage(ViewModel.Chat.Id, message.MessageId, false, false));
            ViewModel.OpenUrl(message.Sponsor.Url, false);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ShowPopup(new AboutAdsPopup(ViewModel, ViewModel.SponsoredMessage));
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.HideSponsoredMessage();
        }

        private SlidePanel.SlideState _collapsed;

        public SlidePanel.SlideState SlideState => _collapsed;

        private bool _isFirst;
        public bool IsFirst
        {
            get => _isFirst;
            set
            {
                if (_isFirst != value)
                {
                    UpdateAttach(_isFirst = value, _isLast);
                }
            }
        }

        private bool _isLast;
        public bool IsLast
        {
            get => _isLast;
            set
            {
                if (_isLast != value)
                {
                    UpdateAttach(_isFirst, _isLast = value);
                }
            }
        }

        private double _cardRadius = 16;
        public double CardRadius
        {
            get => _cardRadius;
            set
            {
                if (_cardRadius != value)
                {
                    _cardRadius = value;
                    UpdateAttach(_isFirst, _isLast);
                }
            }
        }

        private void UpdateAttach(bool first, bool last)
        {
            var radiusTop = first ? _cardRadius : 0;
            var radiusBottom = last ? _cardRadius : 0;

            CornerRadius = new CornerRadius(radiusTop, radiusTop, radiusBottom, radiusBottom);
            Thickness.Opacity = last ? 0 : 1;
        }

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            if (show)
            {
                ViewModel.ViewSponsoredMessage();
            }

            _collapsed.IsVisible = show;
            _chatView.UpdateMessagesHeaderPadding();
        }

        public IEnumerable<UIElement> GetAnimatableVisuals()
        {
            if (_collapsed)
            {
                yield break;
            }

            if (ThumbRoot.Visibility == Visibility.Visible)
            {
                yield return ThumbRoot;
            }
            else
            {
                yield return RemoveButton;
            }
        }
    }
}
