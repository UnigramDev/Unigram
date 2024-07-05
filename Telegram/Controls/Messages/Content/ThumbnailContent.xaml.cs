//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed class ThumbnailContent : Control, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;

        public ThumbnailContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(ThumbnailContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private ImageBrush Texture;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Texture = GetTemplateChild(nameof(Texture)) as ImageBrush;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            var prevId = _message?.Id;
            var nextId = message?.Id;

            _message = message;

            var small = GetContent(message);
            if (small == null || !_templateApplied)
            {
                return;
            }

            UpdateManager.Subscribe(this, message, small.File, ref _fileToken, UpdateFile, true);
            UpdateFile(message, small.File);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var small = GetContent(message);
            if (small == null || !_templateApplied)
            {
                return;
            }

            if (small.File.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)44 / small.Width;
                double ratioY = (double)44 / small.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(small.Width * ratio);
                var height = (int)(small.Height * ratio);

                Texture.ImageSource = UriEx.ToBitmap(file.Local.Path, width, height);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);
            }
        }

        public void Recycle()
        {
            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageText text
                && text.LinkPreview != null
                && text.LinkPreview.HasThumbnail();
        }

        private Thumbnail GetContent(MessageViewModel message)
        {
            var content = message?.GeneratedContent ?? message?.Content;
            if (content is MessageText text)
            {
                return text.LinkPreview?.GetThumbnail();
            }

            return null;
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {

        }
    }
}
