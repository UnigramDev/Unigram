using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Core.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls
{
    public sealed partial class TLMessageDialog : ContentDialog
    {
        public TLMessageDialog()
        {
            InitializeComponent();

            Opened += OnOpened;
            Closed += OnClosed;
        }

        public TLMessageDialog(string message)
            : this(message, null)
        {

        }

        public TLMessageDialog(string message, string title)
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
            var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
            var overlayBrush = Application.Current.Resources["SystemControlBackgroundAltMediumBrush"] as SolidColorBrush;

            if (overlayBrush != null)
            {
                var maskBackground = ColorsHelper.AlphaBlend(backgroundBrush.Color, overlayBrush.Color);
                var maskForeground = ColorsHelper.AlphaBlend(foregroundBrush.Color, overlayBrush.Color);

                titlebar.BackgroundColor = maskBackground;
                titlebar.ForegroundColor = maskForeground;
                titlebar.ButtonBackgroundColor = maskBackground;
                titlebar.ButtonForegroundColor = maskForeground;

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    var statusBar = StatusBar.GetForCurrentView();
                    statusBar.BackgroundColor = maskBackground;
                    statusBar.ForegroundColor = maskForeground;
                }
            }
        }

        private void UnmaskTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;

            titlebar.BackgroundColor = backgroundBrush.Color;
            titlebar.ForegroundColor = foregroundBrush.Color;
            titlebar.ButtonBackgroundColor = backgroundBrush.Color;
            titlebar.ButtonForegroundColor = foregroundBrush.Color;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = backgroundBrush.Color;
                statusBar.ForegroundColor = foregroundBrush.Color;
            }
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
            var dialog = new TLMessageDialog();
            dialog.Title = title;
            dialog.Message = message;
            dialog.PrimaryButtonText = primary ?? string.Empty;
            dialog.SecondaryButtonText = secondary ?? string.Empty;

            return dialog.ShowQueuedAsync();
        }
    }
}
