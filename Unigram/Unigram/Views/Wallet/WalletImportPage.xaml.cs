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
    public sealed partial class WalletImportPage : Page
    {
        public WalletImportViewModel ViewModel => DataContext as WalletImportViewModel;

        public WalletImportPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletImportViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            WordListPanel.Children.Clear();

            AutoSuggestBox CreateTextBox(WalletWordViewModel item, bool right)
            {
                var binding = new Binding();
                binding.Path = new PropertyPath("Text");
                binding.Mode = BindingMode.TwoWay;
                binding.Source = item;

                var text = new AutoSuggestBox();
                text.TextBoxStyle = App.Current.Resources["InlinePlaceholderTextBoxStyle"] as Style;
                text.PlaceholderText = $"{item.Index}. ";
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.Margin = new Thickness(0, 0, right ? 0 : 12, 8);
                text.TabIndex = item.Index;
                text.DataContext = item;

                text.TextChanged += Word_TextChanged;
                text.QuerySubmitted += Word_QuerySubmitted;

                text.SetBinding(AutoSuggestBox.TextProperty, binding);

                return text;
            }

            for (int i = 0; i < 24; i++)
            {
                WordListPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                var text1 = CreateTextBox(ViewModel.Items[i], false);
                var text2 = CreateTextBox(ViewModel.Items[++i], true);

                Grid.SetRow(text1, (i + 1) / 2 - 1);
                Grid.SetRow(text2, (i + 1) / 2 - 1);
                Grid.SetColumn(text2, 1);

                WordListPanel.Children.Add(text1);
                WordListPanel.Children.Add(text2);
            }
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

        private void Word_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            sender.ItemsSource = null;
            FocusNext(sender.TabIndex);
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
