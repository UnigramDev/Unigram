//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Services.Wallet;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// One word of a recovery phrase: completes it from the word list as it is typed, and marks it
    /// when it cannot be one.
    /// </summary>
    /// <remarks>
    /// The completion is drawn behind the text rather than offered in a list: the words are short
    /// and a few letters make them unique, so the rest of the word after the caret is all there is
    /// to offer.
    /// </remarks>
    public partial class MnemonicTextBox : TextBox
    {
        private Run _typed;
        private Run _completion;

        private string _suggestion;
        private bool _rejected;

        public MnemonicTextBox()
        {
            DefaultStyleKey = typeof(MnemonicTextBox);

            TextChanged += OnTextChanged;
            SelectionChanged += OnSelectionChanged;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("SuggestionPresenter") is TextBlock presenter)
            {
                // The typed part is laid out invisibly so the completion starts exactly where the
                // real text ends.
                _typed = new Run { Foreground = new SolidColorBrush(Windows.UI.Colors.Transparent) };
                _completion = new Run();

                presenter.Inlines.Add(_typed);
                presenter.Inlines.Add(_completion);
            }

            UpdateSuggestion();
            UpdateValidationState(false);
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
            private set { SetValue(HasErrorProperty, value); }
        }

        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.Register(nameof(HasError), typeof(bool), typeof(MnemonicTextBox), new PropertyMetadata(false, OnHasErrorChanged));

        private static void OnHasErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MnemonicTextBox)d).UpdateValidationState(true);
        }

        private void UpdateValidationState(bool useTransitions)
        {
            // A group of its own: TextBox drives CommonStates internally, and nothing written there
            // would survive its next pointer or focus change.
            VisualStateManager.GoToState(this, HasError ? "Invalid" : "Valid", useTransitions);
        }

        #endregion

        /// <summary>
        /// Marks a word that is in the list but is not the one asked for, until it is edited.
        /// </summary>
        public void Reject()
        {
            _rejected = true;
            HasError = true;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _rejected = false;

            UpdateSuggestion();
            Validate();
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateSuggestion();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            UpdateSuggestion();
            Validate();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            UpdateSuggestion();
            Validate();
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Right when _suggestion != null:
                    Accept();
                    e.Handled = true;
                    return;
                case VirtualKey.Tab when _suggestion != null:
                    // Left unhandled, so focus still moves on with the word filled in.
                    Accept();
                    break;
                case VirtualKey.Enter:
                case VirtualKey.Space:
                    // A word never contains a space, so it can only mean the word is finished.
                    if (_suggestion != null || (Text.Length > 0 && IsWord(Text)))
                    {
                        Accept();
                        FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == VirtualKey.Space)
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
            }

            base.OnKeyDown(e);
        }

        private void Accept()
        {
            if (_suggestion != null)
            {
                Text = _suggestion;
                SelectionStart = _suggestion.Length;
            }
        }

        private void UpdateSuggestion()
        {
            var text = Text;
            string suggestion = null;

            // Only while typing at the end: a completion after a caret in the middle of the word
            // would be drawn over the letters that follow it.
            if (FocusState != FocusState.Unfocused && text.Length > 0 && SelectionStart == text.Length && SelectionLength == 0)
            {
                var match = Complete(text);
                if (match != null && match.Length > text.Length)
                {
                    suggestion = match;
                }
            }

            if (_suggestion == suggestion)
            {
                return;
            }

            _suggestion = suggestion;

            if (_typed != null)
            {
                _typed.Text = suggestion != null ? text : string.Empty;
                _completion.Text = suggestion != null ? suggestion.Substring(text.Length) : string.Empty;
            }
        }

        private void Validate()
        {
            var text = Text;

            // While it is being typed, a prefix of some word is still on its way to being one.
            HasError = _rejected || text.Length > 0 && (FocusState != FocusState.Unfocused
                ? Complete(text) == null
                : !IsWord(text));
        }

        private static bool IsWord(string text)
        {
            var words = WalletService.RecoveryWords;
            var index = LowerBound(words, text);

            return index < words.Count && string.Equals(words[index], text, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The first word starting with <paramref name="prefix"/>, or null.
        /// </summary>
        private static string Complete(string prefix)
        {
            var words = WalletService.RecoveryWords;
            var index = LowerBound(words, prefix);

            return index < words.Count && words[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? words[index]
                : null;
        }

        // The BIP-39 list is sorted, which is what lets a keystroke cost eleven comparisons rather
        // than a scan of 2048 words.
        private static int LowerBound(IReadOnlyList<string> words, string value)
        {
            int lo = 0, hi = words.Count;

            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;

                if (string.Compare(words[mid], value, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }
    }
}
