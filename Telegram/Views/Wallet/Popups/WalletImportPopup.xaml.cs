//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services.Wallet;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    public sealed partial class WalletImportPopup : ModalPopup
    {
        private readonly IWalletService _wallet;
        private readonly INavigationService _navigationService;

        // The opener's, and disposed by it: importing is one step of whatever it is doing.
        private readonly WalletVault.WalletVaultLease _lease;

        private string[] _words;

        // What the words are for. Binding adopts the account's own wallet onto this device and
        // refuses a phrase that derives anywhere else; replacing is the opposite - a different
        // wallet is the whole point - and it may need the account password, asked once the words
        // are in.
        private readonly bool _isReplacing;

        public WalletImportPopup(IWalletService wallet, INavigationService navigationService, WalletVault.WalletVaultLease lease)
            : this(wallet, navigationService, lease, false)
        {
        }

        /// <summary>
        /// The same screen, used to swap the account's wallet for another one the user already has.
        /// </summary>
        public static WalletImportPopup ForReplacement(IWalletService wallet, INavigationService navigationService, WalletVault.WalletVaultLease lease)
        {
            return new WalletImportPopup(wallet, navigationService, lease, true);
        }

        private WalletImportPopup(IWalletService wallet, INavigationService navigationService, WalletVault.WalletVaultLease lease, bool isReplacing)
        {
            _wallet = wallet;
            _navigationService = navigationService;
            _lease = lease;
            _isReplacing = isReplacing;

            InitializeComponent();
            InitializeWords(12);

            Words12.Content = Locale.Declension(Strings.R.WalletPhraseWords, 12);
            Words24.Content = Locale.Declension(Strings.R.WalletPhraseWords, 24);

            Navigation.SelectionChanged += Navigation_SelectionChanged;

            PrimaryButtonText = Strings.Import;
            SecondaryButtonText = Strings.Cancel;
        }

        private void InitializeWords(int count)
        {
            TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.WalletImportPhraseInfo, count));

            var backup = _words;

            _words = new string[count];
            Words.Children.Clear();

            for (int i = 0; i < count; i++)
            {
                var textBox = new MnemonicTextBox
                {
                    Index = i,
                    Padding = new Thickness(32, 5, 6, 6),
                };

                textBox.TextChanged += TextBox_TextChanged;
                textBox.Paste += TextBox_Paste;

                if (backup?.Length > i)
                {
                    _words[i] = backup[i];
                    textBox.Text = backup[i];
                }
                else
                {
                    _words[i] = string.Empty;
                }

                var content = new Grid();
                var position = new TextBlock
                {
                    Text = $"{i + 1}.",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 5, 6, 6)
                };

                content.Children.Add(textBox);
                content.Children.Add(position);

                if (i == 0)
                {
                    var paste = new Button
                    {
                        Content = Strings.Paste,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Style = BootStrapper.Current.Resources["BadgeControlButtonStyle"] as Style,
                        Margin = new Thickness(0, 0, 6, 0)
                    };

                    paste.Click += Paste_Click;

                    content.Children.Add(paste);
                }

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

        private void TextBox_Paste(object sender, TextControlPasteEventArgs e)
        {
            if (sender is MnemonicTextBox textBox)
            {
                OnPaste(textBox.Index);
            }

            e.Handled = true;
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            OnPaste(0);
        }

        private async void OnPaste(int atIndex)
        {
            var clipboard = Clipboard.GetContent();
            if (clipboard.Contains(StandardDataFormats.Text))
            {
                var text = await clipboard.GetTextAsync();
                var pasted = SplitWords(text);

                if (pasted.Count == 24 && atIndex == 0)
                {
                    Navigation.SelectedIndex = 1;
                }

                var nextWords = ApplyMnemonicPaste(_words, atIndex, pasted);

                for (int i = 0; i < Math.Min(nextWords.Length, Words.Children.Count); i++)
                {
                    _words[i] = nextWords[i];

                    if (Words.Children[i] is Grid content && content.Children[0] is TextBox textBox)
                    {
                        textBox.Text = nextWords[i];
                    }
                }
            }
        }

        // Mnemonic words are letters only, so anything else separates them. That covers a phrase
        // copied with any whitespace, commas, or the numbering other apps put in front of each
        // word ("1. kitten", "1) kitten", "1.kitten").
        private static List<string> SplitWords(string text)
        {
            var words = new List<string>(24);
            var start = -1;

            for (int i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && char.IsLetter(text[i]))
                {
                    if (start < 0)
                    {
                        start = i;
                    }
                }
                else if (start >= 0)
                {
                    words.Add(text.Substring(start, i - start).ToLowerInvariant());
                    start = -1;
                }
            }

            return words;
        }

        private string[] ApplyMnemonicPaste(IReadOnlyList<string> currentWords, int atIndex, IReadOnlyList<string> pastedWords)
        {
            var total = currentWords.Count;
            var isFullOverwrite = pastedWords.Count >= 12 && atIndex == 0;
            var start = isFullOverwrite ? 0 : atIndex;

            var next = new string[total];
            for (int i = 0; i < total; i++)
            {
                next[i] = isFullOverwrite ? string.Empty : currentWords[i];
            }

            var written = Math.Clamp(pastedWords.Count, 0, Math.Max(total - start, 0));
            for (int i = 0; i < written; i++)
            {
                next[start + i] = pastedWords[i];
            }

            return next;
        }

        private bool _submitted;
        private bool _completed;

        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            args.Cancel = _submitted;

            if (_submitted)
            {
                return;
            }

            _submitted = true;
            IsPrimaryButtonPending = true;

            var deferral = args.GetDeferral();

            if (_isReplacing)
            {
                await ReplaceAsync(args, deferral);
                return;
            }

            WalletBindResult result;

            try
            {
                result = await _wallet.BindAsync(_words, _lease);
            }
            catch (WalletAccessDeniedException)
            {
                // Declined the vault prompt. The words stay, so confirming again asks again.
                _submitted = false;
                IsPrimaryButtonPending = false;

                args.Cancel = true;
                deferral.Complete();
                return;
            }

            _submitted = false;
            IsPrimaryButtonPending = false;

            if (result.Failure is WalletBindFailure failure)
            {
                // The popup stays open: the words are still on screen, and two of the three are
                // something the user can do something about.
                args.Cancel = true;
                deferral.Complete();

                _ = MessagePopup.ShowNestedAsync(XamlRoot, Explain(failure), Strings.WalletImport, Strings.OK);
                return;
            }

            deferral.Complete();

            _navigationService.NavigateToWallet();
            _navigationService.ShowToast("[**Wallet Imported**\nYour wallet was restored from your recovery phrase.]", ToastPopupIcon.Success);
        }

        /// <summary>
        /// Swaps the account's wallet for the one these words derive.
        /// </summary>
        /// <remarks>
        /// Unlike binding, the phrase is meant to belong to another wallet, so there is nothing to
        /// check it against here - the account decides, and it decides on a signature the imported
        /// key produces rather than on the words.
        /// </remarks>
        private async Task ReplaceAsync(ModalPopupButtonClickEventArgs args, Deferral deferral)
        {
            try
            {
                if (!await WalletHelper.RunWithPasswordAsync(XamlRoot, ReplaceWithPasswordAsync))
                {
                    _submitted = false;
                    IsPrimaryButtonPending = false;

                    args.Cancel = true;
                    deferral.Complete();
                    return;
                }
            }
            catch (WalletAccessDeniedException)
            {
                _submitted = false;
                IsPrimaryButtonPending = false;

                args.Cancel = true;
                deferral.Complete();
                return;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet could not be replaced: " + ex.Message);

                _submitted = false;
                IsPrimaryButtonPending = false;

                // The words stay on screen: a phrase the account refused is one the user may have
                // mistyped, and retyping it is the only thing they can do about it.
                args.Cancel = true;
                deferral.Complete();

                _ = MessagePopup.ShowNestedAsync(XamlRoot, "[That wallet could not be used. Check the words and their order.]", "[Import Wallet]", Strings.OK);
                return;
            }

            _submitted = false;
            IsPrimaryButtonPending = false;

            deferral.Complete();

            _navigationService.NavigateToWallet();
            _navigationService.ShowToast("[**Wallet Replaced**\nYour account now uses the wallet you imported.]", ToastPopupIcon.Success);
        }

        private Task ReplaceWithPasswordAsync(string password)
        {
            return _wallet.ReplaceWalletAsync(password, _words, _lease);
        }

        /// <summary>
        /// What to say about a phrase that did not bind. The service reports which of the three it
        /// was; the words are the view's.
        /// </summary>
        private static string Explain(WalletBindFailure failure)
        {
            return failure switch
            {
                WalletBindFailure.OtherWallet => "[That recovery phrase belongs to a different wallet.]",
                WalletBindFailure.NoWallet => "[This account doesn't have a wallet yet.]",
                _ => "[That's not a valid recovery phrase. Check the words and their order.]"
            };
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeWords(Navigation.SelectedIndex == 0 ? 12 : 24);
        }
    }
}
