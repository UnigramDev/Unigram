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

            PrimaryButtonContent = Strings.WalletDone;

            InitializeWords();
        }

        /// <summary>
        /// The phrase as a step of disabling the backup, where it is about to become the only copy:
        /// it leads on to the test, and leaving it abandons the whole operation, so that is asked.
        /// </summary>
        public static WalletPhrasePopup ForDisableBackup(IReadOnlyList<string> words)
        {
            var popup = new WalletPhrasePopup(words);
            popup.PrimaryButtonContent = Strings.WalletContinue;
            popup.Closing += popup.OnDisableBackupClosing;

            return popup;
        }

        /// <summary>
        /// The same, for a phrase that replaces the current one as part of disabling the backup.
        /// </summary>
        public static WalletPhrasePopup ForNewPhrase(IReadOnlyList<string> words)
        {
            var popup = ForDisableBackup(words);
            popup.Heading.Text = Strings.WalletNewSecretPhrase;

            TextBlockHelper.SetMarkdown(popup.Info, Strings.WalletNewSecretPhraseInfo);

            return popup;
        }

        private async void OnDisableBackupClosing(ModalPopup sender, ModalPopupClosingEventArgs args)
        {
            if (args.Result == ContentDialogResult.Primary)
            {
                return;
            }

            var deferral = args.GetDeferral();

            var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, Strings.WalletCancelDisableBackupInfo, Strings.WalletCancelDisableBackupTitle, Strings.WalletCancelDisabling, Strings.WalletContinue);
            if (confirm != ContentDialogResult.Primary)
            {
                args.Cancel = true;
            }

            deferral.Complete();
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
                var number = new TextBlock
                {
                    Text = string.Format("{0}. ", i + 1),
                    Style = BootStrapper.Current.Resources["InfoBodyTextBlockStyle"] as Style,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 4, 0)
                };

                var word = new TextBlock
                {
                    Text = _words[i],
                    Style = BootStrapper.Current.Resources["BaseTextBlockStyle"] as Style
                };

                Grid.SetRow(number, i % rows);
                Grid.SetRow(word, i % rows);
                Grid.SetColumn(word, 1);

                if (i < rows)
                {
                    FirstColumn.RowDefinitions.Add(1, GridUnitType.Auto);
                    FirstColumn.Children.Add(number);
                    FirstColumn.Children.Add(word);
                }
                else
                {
                    SecondColumn.RowDefinitions.Add(1, GridUnitType.Auto);
                    SecondColumn.Children.Add(number);
                    SecondColumn.Children.Add(word);
                }
            }
        }
    }
}
