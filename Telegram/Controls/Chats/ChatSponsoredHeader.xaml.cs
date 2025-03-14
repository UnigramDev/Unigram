//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Monetization.Popups;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatSponsoredHeader : HyperlinkButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private IClientService _clientService;
        private UIElement _parent;

        private long _thumbnailToken;

        public ChatSponsoredHeader()
        {
            InitializeComponent();
        }

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
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

        private void UpdateFile(object target, File file)
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

        private bool _collapsed = true;

        private async void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            if (show)
            {
                await this.UpdateLayoutAsync();
            }

            var parent = ElementComposition.GetElementVisual(_parent);
            var visual = ElementComposition.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_collapsed)
                {
                    Visibility = Visibility.Collapsed;
                }
                else
                {
                    ViewModel.ViewSponsoredMessage();
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, ActualSize.Y);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -ActualSize.Y, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
