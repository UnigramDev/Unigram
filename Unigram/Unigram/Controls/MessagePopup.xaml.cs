//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;

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

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, string message, string title = null, string primary = null, string secondary = null, bool dangerous = false)
        {
            var popup = new MessagePopup
            {
                Title = title ?? Strings.Resources.AppName,
                Message = message,
                PrimaryButtonText = primary ?? Strings.Resources.OK,
                SecondaryButtonText = secondary ?? string.Empty
            };

            if (dangerous)
            {
                popup.DefaultButton = ContentDialogButton.None;
                popup.PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return popup.ShowQueuedAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, FormattedText message, string title = null, string primary = null, string secondary = null, bool dangerous = false)
        {
            var popup = new MessagePopup
            {
                Title = title ?? Strings.Resources.AppName,
                FormattedMessage = message,
                PrimaryButtonText = primary ?? Strings.Resources.OK,
                SecondaryButtonText = secondary ?? string.Empty
            };

            if (dangerous)
            {
                popup.DefaultButton = ContentDialogButton.None;
                popup.PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return popup.ShowQueuedAsync(xamlRoot);
        }
    }
}
