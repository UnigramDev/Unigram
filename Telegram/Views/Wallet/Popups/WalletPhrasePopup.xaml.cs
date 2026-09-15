//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// One word of the phrase, with the place it takes in it.
    /// </summary>
    /// <remarks>
    /// The number is carried as it is written rather than as a count, so the template can draw it
    /// without a converter.
    /// </remarks>
    public partial class WalletPhraseWord
    {
        public WalletPhraseWord(string number, string word)
        {
            Number = number;
            Word = word;
        }

        public string Number { get; }

        public string Word { get; }
    }

    /// <summary>
    /// The recovery phrase, on screen.
    /// </summary>
    /// <remarks>
    /// Takes the words rather than the service: getting hold of them is a flow with prompts and
    /// fallbacks in it, and by the time this opens that flow is over.
    ///
    /// The words live only as long as the popup, and nothing here writes them anywhere but the
    /// clipboard, and then only when asked.
    /// </remarks>
    public sealed partial class WalletPhrasePopup : ModalPopup
    {
        private readonly IReadOnlyList<string> _words;

        public WalletPhrasePopup(IReadOnlyList<string> words)
        {
            InitializeComponent();

            _words = words;

            Title = "[Recovery Phrase]";
            PrimaryButtonContent = "[Copy]";
            CloseButtonContent = "[I've Written It Down]";

            PrimaryButtonClick += OnPrimaryButtonClick;

            InitializeWords();
        }

        private void InitializeWords()
        {
            // Half down the first column and half down the second, so 1 sits beside 13 in a
            // 24-word phrase and beside 7 in a 12-word one.
            var rows = (_words.Count + 1) / 2;

            var first = new List<WalletPhraseWord>(rows);
            var second = new List<WalletPhraseWord>(_words.Count - rows);

            for (int i = 0; i < _words.Count; i++)
            {
                var word = new WalletPhraseWord(string.Format("{0}. ", i + 1), _words[i]);

                if (i < rows)
                {
                    first.Add(word);
                }
                else
                {
                    second.Add(word);
                }
            }

            FirstColumn.ItemsSource = first;
            SecondColumn.ItemsSource = second;
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            // The popup stays open: copying is not the end of this, writing them down is.
            args.Cancel = true;

            MessageHelper.CopyText(XamlRoot, string.Join(" ", _words));
        }
    }
}
