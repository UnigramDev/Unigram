using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class InvoiceContent : StackPanel, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public InvoiceContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var invoice = message.Content as MessageInvoice;
            if (invoice == null)
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
