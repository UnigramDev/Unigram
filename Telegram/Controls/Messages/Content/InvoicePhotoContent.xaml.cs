//
// Copyright (c) Fela Ameghino 2015-2026
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
    public sealed partial class InvoicePhotoContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _thumbnailToken;

        public InvoicePhotoContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(InvoicePhotoContent);
            Telegram.Common.Instrumentation.Register(this);
        }

        #region InitializeComponent

        private TextBlock Title;
        private TextBlock Description;
        private InvoiceFooter Footer;
        private AspectView Photo;
        private ImageBrush Texture;

        private ThumbnailController _thumbnailController;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            Description = GetTemplateChild(nameof(Description)) as TextBlock;
            Footer = GetTemplateChild(nameof(Footer)) as InvoiceFooter;
            Photo = GetTemplateChild(nameof(Photo)) as AspectView;
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
            _message = message;

            var invoice = message.Content as MessageInvoice;
            if (invoice == null || !_templateApplied)
            {
                return;
            }

            Title.Text = invoice.ProductInfo.Title;
            TextBlockHelper.SetFormattedText(Description, invoice.ProductInfo.Description);

            Photo.Constraint = invoice.ProductInfo.Photo;

            // Rebinding to another invoice: drop the previous image so it cannot show
            // through until the new one is decoded.
            _thumbnailController?.Recycle();

            if (invoice.Currency == "XTR")
            {
                Footer.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else
            {
                Footer.UpdateMessage(message);
            }

            var small = invoice.ProductInfo.Photo.GetSmall();
            if (small != null)
            {
                UpdateManager.Subscribe(this, message, small.Photo, ref _thumbnailToken, UpdateFile, true);
                UpdateThumbnail(message, small.Photo);
            }
        }

        private void UpdateFile(File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var invoice = message.Content as MessageInvoice;
            if (invoice == null || !_templateApplied)
            {
                return;
            }

            var small = invoice.ProductInfo.Photo.GetSmall();
            if (small != null && small.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            _thumbnailController ??= new ThumbnailController(Texture);

            if (file.Local.IsDownloadingCompleted)
            {
                _thumbnailController.Bitmap(file.Local.Path, hashCode: HashCode.Combine(message.ChatId, message.Id));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);
            }
        }

        public void Recycle()
        {
            _message = null;
            _thumbnailController?.Recycle();

            UpdateManager.Unsubscribe(this, ref _thumbnailToken);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageInvoice invoice && invoice.ProductInfo.Photo != null;
        }
    }
}
