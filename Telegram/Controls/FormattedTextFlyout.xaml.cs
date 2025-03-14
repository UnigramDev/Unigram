//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Telegram.Controls
{
    public sealed partial class FormattedTextFlyout : StackPanel
    {
        private readonly FormattedTextBox _textBox;

        public FormattedTextBox TextBox => _textBox;

        public FormattedTextFlyout(FormattedTextBox textBox)
        {
            InitializeComponent();

            _textBox = textBox;
            _textBox.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_textBox == null)
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
