using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages.Content
{
    public sealed class InvoicePhotoContent : Control, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public InvoicePhotoContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(InvoicePhotoContent);
        }

        #region InitializeComponent

        private Run Title;
        private Run Description;
        private InvoiceFooter Footer;
        private AspectView Photo;
        private Image Texture;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Title = GetTemplateChild(nameof(Title)) as Run;
            Description = GetTemplateChild(nameof(Description)) as Run;
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
            Description.Text = invoice.Description;

            Photo.Constraint = invoice.Photo;
            Texture.Source = null;

            Footer.UpdateMessage(message);

            var small = invoice.Photo.GetSmall();
            if (small != null)
            {
                UpdateThumbnail(message, small.Photo);
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
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
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageInvoice invoice && invoice.Photo != null;
        }
    }
}
