using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;

namespace Unigram.Controls
{
    public sealed partial class MessagePopup : ContentPopup
    {
        public MessagePopup()
        {
            InitializeComponent();
        }

        public MessagePopup(string message)
            : this(message, null)
        {

        }

        public MessagePopup(string message, string title)
        {
            InitializeComponent();

            Message = message;
            Title = title;
            PrimaryButtonText = "OK";
        }

        public string Message
        {
            get => TextBlockHelper.GetMarkdown(MessageLabel);
            set => TextBlockHelper.SetMarkdown(MessageLabel, value);
        }

        public FormattedText FormattedMessage
        {
            get => TextBlockHelper.GetFormattedText(MessageLabel);
            set => TextBlockHelper.SetFormattedText(MessageLabel, value);
        }

        public string CheckBoxLabel
        {
            get => CheckBox.Content.ToString();
            set
            {
                CheckBox.Content = value;
                CheckBox.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool? IsChecked
        {
            get => CheckBox.IsChecked;
            set => CheckBox.IsChecked = value;
        }

        public static Task<ContentDialogResult> ShowAsync(string message, string title = null, string primary = null, string secondary = null)
        {
            var popup = new MessagePopup
            {
                Title = title,
                Message = message,
                PrimaryButtonText = primary ?? string.Empty,
                SecondaryButtonText = secondary ?? string.Empty
            };

            return popup.ShowQueuedAsync();
        }

        public static Task<ContentDialogResult> ShowAsync(FormattedText message, string title = null, string primary = null, string secondary = null)
        {
            var popup = new MessagePopup
            {
                Title = title,
                FormattedMessage = message,
                PrimaryButtonText = primary ?? string.Empty,
                SecondaryButtonText = secondary ?? string.Empty
            };

            return popup.ShowQueuedAsync();
        }
    }
}
