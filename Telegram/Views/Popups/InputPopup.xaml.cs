//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Windows.Globalization.NumberFormatting;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public enum InputPopupType
    {
        Text,
        Password,
        Value
    }

    public class InputPopupResult
    {
        public ContentDialogResult Result { get; set; }

        public string Text { get; set; }

        public double Value { get; set; }

        public InputPopupResult(ContentDialogResult result, string text, double value)
        {
            Result = result;
            Text = text;
            Value = value;
        }
    }

    public sealed partial class InputPopup : ContentPopup
    {
        public string Header { get; set; }

        public string Text { get; set; } = string.Empty;
        public double Value { get; set; }

        public string PlaceholderText { get; set; } = string.Empty;

        public int MaxLength { get; set; } = int.MaxValue;
        public int MinLength { get; set; } = 1;
        public double Maximum { get; set; } = double.MaxValue;

        public InputScopeNameValue InputScope { get; set; }
        public INumberFormatter2 Formatter { get; set; }

        public InputPopup(InputPopupType type = InputPopupType.Text)
        {
            InitializeComponent();

            switch (type)
            {
                case InputPopupType.Text:
                    FindName(nameof(Label));
                    break;
                case InputPopupType.Password:
                    FindName(nameof(Password));
                    break;
                case InputPopupType.Value:
                    FindName(nameof(Number));
                    break;
            }

            Opened += OnOpened;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (string.IsNullOrEmpty(Header))
            {
                MessageLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageLabel.Text = Header;
                MessageLabel.Visibility = Visibility.Visible;
            }

            if (Label != null)
            {
                Label.PlaceholderText = PlaceholderText;
                Label.Text = Text;
                Label.MaxLength = MaxLength;

                var scope = new InputScope();
                var name = new InputScopeName();

                name.NameValue = InputScope;
                scope.Names.Add(name);

                Label.InputScope = scope;

                Label.Focus(FocusState.Keyboard);
                Label.SelectionStart = Label.Text.Length;
            }
            else if (Password != null)
            {
                Password.PlaceholderText = PlaceholderText;
                Password.Password = Text;
                Password.MaxLength = MaxLength;

                Password.Focus(FocusState.Keyboard);
                Password.SelectAll();
            }
            else if (Number != null)
            {
                Number.NumberFormatter = Formatter;
                Number.Maximum = Maximum;
                Number.Value = Value;

                Number.Focus(FocusState.Keyboard);
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Label != null)
            {
                if (Label.Text.Length < MinLength)
                {
                    VisualUtilities.ShakeView(Label);
                    args.Cancel = true;
                    return;
                }

                Text = Label.Text;
            }
            else if (Password != null)
            {
                if (Password.Password.Length < MinLength)
                {
                    VisualUtilities.ShakeView(Password);
                    args.Cancel = true;
                    return;
                }

                Text = Password.Password;
            }
            else if (Number != null)
            {
                if (Number.Value < 0 || Number.Value > Maximum)
                {
                    VisualUtilities.ShakeView(Number);
                    args.Cancel = true;
                    return;
                }

                Value = Number.Value;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Label_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            Hide(ContentDialogResult.Primary);
        }

        private void Label_TextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = Label.Text.Length >= MinLength;
        }

        private void Label_PasswordChanged(object sender, RoutedEventArgs e)
        {
            IsPrimaryButtonEnabled = Password.Password.Length >= MinLength;
        }

        private void Number_ValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            IsPrimaryButtonEnabled = args.NewValue >= 0 && args.NewValue <= Maximum;
        }

        #region Static methods

        public static async Task<InputPopupResult> ShowAsync(InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new InputPopup(type)
            {
                Title = title ?? string.Empty,
                Header = message,
                PlaceholderText = placeholderText ?? string.Empty,
                PrimaryButtonText = primary,
                PrimaryButtonStyle = BootStrapper.Current.Resources[destructive ? "DangerButtonStyle" : "AccentButtonStyle"] as Style,
                SecondaryButtonText = secondary,
                RequestedTheme = requestedTheme
            };

            var confirm = await popup.ShowQueuedAsync();
            return new InputPopupResult(confirm, popup.Text, popup.Value);
        }

        public static async Task<InputPopupResult> ShowAsync(FrameworkElement target, InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var popup = new InputTeachingTip(type)
            {
                Title = title ?? string.Empty,
                Header = message,
                PlaceholderText = placeholderText ?? string.Empty,
                ActionButtonContent = primary,
                ActionButtonStyle = BootStrapper.Current.Resources[destructive ? "DangerButtonStyle" : "AccentButtonStyle"] as Style,
                CloseButtonContent = secondary,
                PreferredPlacement = target != null ? TeachingTipPlacementMode.Top : TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                Target = target,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
                // TODO:
                RequestedTheme = target?.ActualTheme ?? requestedTheme
            };

            var confirm = await popup.ShowAsync();
            return new InputPopupResult(confirm, popup.Text, popup.Value);
        }

        #endregion
    }
}
