using System;
using System.Windows.Input;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class WalkthroughControl : ContentPageHeader
    {
        public LottieView Header { get; private set; }

        public WalkthroughControl()
        {
            DefaultStyleKey = typeof(WalkthroughControl);
        }

        protected override void OnApplyTemplate()
        {
            Header = GetTemplateChild("Header") as LottieView;

            base.OnApplyTemplate();
            VisualStateManager.GoToState(this, Content != null ? "ContentVisible" : "NoContent", false);
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
            VisualStateManager.GoToState(this, newContent != null ? "ContentVisible" : "NoContent", false);
        }

        #region HeaderSource

        public Uri HeaderSource
        {
            get { return (Uri)GetValue(HeaderSourceProperty); }
            set { SetValue(HeaderSourceProperty, value); }
        }

        public static readonly DependencyProperty HeaderSourceProperty =
            DependencyProperty.Register("HeaderSource", typeof(Uri), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region Title

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region ButtonText

        public string ButtonText
        {
            get { return (string)GetValue(ButtonTextProperty); }
            set { SetValue(ButtonTextProperty, value); }
        }

        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register("ButtonText", typeof(string), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region ButtonCommand

        public ICommand ButtonCommand
        {
            get { return (ICommand)GetValue(ButtonCommandProperty); }
            set { SetValue(ButtonCommandProperty, value); }
        }

        public static readonly DependencyProperty ButtonCommandProperty =
            DependencyProperty.Register("ButtonCommand", typeof(ICommand), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region ButtonVisibility

        public Visibility ButtonVisibility
        {
            get { return (Visibility)GetValue(ButtonVisibilityProperty); }
            set { SetValue(ButtonVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ButtonVisibilityProperty =
            DependencyProperty.Register("ButtonVisibility", typeof(Visibility), typeof(WalkthroughControl), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region Footer

        public UIElement Footer
        {
            get { return (UIElement)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(UIElement), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion
    }
}
