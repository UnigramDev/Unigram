using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletTestPage : Page
    {
        public WalletTestViewModel ViewModel => DataContext as WalletTestViewModel;

        public WalletTestPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletTestViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }


        private string ConvertInfo(IList<int> indices)
        {
            return string.Format(Strings.Resources.WalletTestTimeInfo, indices[0] + 1, indices[1] + 1, indices[2] + 1);
        }

        private string ConvertPlaceholder(int index)
        {
            return $"{index + 1}. ";
        }

        private void Word_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            var hints = ViewModel.Hints;
            if (hints == null)
            {
                return;
            }

            if (sender.Text.Length >= 3)
            {
                sender.ItemsSource = hints.Where(x => x.StartsWith(sender.Text, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                sender.ItemsSource = null;
            }
        }

        private void Word_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.ItemsSource = null;
            FocusNext(sender.TabIndex);
        }

        private void Word_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;

            var text = sender as AutoSuggestBox;
            FocusNext(text.TabIndex);
        }

        private void FocusNext(int index)
        {
            var next = WordListPanel.Children.FirstOrDefault(x => x is AutoSuggestBox control && control.TabIndex == index + 1) as AutoSuggestBox;
            if (next != null)
            {
                next.Focus(FocusState.Keyboard);
            }
            else
            {
                ViewModel.SendCommand.Execute();
            }
        }
    }
}
