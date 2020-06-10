using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class MessagePopup : ContentPopup
    {
        public MessagePopup()
        {
            InitializeComponent();

            Opened += OnOpened;
            Closed += OnClosed;
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

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            MaskTitleAndStatusBar();
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            UnmaskTitleAndStatusBar();
        }

        private void MaskTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["PageHeaderBackgroundBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
            var overlayBrush = Application.Current.Resources["SystemControlBackgroundAltMediumBrush"] as SolidColorBrush;

            if (overlayBrush != null)
            {
                var maskBackground = ColorsHelper.AlphaBlend(backgroundBrush.Color, overlayBrush.Color);
                var maskForeground = ColorsHelper.AlphaBlend(foregroundBrush.Color, overlayBrush.Color);

                titlebar.BackgroundColor = maskBackground;
                titlebar.ForegroundColor = maskForeground;
                //titlebar.ButtonBackgroundColor = maskBackground;
                titlebar.ButtonForegroundColor = maskForeground;
            }
        }

        private void UnmaskTitleAndStatusBar()
        {
            TLWindowContext.GetForCurrentView().UpdateTitleBar();
        }

        public string Message
        {
            get
            {
                return TextBlockHelper.GetMarkdown(MessageLabel);
            }
            set
            {
                TextBlockHelper.SetMarkdown(MessageLabel, value);
            }
        }

        public FormattedText FormattedMessage
        {
            get
            {
                return TextBlockHelper.GetFormattedText(MessageLabel);
            }
            set
            {
                TextBlockHelper.SetFormattedText(MessageLabel, value);
            }
        }

        public string CheckBoxLabel
        {
            get
            {
                return CheckBox.Content.ToString();
            }
            set
            {
                CheckBox.Content = value;
                CheckBox.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool? IsChecked
        {
            get
            {
                return CheckBox.IsChecked;
            }
            set
            {
                CheckBox.IsChecked = value;
            }
        }

        public static Task<ContentDialogResult> ShowAsync(string message, string title = null, string primary = null, string secondary = null)
        {
            var dialog = new MessagePopup();
            dialog.Title = title;
            dialog.Message = message;
            dialog.PrimaryButtonText = primary ?? string.Empty;
            dialog.SecondaryButtonText = secondary ?? string.Empty;

            return dialog.ShowQueuedAsync();
        }

        public static Task<ContentDialogResult> ShowAsync(FormattedText message, string title = null, string primary = null, string secondary = null)
        {
            var dialog = new MessagePopup();
            dialog.Title = title;
            dialog.FormattedMessage = message;
            dialog.PrimaryButtonText = primary ?? string.Empty;
            dialog.SecondaryButtonText = secondary ?? string.Empty;

            return dialog.ShowQueuedAsync();
        }
    }
}
