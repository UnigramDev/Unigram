//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class InvoicePreviewContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public InvoicePreviewContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(InvoicePreviewContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private Border Overlay;
        private TextBlock Subtitle;
        private FileButton Button;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as FileButton;

            Button.Click += Button_Click;

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

            var preview = GetContent(message);
            if (preview == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = message;
            LayoutRoot.Background = null;

            if (preview.Duration > 0)
            {
                Subtitle.Text = preview.GetDuration();
                Overlay.Visibility = Visibility.Visible;
            }
            else
            {
                Overlay.Visibility = Visibility.Collapsed;
            }

            if (_message.ReplyMarkup is ReplyMarkupInlineKeyboard keyboard
                && keyboard.Rows.Count == 1
                && keyboard.Rows[0].Count == 1)
            {
                Button.Content = keyboard.Rows[0][0].Text;
            }
            else
            {
                Button.Content = null;
            }

            Button.SetGlyph(0, MessageContentState.Unlock);

            UpdateThumbnail(message, preview.Minithumbnail);
        }

        private void UpdateThumbnail(MessageViewModel message, Minithumbnail minithumbnail)
        {
            BitmapImage source = null;
            ImageBrush brush;

            if (LayoutRoot.Background is ImageBrush existing)
            {
                brush = existing;
            }
            else
            {
                brush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                LayoutRoot.Background = brush;
            }

            if (minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
            }

            brush.ImageSource = source;
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaPreview)
            {
                return true;
            }

            return false;
        }

        private PaidMediaPreview GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaPreview paidMedia)
            {
                return paidMedia;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_message.ReplyMarkup is ReplyMarkupInlineKeyboard keyboard
                && keyboard.Rows.Count == 1
                && keyboard.Rows[0].Count == 1)
            {
                _message.Delegate.OpenInlineButton(_message, keyboard.Rows[0][0]);
            }
        }
    }
}
