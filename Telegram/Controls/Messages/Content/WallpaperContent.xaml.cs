//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Controls.Chats;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages.Content
{
    public sealed class WallpaperContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public WallpaperContent(MessageViewModel message, bool album = false)
        {
            _message = message;
            DefaultStyleKey = typeof(WallpaperContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private ChatBackgroundPresenter Presenter;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Presenter = GetTemplateChild(nameof(Presenter)) as ChatBackgroundPresenter;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            _message = message;

            if (message.Content is MessageText text && Uri.TryCreate(text.LinkPreview?.Url, UriKind.Absolute, out Uri result))
            {
                var document = GetContent(message);
                var backgroundType = TdBackground.FromUri(result);
                var background = new Background(0, false, false, string.Empty, document, backgroundType);

                LayoutRoot.Constraint = background;
                Presenter.UpdateSource(message.ClientService, background, true);
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeBackground;
            }

            return false;
        }

        private Document GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeBackground previewBackground)
            {
                return previewBackground.Document;
            }

            return null;
        }
    }
}
