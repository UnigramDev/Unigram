//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed class InvoicePhotoContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _thumbnailToken;

        public InvoicePhotoContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(InvoicePhotoContent);
        }

        #region InitializeComponent

        private TextBlock Title;
        private TextBlock Description;
        private InvoiceFooter Footer;
        private AspectView Photo;
        private Image Texture;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            Description = GetTemplateChild(nameof(Description)) as TextBlock;
            Footer = GetTemplateChild(nameof(Footer)) as InvoiceFooter;
            Photo = GetTemplateChild(nameof(Photo)) as AspectView;
            Texture = GetTemplateChild(nameof(Texture)) as Image;

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

            Title.Text = invoice.Title;
            TextBlockHelper.SetFormattedText(Description, invoice.Description);

            Photo.Constraint = invoice.Photo;
            Texture.Source = null;

            Footer.UpdateMessage(message);

            var small = invoice.Photo.GetSmall();
            if (small != null)
            {
                UpdateManager.Subscribe(this, message, small.Photo, ref _thumbnailToken, UpdateFile, true);
                UpdateThumbnail(message, small.Photo);
            }
        }

        private void UpdateFile(object target, File file)
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

            var small = invoice.Photo.GetSmall();
            if (small != null && small.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Texture.Source = new BitmapImage(UriEx.ToLocal(file.Local.Path));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);
            }
        }

        public void Recycle()
        {
            _thumbnailToken = null;
            UpdateManager.Unsubscribe(this);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageInvoice invoice && invoice.Photo != null;
        }
    }
}
