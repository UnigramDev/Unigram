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

        #region BackVisibility

        public Visibility BackVisibility
        {
            get { return (Visibility)GetValue(BackVisibilityProperty); }
            set { SetValue(BackVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BackVisibilityProperty =
            DependencyProperty.Register("BackVisibility", typeof(Visibility), typeof(PageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

        #endregion

        #region IsLoading

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
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
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region Command

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region CommandParameter

        public object CommandParameter
        {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region CommandVisibility

        public Visibility CommandVisibility
        {
            get { return (Visibility)GetValue(CommandVisibilityProperty); }
            set { SetValue(CommandVisibilityProperty, value); }
        }

        public static readonly DependencyProperty CommandVisibilityProperty =
            DependencyProperty.Register("CommandVisibility", typeof(Visibility), typeof(ButtonPageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region CommandToolTip

        public string CommandToolTip
        {
            get { return (string)GetValue(CommandToolTipProperty); }
            set { SetValue(CommandToolTipProperty, value); }
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

        #region BackVisibility

        public Visibility BackVisibility
        {
            get { return (Visibility)GetValue(BackVisibilityProperty); }
            set { SetValue(BackVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BackVisibilityProperty =
            DependencyProperty.Register("BackVisibility", typeof(Visibility), typeof(ContentPageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region IsLoading

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(ContentPageHeader), new PropertyMetadata(false));

        #endregion
    }
}
