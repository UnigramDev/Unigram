using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Behaviors
{
    public class AspectRatioBehavior : DependencyObject, IBehavior
    {
        #region AspectRatio
        public Size AspectRatio
        {
            get { return (Size)GetValue(AspectRatioProperty); }
            set { SetValue(AspectRatioProperty, value); }
        }

        public static readonly DependencyProperty AspectRatioProperty =
            DependencyProperty.Register("AspectRatio", typeof(Size), typeof(AspectRatioBehavior), new PropertyMetadata(null));
        #endregion

        public DependencyObject AssociatedObject { get; private set; }

        public void Attach(DependencyObject associatedObject)
        {
            var control = associatedObject as FrameworkElement;
            if (control != null)
            {
                AssociatedObject = associatedObject;

                control.SizeChanged += OnSizeChanged;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var control = AssociatedObject as FrameworkElement;
            if (control != null)
            {
                control.Height = AspectRatio.Height * control.ActualWidth / AspectRatio.Width;
            }
        }

        public void Detach()
        {
            var control = AssociatedObject as FrameworkElement;
            if (control != null)
            {
                control.SizeChanged -= OnSizeChanged;
            }
        }
    }
}
