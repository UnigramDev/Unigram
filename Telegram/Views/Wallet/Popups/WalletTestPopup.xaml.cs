//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    public sealed partial class WalletTestPopup : ContentPopup
    {
        private readonly IWalletService _wallet;
        private readonly INavigationService _navigationService;

        private IReadOnlyList<string> _mnemonicWordList;
        private IReadOnlyList<string> _mnemonic;
        private int[] _indexes;
        private string[] _words;

        public WalletTestPopup(IWalletService wallet, INavigationService navigationService)
        {
            _wallet = wallet;
            _navigationService = navigationService;

            InitializeComponent();
            InitializeWords();

            PrimaryButtonText = Strings.Import;
            SecondaryButtonText = Strings.Cancel;
        }

        private async void InitializeWords()
        {
            _mnemonicWordList = WalletService.RecoveryWords;
            _mnemonic = await _wallet.RevealRecoveryPhraseAsync();

            var random = new Random();

            _indexes = new int[3];
            _words = new string[3];
            Words.Children.Clear();

            for (int i = 0; i < 3; i++)
            {
                _indexes[i] = random.Next(0, _mnemonic.Count);

                var local = _indexes[i];
                var textBox = new MnemonicTextBox
                {
                    Index = i,
                    Padding = new Thickness(32, 5, 6, 6),
                };

                textBox.TextChanged += (s, args) =>
                {
                    _words[i] = textBox.Text;
                    //textBox.ItemsSource = _mnemonicWordList.Where(x => x.StartsWith(textBox.Text, StringComparison.OrdinalIgnoreCase)).ToList();
                };

                textBox.TextChanged += TextBox_TextChanged;

                _words[i] = string.Empty;

                var content = new Grid();
                var position = new TextBlock
                {
                    Text = $"{local + 1}.",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 5, 6, 6)
                };

                content.Children.Add(textBox);
                content.Children.Add(position);

                Words.Children.Add(content);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is MnemonicTextBox textBox)
            {
                _words[textBox.Index] = textBox.Text;
                textBox.HasError = !_mnemonicWordList.Contains(textBox.Text);
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_words[i] != _mnemonic[_indexes[i]])
                {
                    args.Cancel = true;
                    return;
                }
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
