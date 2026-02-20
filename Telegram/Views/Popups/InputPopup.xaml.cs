//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Views.Host;
using Windows.Globalization.NumberFormatting;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public enum InputPopupType
    {
        Text,
        Password,
        Value,
        Stars
    }

    public partial class InputPopupResult
    {
        public ContentDialogResult Result { get; set; }

        public string Text { get; set; }

        public long Value { get; set; }

        public InputPopupResult(ContentDialogResult result, string text, long value)
        {
            Result = result;
            Text = text;
            Value = value;
        }
    }

    public partial class InputPopupValidatingEventArgs : CancelEventArgs
    {
        public InputPopupValidatingEventArgs(string text, long value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public long Value { get; }
    }

    public partial class InputPopupValueChangedEventArgs
    {
        public InputPopupValueChangedEventArgs(string text, long value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public long Value { get; }

        public string Footer { get; set; }
    }

    public sealed partial class InputPopup : ContentPopup
    {
        public string Header { get; set; }

        public string Footer { get; set; }

        public string Text { get; set; } = string.Empty;
        public long Value { get; set; }

        public string PlaceholderText { get; set; } = string.Empty;

        public int MaxLength { get; set; } = int.MaxValue;
        public int MinLength { get; set; } = 1;

        public long Minimum { get; set; } = 0;
        public long Maximum { get; set; } = long.MaxValue;

        public InputScopeNameValue InputScope { get; set; }
        public INumberFormatter2 Formatter { get; set; }

        private readonly InputPopupType _type;

        public InputPopup(InputPopupType type = InputPopupType.Text)
        {
            InitializeComponent();
            Formatter = GetRegionalSettingsAwareDecimalFormatter();

            switch (_type = type)
            {
                case InputPopupType.Text:
                    FindName(nameof(Label));
                    Label.TextChanged += OnTextChanged;
                    break;
                case InputPopupType.Password:
                    FindName(nameof(Password));
                    Password.PasswordChanged += OnPasswordChanged;
                    break;
                case InputPopupType.Value:
                case InputPopupType.Stars:
                    FindName(nameof(Label));
                    Label.BeforeTextChanging += OnBeforeTextChanging;
                    break;
            }
        }

        internal const int LOCALE_NAME_MAX_LENGTH = 85;

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        private static partial int GetUserDefaultLocaleName(Span<char> buf, int bufferLength);

        private static string GetUserDefaultLocaleName()
        {
            Span<char> buffer = stackalloc char[LOCALE_NAME_MAX_LENGTH];

            int result = GetUserDefaultLocaleName(buffer, buffer.Length);
            if (result != 0)
            {
                int length = buffer.IndexOf('\0');
                if (length == -1)
                    length = result - 1;

                return new string(buffer.Slice(0, length));
            }
            return null;
        }
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetUserDefaultLocaleName(StringBuilder buf, int bufferLength);

        private static string GetUserDefaultLocaleName()
        {
            var sb = new StringBuilder(LOCALE_NAME_MAX_LENGTH);
            if (GetUserDefaultLocaleName(sb, LOCALE_NAME_MAX_LENGTH) != 0)
            {
                return sb.ToString();
            }

            return null;
        }
#endif

        // This was largely copied from Calculator's GetRegionalSettingsAwareDecimalFormatter()
        private DecimalFormatter GetRegionalSettingsAwareDecimalFormatter()
        {
            DecimalFormatter formatter = null;

            var currentLocale = GetUserDefaultLocaleName();
            if (currentLocale != null)
            {
                // GetUserDefaultLocaleName may return an invalid bcp47 language tag with trailing non-BCP47 friendly characters,
                // which if present would start with an underscore, for example sort order
                // (see https://msdn.microsoft.com/en-us/library/windows/desktop/dd373814(v=vs.85).aspx).
                // Therefore, if there is an underscore in the locale name, trim all characters from the underscore onwards.

                var underscore = currentLocale.IndexOf('_');
                if (underscore != -1)
                {
                    currentLocale.Substring(0, underscore);
                }

                if (Windows.Globalization.Language.IsWellFormed(currentLocale))
                {
                    formatter = new DecimalFormatter(new[] { currentLocale }, GlobalizationPreferences.HomeGeographicRegion);
                }
            }

            if (formatter == null)
            {
                formatter = new DecimalFormatter();
            }

            formatter.IntegerDigits = 1;
            formatter.FractionDigits = 0;

            return formatter;
        }

        public event EventHandler<InputPopupValidatingEventArgs> Validating;
        public event EventHandler<InputPopupValueChangedEventArgs> ValueChanged;

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            OnValueChanged(Text = Label.Text, 0);
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            OnValueChanged(Text = Password.Password, 0);
        }

        private void OnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            var parser = Formatter as INumberParser;

            var newValue = parser?.ParseInt(args.NewText);
            if (newValue >= Minimum && newValue <= Maximum)
            {
                OnValueChanged(string.Empty, Value = newValue.Value);
            }
            else
            {
                if (newValue > Maximum)
                {
                    VisualUtilities.QueueCallbackForCompositionRendered(InvalidateToMaximum);
                }

                args.Cancel = args.NewText.Length > 0;
            }
        }

        private void InvalidateToMaximum()
        {
            var selectionStart = Label.SelectionStart;

            Label.Text = Formatter.FormatInt(Maximum);
            Label.SelectionStart = selectionStart + 1;

            VisualUtilities.ShakeView(InputRoot);
        }

        private void OnValueChanged(string text, long value)
        {
            var handler = ValueChanged;
            if (handler != null)
            {
                var args = new InputPopupValueChangedEventArgs(text, value);
                handler(this, args);

                if (string.IsNullOrEmpty(args.Footer))
                {
                    FooterText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FooterText.Visibility = Visibility.Visible;
                    TextBlockHelper.SetMarkdown(FooterText, args.Footer);
                }
            }
        }

        public override void OnCreate()
        {
            if (string.IsNullOrEmpty(Header))
            {
                HeaderText.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextBlockHelper.SetMarkdown(HeaderText, Header);
                HeaderText.Visibility = Visibility.Visible;
            }

            if (string.IsNullOrEmpty(Footer))
            {
                FooterText.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextBlockHelper.SetMarkdown(FooterText, Footer);
                FooterText.Visibility = Visibility.Visible;
            }

            if (Label != null)
            {
                Label.PlaceholderText = PlaceholderText;

                var scope = new InputScope();
                var name = new InputScopeName();

                if (_type == InputPopupType.Text)
                {
                    Label.MaxLength = MaxLength;
                    Label.Text = Text;

                    name.NameValue = InputScope;
                }
                else
                {
                    Label.MaxLength = 0;
                    Label.Text = Formatter.FormatInt(Value);

                    name.NameValue = InputScopeNameValue.Number;
                }

                scope.Names.Add(name);
                Label.InputScope = scope;

                Label.Focus(FocusState.Keyboard);
                Label.SelectionStart = Label.Text.Length;

                if (_type == InputPopupType.Stars)
                {
                    Label.Padding = new Thickness(36, Label.Padding.Top, Label.Padding.Right, Label.Padding.Bottom);
                    FindName(nameof(StarCount));
                }
            }
            else if (Password != null)
            {
                Password.PlaceholderText = PlaceholderText;
                Password.Password = Text;
                Password.MaxLength = MaxLength;

                Password.Focus(FocusState.Keyboard);
                Password.SelectAll();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Label != null)
            {
                if (_type == InputPopupType.Text)
                {
                    if (Label.Text.Length < MinLength)
                    {
                        VisualUtilities.ShakeView(InputRoot);
                        return;
                    }

                    Text = Label.Text;
                }
                else
                {
                    var parser = Formatter as INumberParser;

                    var newValue = parser?.ParseInt(Label.Text);
                    if (newValue < Minimum || newValue > Maximum || newValue == null)
                    {
                        VisualUtilities.ShakeView(InputRoot);
                        return;
                    }

                    Value = newValue.Value;
                }
            }
            else if (Password != null)
            {
                if (Password.Password.Length < MinLength)
                {
                    VisualUtilities.ShakeView(InputRoot);
                    return;
                }

                Text = Password.Password;
            }

            if (Validating != null)
            {
                var temp = new InputPopupValidatingEventArgs(Text, Value);

                Validating(this, temp);

                if (temp.Cancel)
                {
                    VisualUtilities.ShakeView(InputRoot);
                    args.Cancel = true;
                }
            }
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

        #region Static methods

        public static async Task<InputPopupResult> ShowAsync(XamlRoot xamlRoot, InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
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

            var confirm = await popup.ShowQueuedAsync(xamlRoot);
            return new InputPopupResult(confirm, popup.Text, popup.Value);
        }

        public static async Task<InputPopupResult> ShowAsync(XamlRoot xamlRoot, FrameworkElement target, InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return null;
            }

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

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(popup);

            var confirm = await popup.ShowAsync();
            return new InputPopupResult(confirm, popup.Text, popup.Value);
        }

        #endregion
    }
}
