//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
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

        public object CheckBoxLabel
        {
            get => CheckBox.Content.ToString();
            set
            {
                CheckBox.Content = value;
                CheckBox.Visibility = (value is string str ? string.IsNullOrWhiteSpace(str) : value == null) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool? IsChecked
        {
            get => CheckBox.IsChecked;
            set => CheckBox.IsChecked = value;
        }

        private bool _isCheckedRequired;
        public bool IsCheckedRequired
        {
            get => _isCheckedRequired;
            set
            {
                _isCheckedRequired = value;

                if (value)
                {
                    IsPrimaryButtonEnabled = CheckBox.IsChecked == true;
                }
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IsPrimaryButtonEnabled = !_isCheckedRequired || CheckBox.IsChecked is true;
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, string message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new MessagePopup
            {
                Title = title ?? Strings.AppName,
                Message = message,
                PrimaryButtonText = primary ?? Strings.OK,
                SecondaryButtonText = secondary ?? string.Empty,
                CloseButtonText = tertiary ?? string.Empty,
            };

            if (requestedTheme != ElementTheme.Default)
            {
                popup.RequestedTheme = requestedTheme;
            }

            if (destructive)
            {
                popup.DefaultButton = ContentDialogButton.None;
                popup.PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return popup.ShowQueuedAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, FormattedText message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new MessagePopup
            {
                Title = title ?? Strings.AppName,
                FormattedMessage = message,
                PrimaryButtonText = primary ?? Strings.OK,
                SecondaryButtonText = secondary ?? string.Empty,
                CloseButtonText = tertiary ?? string.Empty
            };

            if (requestedTheme != ElementTheme.Default)
            {
                popup.RequestedTheme = requestedTheme;
            }

            if (destructive)
            {
                popup.DefaultButton = ContentDialogButton.None;
                popup.PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return popup.ShowQueuedAsync(xamlRoot);
        }

        // Nested: a ContentDialog cannot open over another one, so an alert raised while a popup
        // is already up is a ModalPopup instead. It used to be told apart by a FrameworkElement
        // target that every caller passed as null, and that the tip then pointed at nothing.
        public static Task<ContentDialogResult> ShowNestedAsync(XamlRoot xamlRoot, string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = CreateNested(title, primary, secondary, destructive, requestedTheme);
            popup.Subtitle = message;

            return popup.ShowAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowNestedAsync(XamlRoot xamlRoot, FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var content = new TextBlock
            {
                Style = BootStrapper.Current.Resources["BodyTextBlockStyle"] as Style
            };

            TextBlockHelper.SetFormattedText(content, message);

            var popup = CreateNested(title, primary, secondary, destructive, requestedTheme);
            popup.Content = content;

            return popup.ShowAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowNestedAsync(XamlRoot xamlRoot, string message, string title, FrameworkElement content, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = CreateNested(title, primary, secondary, destructive, requestedTheme);
            popup.Subtitle = message;
            popup.Content = content;

            return popup.ShowAsync(xamlRoot);
        }

        private static ModalPopup CreateNested(string title, string primary, string secondary, bool destructive, ElementTheme requestedTheme)
        {
            return new ModalPopup
            {
                Title = title,
                PrimaryButtonContent = primary,
                PrimaryButtonStyle = BootStrapper.Current.Resources[destructive ? "DangerButtonStyle" : "AccentButtonStyle"] as Style,
                SecondaryButtonContent = secondary,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
                RequestedTheme = requestedTheme
            };
        }
    }
}
