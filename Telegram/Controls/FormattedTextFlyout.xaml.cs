//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class FormattedTextFlyout : StackPanel
    {
        private readonly FormattedTextBox _textBox;
        private bool _registered;

        public FormattedTextBox TextBox => _textBox;

        public FormattedTextFlyout(FormattedTextBox textBox)
        {
            InitializeComponent();

            _textBox = textBox;
        }

        public void Register()
        {
            if (!_registered)
            {
                _registered = true;
                _textBox.SelectionChanged += OnSelectionChanged;
            }

            OnSelectionChanged(null, null);
        }

        public void Unregister()
        {
            if (_registered)
            {
                _registered = false;
                _textBox.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_textBox == null || _textBox.Document.Selection.Length == 0)
            {
                return;
            }

            Update(_textBox.Document.Selection);
        }

        public void Update(ITextRange selection)
        {
            var paragraph = selection.ParagraphFormat;
            var character = selection.CharacterFormat;

            Quote.IsChecked = paragraph.SpaceAfter != 0;
            Bold.IsChecked = character.Bold == FormatEffect.On;
            Italic.IsChecked = character.Italic == FormatEffect.On;
            Strikethrough.IsChecked = character.Strikethrough == FormatEffect.On;
            Underline.IsChecked = character.Underline == UnderlineType.Single;
            Monospace.IsChecked = string.Equals(character.Name, "Consolas", StringComparison.OrdinalIgnoreCase);
            Spoiler.IsChecked = character.BackgroundColor == Colors.Gray;
        }
    }
}
