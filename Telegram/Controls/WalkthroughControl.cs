//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class WalkthroughControl : ContentPageHeader
    {
        public AnimatedImage Header { get; private set; }
        private Button Action;

        public WalkthroughControl()
        {
            DefaultStyleKey = typeof(WalkthroughControl);
        }

        public event RoutedEventHandler ButtonClick;

        protected override void OnApplyTemplate()
        {
            Header = GetTemplateChild("Header") as AnimatedImage;
            Action = GetTemplateChild(nameof(Action)) as Button;
            Action.Click += ButtonClick;

            base.OnApplyTemplate();
            VisualStateManager.GoToState(this, Content != null ? "ContentVisible" : "NoContent", false);
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
            VisualStateManager.GoToState(this, newContent != null ? "ContentVisible" : "NoContent", false);
        }

        #region HeaderSource

        public AnimatedImageSource HeaderSource
        {
            get => (AnimatedImageSource)GetValue(HeaderSourceProperty);
            set => SetValue(HeaderSourceProperty, value);
        }

        public static readonly DependencyProperty HeaderSourceProperty =
            DependencyProperty.Register("HeaderSource", typeof(AnimatedImageSource), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region Title

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region Text

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region ButtonText

        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register("ButtonText", typeof(string), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion

        #region ButtonVisibility

        public Visibility ButtonVisibility
        {
            get => (Visibility)GetValue(ButtonVisibilityProperty);
            set => SetValue(ButtonVisibilityProperty, value);
        }

        public static readonly DependencyProperty ButtonVisibilityProperty =
            DependencyProperty.Register("ButtonVisibility", typeof(Visibility), typeof(WalkthroughControl), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region Footer

        public UIElement Footer
        {
            get => (UIElement)GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(UIElement), typeof(WalkthroughControl), new PropertyMetadata(null));

        #endregion
    }
}
