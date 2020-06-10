using System;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Chats
{
    public sealed partial class ChatTextFormatting : UserControl
    {
        public ChatTextFormatting()
        {
            InitializeComponent();
        }

        private FormattedTextBox _textBox;
        public FormattedTextBox TextBox
        {
            get => _textBox;
            set
            {
                if (_textBox != null)
                {
                    _textBox.SelectionChanged -= OnSelectionChanged;
                }

                _textBox = value;

                if (_textBox != null)
                {
                    Update(_textBox.Document.Selection.CharacterFormat);
                    _textBox.SelectionChanged += OnSelectionChanged;
                }
            }
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
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            _textBox?.ToggleBold();
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            _textBox?.ToggleItalic();
        }

        private void Strikethrough_Click(object sender, RoutedEventArgs e)
        {
            _textBox?.ToggleStrikethrough();
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            _textBox?.ToggleUnderline();
        }

        private void Monospace_Click(object sender, RoutedEventArgs e)
        {
            _textBox?.ToggleMonospace();
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            _textBox?.CreateLink();
        }
    }
}
