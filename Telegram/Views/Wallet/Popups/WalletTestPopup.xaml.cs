//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Asks for three words of the phrase, to show it was written down.
    /// </summary>
    /// <remarks>
    /// Takes the words rather than the service: the caller has just shown them, so it has them.
    /// </remarks>
    public sealed partial class WalletTestPopup : ModalPopup
    {
        private const int Count = 3;

        private readonly IReadOnlyList<string> _mnemonic;
        private readonly int[] _indexes = new int[Count];
        private readonly string[] _words = new string[Count];

        public WalletTestPopup(IReadOnlyList<string> mnemonic)
        {
            _mnemonic = mnemonic;

            InitializeComponent();
            InitializeWords();

            PrimaryButtonText = Strings.WalletContinue;
            SecondaryButtonText = Strings.Cancel;
        }

        private void InitializeWords()
        {
            // Distinct, and in the order they appear in the phrase, so the user reads down their
            // copy once instead of jumping back and forth.
            var random = new Random();
            var picked = new SortedSet<int>();

            while (picked.Count < Count)
            {
                picked.Add(random.Next(0, _mnemonic.Count));
            }

            picked.CopyTo(_indexes);

            TextBlockHelper.SetMarkdown(Description, string.Format(Strings.WalletTestPhraseInfo, _indexes[0] + 1, _indexes[1] + 1, _indexes[2] + 1));

            for (int i = 0; i < Count; i++)
            {
                _words[i] = string.Empty;

                var textBox = new MnemonicTextBox
                {
                    Index = i,
                    Padding = new Thickness(32, 5, 6, 6),
                };

                textBox.TextChanged += TextBox_TextChanged;

                var position = new TextBlock
                {
                    Text = $"{_indexes[i] + 1}.",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 5, 6, 6)
                };

                var content = new Grid();
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
            }
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            MnemonicTextBox first = null;

            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(_words[i].Trim(), _mnemonic[_indexes[i]], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Words.Children[i] is Grid content && content.Children[0] is MnemonicTextBox textBox)
                {
                    textBox.Reject();
                    first ??= textBox;
                }
            }

            if (first != null)
            {
                args.Cancel = true;
                first.Focus(FocusState.Keyboard);

                ToastPopup.Show(XamlRoot, Strings.WalletWrongSecretPhraseInfo, ToastPopupIcon.Error);
            }
        }
    }
}
