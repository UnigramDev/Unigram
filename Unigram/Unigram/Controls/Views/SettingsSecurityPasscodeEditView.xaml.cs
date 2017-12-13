using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class SettingsSecurityPasscodeEditView : ContentDialog
    {
        public SettingsSecurityPasscodeEditView()
        {
            InitializeComponent();

            Title = Strings.Android.PasscodePIN;
            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;
        }

        public string Passcode { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (First.Password.Length != 4 || !First.Password.All(x => x >= '0' && x <= '9'))
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
            if (First.Password.Length == 4 && First.Password.All(x => x >= '0' &&  x <= '9'))
            {
                Confirm.Focus(FocusState.Keyboard);
            }
        }

        private void Confirm_Changed(object sender, RoutedEventArgs e)
        {
            if (First.Password.Length == 4 && First.Password.All(x => x >= '0' && x <= '9') && First.Password.Equals(Confirm.Password))
            {
                var button = this.Descendants<Button>().FirstOrDefault(x => x is Button y && y.Parent is Border border && border.Name == "Button1Host") as Button;
                if (button != null)
                {
                    new ButtonAutomationPeer(button).Invoke();
                }
            }
        }

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key >= Windows.System.VirtualKey.Number0 && e.Key <= Windows.System.VirtualKey.Number9) { }
            else if (e.Key >= Windows.System.VirtualKey.NumberPad0 && e.Key <= Windows.System.VirtualKey.NumberPad9) { }
            else
            {
                e.Handled = true;
            }
        }
    }
}
