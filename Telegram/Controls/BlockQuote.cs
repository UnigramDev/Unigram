//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Media;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // TODO: register FormattedTextBlock.IsTextTrimmableChanged
    public partial class BlockQuote : ContentControl
    {
        private HyperlinkButton Header;
        private HyperlinkButton Expand;

        public BlockQuote()
        {
            DefaultStyleKey = typeof(BlockQuote);
        }

        protected override void OnApplyTemplate()
        {
            if (!string.IsNullOrEmpty(LanguageName))
            {
                Header = GetTemplateChild(nameof(Header)) as HyperlinkButton;
                Header.Click += Header_Click;
                Header?.Visibility = Visibility.Visible;
            }

            if (ComputedIsExpandable)
            {
                Expand = GetTemplateChild(nameof(Expand)) as HyperlinkButton;
                Expand.Click += Expand_Click;
                Expand?.Visibility = Visibility.Visible;
            }

            base.OnApplyTemplate();
        }

        private void Header_Click(object sender, RoutedEventArgs e)
        {
            if (Content is FormattedTextBlock block)
            {
                MessageHelper.CopyText(XamlRoot, block.GetSelectedText(0, block.ContentLength));
            }
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            if (Content is FormattedTextBlock block)
            {
                var expanded = block.MaxLines == 0;
                if (expanded)
                {
                    block.MaxLines = 3;
                    Expand.HorizontalAlignment = HorizontalAlignment.Stretch;
                    Expand.Content = Icons.ChevronDown16;
                }
                else
                {
                    block.MaxLines = 0;
                    Expand.HorizontalAlignment = HorizontalAlignment.Right;
                    Expand.Content = Icons.ChevronUp16;
                }

                OnExpandableChanged(this, null);
            }
        }

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(BlockQuote), new PropertyMetadata(null));

        #endregion

        #region LanguageName

        public string LanguageName
        {
            get { return (string)GetValue(LanguageNameProperty); }
            set { SetValue(LanguageNameProperty, value); }
        }

        public static readonly DependencyProperty LanguageNameProperty =
            DependencyProperty.Register("LanguageName", typeof(string), typeof(BlockQuote), new PropertyMetadata(string.Empty, OnLanguageNameChanged));

        private static void OnLanguageNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as BlockQuote;
            if (sender?.Header != null || !string.IsNullOrEmpty((string)e.NewValue))
            {
                if (sender.Header == null)
                {
                    sender.Header = sender.GetTemplateChild(nameof(sender.Header)) as HyperlinkButton;
                    sender.Header?.Click += sender.Header_Click;
                }

                sender.Header?.Visibility = !string.IsNullOrEmpty((string)e.NewValue)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region IsExpandable

        public bool ComputedIsExpandable => IsExpandable && Content is FormattedTextBlock { IsTextTrimmable: true };

        public bool IsExpandable
        {
            get { return (bool)GetValue(IsExpandableProperty); }
            set { SetValue(IsExpandableProperty, value); }
        }

        public static readonly DependencyProperty IsExpandableProperty =
            DependencyProperty.Register("IsExpandable", typeof(bool), typeof(BlockQuote), new PropertyMetadata(false, OnExpandableChanged));

        private static void OnExpandableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as BlockQuote;
            if (sender?.Expand != null || sender.ComputedIsExpandable)
            {
                if (sender.Expand == null)
                {
                    sender.Expand = sender.GetTemplateChild(nameof(sender.Expand)) as HyperlinkButton;
                    sender.Expand?.Click += sender.Expand_Click;
                }

                sender.Expand?.Visibility = sender.ComputedIsExpandable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion
    }
}
