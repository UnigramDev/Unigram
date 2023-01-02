//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
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

            Update(_textBox.Document.Selection.CharacterFormat);
        }

        public void Update(ITextCharacterFormat format)
        {
            Bold.IsChecked = format.Bold == FormatEffect.On;
            Italic.IsChecked = format.Italic == FormatEffect.On;
            Strikethrough.IsChecked = format.Strikethrough == FormatEffect.On;
            Underline.IsChecked = format.Underline == UnderlineType.Single;
            Monospace.IsChecked = string.Equals(format.Name, "Consolas", StringComparison.OrdinalIgnoreCase);
            Spoiler.IsChecked = format.BackgroundColor == Colors.Gray;
        }
    }
}
