using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class BubbleTextBox : RichEditBox
    {
        private MenuFlyout _flyout;
        private MenuFlyoutPresenter _presenter;

        public BubbleTextBox()
        {
            _flyout = new MenuFlyout();
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Bold" });
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Italic" });
            _flyout.AllowFocusOnInteraction = false;
            _flyout.AllowFocusWhenDisabled = false;

            ((MenuFlyoutItem)_flyout.Items[0]).Click += Bold_Click;
            ((MenuFlyoutItem)_flyout.Items[1]).Click += Italic_Click;
            ((MenuFlyoutItem)_flyout.Items[1]).Loaded += Italic_Loaded;

            SelectionChanged += OnSelectionChanged;
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Off;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            if (string.IsNullOrEmpty(Document.Selection.Link) == false)
            {
                Document.Selection.Link = string.Empty;
                Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Off;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            if (string.IsNullOrEmpty(Document.Selection.Link) == false)
            {
                Document.Selection.Link = string.Empty;
                Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }
        }

        private void Italic_Loaded(object sender, RoutedEventArgs e)
        {
            _presenter = (MenuFlyoutPresenter)_flyout.Items[1].Ancestors<MenuFlyoutPresenter>().FirstOrDefault();
            OnSelectionChanged();
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            if (Document.Selection.Length != 0)
            {
                int hit;
                Rect rect;
                Document.Selection.GetRect(PointOptions.ClientCoordinates, out rect, out hit);
                _flyout.ShowAt(this, new Point(rect.X + 12, rect.Y - _presenter?.ActualHeight ?? 0));
            }
            else
            {
                _flyout.Hide();
            }
        }

        protected override void OnKeyUp(KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Space || e.Key == Windows.System.VirtualKey.Enter)
            {
                string text;
                Document.GetText(TextGetOptions.FormatRtf, out text);

                var caretPosition = Document.Selection.StartPosition;
                var result = Emoticon.Pattern.Replace(text, (match) =>
                {
                    var emoticon = match.Groups[1].Value;
                    var emoji = Emoticon.Replace(emoticon);
                    if (match.Index + match.Length < caretPosition)
                    {
                        caretPosition += emoji.Length - emoticon.Length;
                    }
                    if (match.Value.StartsWith(" "))
                    {
                        emoji = $" {emoji}";
                    }

                    return emoji;
                });

                Document.SetText(TextSetOptions.FormatRtf, result);
                Document.Selection.SetRange(caretPosition, caretPosition);
            }

            base.OnKeyUp(e);
        }

        public bool IsEmpty
        {
            get
            {
                // TODO: a better implementation?

                string text;
                Document.GetText(TextGetOptions.None, out text);

                return string.IsNullOrWhiteSpace(text);
            }
        }
    }
}
