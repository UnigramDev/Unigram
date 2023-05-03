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

namespace Telegram.Controls.Messages.Content
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

        private TextBlock Title;
        private TextBlock Description;
        private InvoiceFooter Footer;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            Description = GetTemplateChild(nameof(Description)) as TextBlock;
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
            TextBlockHelper.SetFormattedText(Description, invoice.Description);

            Footer.UpdateMessage(message);
        }

        public void Recycle()
        {
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageInvoice invoice && invoice.Photo == null;
        }
    }
}
