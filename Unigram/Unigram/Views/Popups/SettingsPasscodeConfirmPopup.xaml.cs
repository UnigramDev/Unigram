using LinqToVisualTree;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Popups
{
    public sealed partial class SettingsPasscodeConfirmPopup : ContentPopup
    {
        private readonly Func<string, Task<bool>> _verify;

        public SettingsPasscodeConfirmPopup(Func<string, Task<bool>> verify, bool simple)
        {
            InitializeComponent();

            _verify = verify;

            Title = Strings.Resources.Passcode;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            var confirmScope = new InputScope();
            confirmScope.Names.Add(new InputScopeName(simple ? InputScopeNameValue.NumericPin : InputScopeNameValue.Password));
            Confirm.InputScope = confirmScope;
            Confirm.MaxLength = simple ? 4 : int.MaxValue;
        }

        public bool IsSimple { get; set; }

        public string Passcode { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

            //if (/*Confirm.Password.Length != 4 || !Confirm.Password.All(x => x >= '0' && x <= '9') ||*/ !_passcodeService.Check(Confirm.Password))
            if (await _verify(Confirm.Password))
            {
                VisualUtilities.ShakeView(Confirm);
                Confirm.Password = string.Empty;
                args.Cancel = true;
            }

            Passcode = Confirm.Password.ToString();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Confirm_Changed(object sender, RoutedEventArgs e)
        {
            if (IsSimple && Confirm.Password.Length == 4 && Confirm.Password.All(x => x >= '0' && x <= '9'))
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
                Done();
                e.Handled = true;
            }
            else
            {
                e.Handled = IsSimple;
            }
        }
    }
}
