using LinqToVisualTree;
using System.Linq;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Popups
{
    public sealed partial class SettingsPasscodeInputPopup : ContentPopup
    {
        public SettingsPasscodeInputPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.Passcode;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public bool IsSimple
        {
            get
            {
                return Type.SelectedIndex == 0;
            }
            set
            {
                Type.SelectedIndex = value ? 0 : 1;
            }
        }

        public string Passcode { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsSimple && (First.Password.Length != 4 || !First.Password.All(x => x >= '0' && x <= '9')))
            {
                VisualUtilities.ShakeView(First);
                args.Cancel = true;
                return;
            }

            if (!First.Password.Equals(Confirm.Password))
            {
                VisualUtilities.ShakeView(Confirm);
                args.Cancel = true;
                return;
            }

            Passcode = First.Password.ToString();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void First_Changed(object sender, RoutedEventArgs e)
        {
            if (IsSimple && First.Password.Length == 4 && First.Password.All(x => x >= '0' && x <= '9'))
            {
                Confirm.Focus(FocusState.Keyboard);
            }
        }

        private void Confirm_Changed(object sender, RoutedEventArgs e)
        {
            if (IsSimple && First.Password.Length == 4 && First.Password.All(x => x >= '0' && x <= '9') && First.Password.Equals(Confirm.Password))
            {
                Done();
            }
        }

        private void Done()
        {
            var button = this.Descendants<Button>().FirstOrDefault(x => x is Button y && y.Parent is Border border && border.Name == "Button1Host") as Button;
            if (button != null)
            {
                new ButtonAutomationPeer(button).Invoke();
            }
        }

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key >= Windows.System.VirtualKey.Number0 && e.Key <= Windows.System.VirtualKey.Number9) { }
            else if (e.Key >= Windows.System.VirtualKey.NumberPad0 && e.Key <= Windows.System.VirtualKey.NumberPad9) { }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender == First)
                {
                    Confirm.Focus(FocusState.Keyboard);
                }
                else
                {
                    Done();
                }

                e.Handled = true;
            }
            else
            {
                e.Handled = IsSimple;
            }
        }

        private void Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scope = InputScopeNameValue.NumericPin;
            var maxLength = 4;

            switch (Type.SelectedIndex)
            {
                case 1:
                    scope = InputScopeNameValue.Password;
                    maxLength = int.MaxValue;
                    break;
            }

            First.Password = Confirm.Password = string.Empty;
            First.MaxLength = Confirm.MaxLength = maxLength;

            var firstScope = new InputScope();
            firstScope.Names.Add(new InputScopeName(scope));
            First.InputScope = firstScope;

            var confirmScope = new InputScope();
            confirmScope.Names.Add(new InputScopeName(scope));
            Confirm.InputScope = confirmScope;

            First.Focus(FocusState.Keyboard);
        }
    }
}
