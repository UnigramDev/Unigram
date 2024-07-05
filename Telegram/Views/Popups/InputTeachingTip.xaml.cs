//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Globalization.NumberFormatting;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class InputTeachingTip : TeachingTip
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

        private readonly InputPopupType _type;

        private readonly TaskCompletionSource<ContentDialogResult> _tsc = new();
        private readonly RelayCommand _actionButtonCommand;
        private bool _actionButtonEnabled;

        public InputTeachingTip(InputPopupType type = InputPopupType.Text)
        {
            InitializeComponent();

            switch (_type = type)
            {
                case InputPopupType.Text:
                    FindName(nameof(Label));
                    break;
                case InputPopupType.Password:
                    FindName(nameof(Password));
                    break;
                case InputPopupType.Value:
                case InputPopupType.Stars:
                    FindName(nameof(Number));
                    break;
            }

            ActionButtonCommand = _actionButtonCommand = new RelayCommand(ActionButtonExecute, () => _actionButtonEnabled);

            (Content as FrameworkElement).Loaded += OnOpened;
            Closed += OnClosed;
        }

        public event EventHandler<InputPopupValidatingEventArgs> Validating;

        private void OnOpened(object sender, RoutedEventArgs args)
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
                if (Formatter != null)
                {
                    Number.NumberFormatter = Formatter;
                }

                Number.Maximum = Maximum;
                Number.Value = Value;

                Number.Focus(FocusState.Keyboard);

                if (_type == InputPopupType.Stars)
                {
                    Number.Padding = new Thickness(28, Number.Padding.Top, Number.Padding.Right, Number.Padding.Bottom);
                    FindName(nameof(StarCount));
                }
            }
        }

        private void ActionButtonExecute()
        {
            FrameworkElement target = null;
            if (Label != null)
            {
                if (Label.Text.Length < MinLength)
                {
                    VisualUtilities.ShakeView(Label);
                    return;
                }

                target = Label;
                Text = Label.Text;
            }
            else if (Password != null)
            {
                if (Password.Password.Length < MinLength)
                {
                    VisualUtilities.ShakeView(Password);
                    return;
                }

                target = Password;
                Text = Password.Password;
            }
            else if (Number != null)
            {
                if (Number.Value < 0 || Number.Value > Maximum)
                {
                    VisualUtilities.ShakeView(Number);
                    return;
                }

                target = Number;
                Value = Number.Value;
            }

            if (Validating != null && target != null)
            {
                var temp = new InputPopupValidatingEventArgs(Text, Value);

                Validating(this, temp);

                if (temp.Cancel)
                {
                    VisualUtilities.ShakeView(target);
                    return;
                }
            }

            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
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

            _actionButtonCommand.Execute();
        }

        private void Label_TextChanged(object sender, TextChangedEventArgs e)
        {
            _actionButtonEnabled = Label.Text.Length >= MinLength;
            _actionButtonCommand.RaiseCanExecuteChanged();
        }

        private void Label_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _actionButtonEnabled = Password.Password.Length >= MinLength;
            _actionButtonCommand.RaiseCanExecuteChanged();
        }

        private void Number_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            _actionButtonEnabled = args.NewValue >= 0 && args.NewValue <= Maximum;
            _actionButtonCommand.RaiseCanExecuteChanged();
        }

        private void OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            _tsc.TrySetResult(ContentDialogResult.Secondary);
        }

        public Task<ContentDialogResult> ShowAsync()
        {
            IsOpen = true;
            return _tsc.Task;
        }
    }
}
