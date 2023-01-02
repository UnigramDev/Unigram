//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class PageHeader : Control
    {
        public PageHeader()
        {
            DefaultStyleKey = typeof(PageHeader);
        }

        #region Text

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

        #endregion

        #region IsLoading

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(PageHeader), new PropertyMetadata(false));

        #endregion
    }

    public class ButtonPageHeader : PageHeader
    {
        public ButtonPageHeader()
        {
            DefaultStyleKey = typeof(ButtonPageHeader);
        }

        #region Glyph

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region Command

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region CommandParameter

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region CommandVisibility

        public Visibility CommandVisibility
        {
            get => (Visibility)GetValue(CommandVisibilityProperty);
            set => SetValue(CommandVisibilityProperty, value);
        }

        public static readonly DependencyProperty CommandVisibilityProperty =
            DependencyProperty.Register("CommandVisibility", typeof(Visibility), typeof(ButtonPageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region CommandToolTip

        public string CommandToolTip
        {
            get => (string)GetValue(CommandToolTipProperty);
            set => SetValue(CommandToolTipProperty, value);
        }

        public static readonly DependencyProperty CommandToolTipProperty =
            DependencyProperty.Register("CommandToolTip", typeof(string), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion
    }

    public class ContentPageHeader : ContentControl
    {
        public ContentPageHeader()
        {
            DefaultStyleKey = typeof(ContentPageHeader);
        }

        #region IsLoading

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(ContentPageHeader), new PropertyMetadata(false));

        #endregion
    }
}
