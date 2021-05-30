using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Unigram.Controls.Messages.Content
{
    public sealed class InvoiceContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public InvoiceContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(InvoiceContent);
        }

        #region InitializeComponent

        private Run Title;
        private Run Description;
        private InvoiceFooter Footer;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Title = GetTemplateChild(nameof(Title)) as Run;
            Description = GetTemplateChild(nameof(Description)) as Run;
            Footer = GetTemplateChild(nameof(Footer)) as InvoiceFooter;

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

            Footer.UpdateMessage(message);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageInvoice invoice && invoice.Photo == null;
        }
    }
}
