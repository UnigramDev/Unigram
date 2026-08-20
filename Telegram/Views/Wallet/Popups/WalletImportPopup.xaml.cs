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
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    public sealed partial class WalletImportPopup : ContentPopup
    {
        private readonly IWalletService _wallet;
        private readonly INavigationService _navigationService;

        private IReadOnlyList<string> _mnemonicWordList;
        private string[] _words;

        public WalletImportPopup(IWalletService wallet, INavigationService navigationService)
        {
            _wallet = wallet;
            _navigationService = navigationService;

            InitializeComponent();
            InitializeKit();
            InitializeWords(12);

            Navigation.SelectionChanged += Navigation_SelectionChanged;

            PrimaryButtonText = Strings.Import;
            SecondaryButtonText = Strings.Cancel;
        }

        private void InitializeKit()
        {
            _mnemonicWordList ??= WalletService.RecoveryWords;
        }

        private void InitializeWords(int count)
        {
            var backup = _words;

            _words = new string[count];
            Words.Children.Clear();

            for (int i = 0; i < count; i++)
            {
                var local = i;
                var textBox = new MnemonicTextBox
                {
                    Index = i,
                    Padding = new Thickness(32, 5, 6, 6),
                };

                textBox.TextChanged += (s, args) =>
                {
                    _words[local] = textBox.Text;
                    //textBox.ItemsSource = _mnemonicWordList.Where(x => x.StartsWith(textBox.Text, StringComparison.OrdinalIgnoreCase)).ToList();
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
                        Style = BootStrapper.Current.Resources["SmallPillButtonStyle"] as Style,
                        Padding = new Thickness(8, 5, 8, 7),
                        Margin = new Thickness(0, 0, 6, 0),
                        Height = 20,
                        CornerRadius = new CornerRadius(10)
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
                textBox.HasError = !_mnemonicWordList.Contains(textBox.Text);
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
                // A pasted phrase can arrive with any whitespace between the words, and with
                // trailing newlines from whatever it was copied out of.
                var pasted = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                if (pasted.Length == 24 && atIndex == 0)
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

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = _submitted;

            if (_submitted)
            {
                return;
            }

            _submitted = true;
            IsPrimaryButtonPending = true;

            var deferral = args.GetDeferral();

            try
            {
                await _wallet.ImportAsync(_words);

                _submitted = false;
                deferral.Complete();

                _navigationService.Navigate(typeof(WalletPage));
                _navigationService.ShowToast("[**Wallet Imported**\nYour wallet was restored from your recovery phrase.]", ToastPopupIcon.Success);
            }
            catch (Exception ex)
            {
                _navigationService.ShowPopup(ex.ToString(), "Error", "OK");
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeWords(Navigation.SelectedIndex == 0 ? 12 : 24);
        }
    }

    public class MnemonicTextBox : TextBox
    {
        public MnemonicTextBox()
        {
            DefaultStyleKey = typeof(MnemonicTextBox);
        }

        #region Index

        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register(nameof(Index), typeof(int), typeof(MnemonicTextBox), new PropertyMetadata(0));

        #endregion

        #region HasError

        public bool HasError
        {
            get { return (bool)GetValue(HasErrorProperty); }
            set { SetValue(HasErrorProperty, value); }
        }

        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.Register(nameof(HasError), typeof(bool), typeof(MnemonicTextBox), new PropertyMetadata(false, OnHasErrorChanged));

        private static void OnHasErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            VisualStateManager.GoToState(d as Control, (bool)e.NewValue ? "Invalid" : "Normal", false);
        }

        #endregion

        protected override bool GoToElementStateCore(string stateName, bool useTransitions)
        {
            return base.GoToElementStateCore(stateName, useTransitions);
        }
    }

    public class MnemonicTextBoxVisualStateManager : VisualStateManager
    {
        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if (group.States.Count > 2 && stateName != "Focused" && control is MnemonicTextBox { HasError: true })
            {
                return base.GoToStateCore(control, templateRoot, "Invalid", group, state, false);
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }
}
