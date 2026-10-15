//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native.Highlight;
using Telegram.Td;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    /// <summary>A language the code can be highlighted as: what is sent, what it is called, and the other ids it goes by.</summary>
    public sealed partial class CodeLanguage
    {
        public CodeLanguage(string id, string name, List<string> ids)
        {
            Id = id;
            Name = name;
            Ids = ids;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>Every id the highlighter knows it by, <see cref="Id"/> among them.</summary>
        public List<string> Ids { get; }
    }

    /// <summary>
    /// The language of a code block: one of those the code can be highlighted as, or any other
    /// the sender writes, as a code block's language is the sender's own.
    /// </summary>
    public sealed partial class CodeLanguagePopup : ModalPopup
    {
        private static List<CodeLanguage> _all;
        private static string[] _searchable;

        private string _language;

        public CodeLanguagePopup(string language)
        {
            InitializeComponent();

            Title = Strings.ArticleHintLanguage;
            PrimaryButtonContent = Strings.OK;
            SecondaryButtonContent = Strings.Cancel;

            Input.Text = language ?? string.Empty;
        }

        /// <summary>Asks for a code block's language: null if cancelled, empty for none.</summary>
        public static async Task<string> ChooseAsync(XamlRoot xamlRoot, string language)
        {
            var popup = new CodeLanguagePopup(language);

            var confirm = await popup.ShowAsync(xamlRoot);
            return confirm == ContentDialogResult.Primary ? popup._language : null;
        }

        private static List<CodeLanguage> All
        {
            get
            {
                if (_all == null)
                {
                    var languages = SyntaxToken.Languages;
                    var all = new List<CodeLanguage>(languages.Count);

                    foreach (var language in languages)
                    {
                        var ids = new List<string>(language.Aliases.Count + 1) { language.Id };
                        ids.AddRange(language.Aliases);

                        all.Add(new CodeLanguage(language.Id, language.Name, ids));
                    }

                    all.Sort(CompareNames);

                    // Matched on a word of the name or of any id, in one string so one call searches all of them.
                    var searchable = new string[all.Count];

                    for (int i = 0; i < all.Count; i++)
                    {
                        searchable[i] = all[i].Name + " " + string.Join(" ", all[i].Ids);
                    }

                    _searchable = searchable;
                    _all = all;
                }

                return _all;
            }
        }

        private static int CompareNames(CodeLanguage x, CodeLanguage y)
        {
            return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Input.Focus(FocusState.Keyboard);
        }

        // Offered once something is typed, and only what matches it.
        private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            var query = sender.Text.Trim();
            if (query.Length == 0)
            {
                sender.ItemsSource = null;
                return;
            }

            var all = All;
            var positions = ClientEx.SearchByPrefix(_searchable, query);
            var results = new List<CodeLanguage>(positions.Count);

            foreach (var position in positions)
            {
                results.Add(all[position]);
            }

            sender.ItemsSource = results;
        }

        private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // A suggestion picked, by a click or by Enter on it, only fills the box: confirming
            // is the next Enter, or the button.
            if (args.ChosenSuggestion is CodeLanguage language)
            {
                sender.Text = language.Id;
                return;
            }

            if (Validate())
            {
                Hide(ContentDialogResult.Primary);
            }
        }

        // Escape closes the list first, as it would anywhere else, and the popup only after.
        private void OnClosing(ModalPopup sender, ModalPopupClosingEventArgs args)
        {
            if (args.Result == ContentDialogResult.None && Input.IsSuggestionListOpen)
            {
                Input.IsSuggestionListOpen = false;
                args.Cancel = true;
            }
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (!Validate())
            {
                args.Cancel = true;
            }
        }

        private bool Validate()
        {
            _language = Resolve(Input.Text);

            if (_language == null)
            {
                VisualUtilities.ShakeView(Input);
                return false;
            }

            return true;
        }

        /// <summary>
        /// What is sent for <paramref name="text"/>: a language typed by its name is sent as its
        /// id, anything else as it is. Null when it cannot be a language.
        /// </summary>
        private static string Resolve(string text)
        {
            text = text?.Trim() ?? string.Empty;

            if (text.Length == 0)
            {
                return string.Empty;
            }

            // An alias typed is as good as the language's own id, and is kept.
            foreach (var language in All)
            {
                if (string.Equals(language.Name, text, StringComparison.OrdinalIgnoreCase))
                {
                    return language.Id;
                }

                foreach (var id in language.Ids)
                {
                    if (string.Equals(id, text, StringComparison.OrdinalIgnoreCase))
                    {
                        return id;
                    }
                }
            }

            // As TDLib writes a language into markdown: printable ASCII, no spaces. A backquote
            // would end the code block it opens.
            foreach (var c in text)
            {
                if (c < 33 || c > 126 || c == '`')
                {
                    return null;
                }
            }

            return text;
        }
    }
}
