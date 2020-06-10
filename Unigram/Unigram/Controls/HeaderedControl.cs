using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class HeaderedControl : ItemsControl
    {
        public HeaderedControl()
        {
            DefaultStyleKey = typeof(HeaderedControl);
        }

        #region Header

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion

        #region Footer

        public string Footer
        {
            get { return (string)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(string), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion

        #region Radius

        public CornerRadius Radius
        {
            get { return (CornerRadius)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(CornerRadius), typeof(HeaderedControl), new PropertyMetadata(default));

        #endregion

        //protected override Size ArrangeOverride(Size finalSize)
        //{
        //    var size = base.ArrangeOverride(finalSize);
        //    if (size.Width > 640)
        //    {
        //        VisualStateManager.GoToState(this, "WideState", false);
        //    }
        //    else
        //    {
        //        VisualStateManager.GoToState(this, "NarrowState", false);
        //    }

        //    return size;
        //}
    }
}
