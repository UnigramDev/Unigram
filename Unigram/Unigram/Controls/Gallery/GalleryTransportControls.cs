using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Gallery
{
    [ContentProperty(Name = "Content")]
    public class GalleryTransportControls : MediaTransportControls
    {
        private Button PreviousButtonHorizontal;
        private Button NextButtonHorizontal;

        public GalleryTransportControls()
        {
            DefaultStyleKey = typeof(GalleryTransportControls);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            PreviousButtonHorizontal = GetTemplateChild("PreviousButtonHorizontal") as Button;
            NextButtonHorizontal = GetTemplateChild("NextButtonHorizontal") as Button;

            PreviousButtonHorizontal.Click += Switch_Click;
            NextButtonHorizontal.Click += Switch_Click;
        }

        public event TypedEventHandler<GalleryTransportControls, int> Switch;

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            Switch?.Invoke(this, sender == PreviousButtonHorizontal ? 0 : 2);
        }

        #region Header

        public object Header
        {
            get { return (object)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(GalleryTransportControls), new PropertyMetadata(null));

        #endregion

        #region Footer

        public object Footer
        {
            get { return (object)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(object), typeof(GalleryTransportControls), new PropertyMetadata(null));

        #endregion

        #region Content

        public object Content
        {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(GalleryTransportControls), new PropertyMetadata(null));

        #endregion

        #region TransportVisibility

        public Visibility TransportVisibility
        {
            get { return (Visibility)GetValue(TransportVisibilityProperty); }
            set { SetValue(TransportVisibilityProperty, value); }
        }

        public static readonly DependencyProperty TransportVisibilityProperty =
            DependencyProperty.Register("TransportVisibility", typeof(Visibility), typeof(GalleryTransportControls), new PropertyMetadata(Visibility.Collapsed));

        #endregion

        #region NextVisibility

        public Visibility NextVisibility
        {
            get { return (Visibility)GetValue(NextVisibilityProperty); }
            set { SetValue(NextVisibilityProperty, value); }
        }

        public static readonly DependencyProperty NextVisibilityProperty =
            DependencyProperty.Register("NextVisibility", typeof(Visibility), typeof(GalleryTransportControls), new PropertyMetadata(Visibility.Collapsed));

        #endregion

        #region PreviousVisibility

        public Visibility PreviousVisibility
        {
            get { return (Visibility)GetValue(PreviousVisibilityProperty); }
            set { SetValue(PreviousVisibilityProperty, value); }
        }

        public static readonly DependencyProperty PreviousVisibilityProperty =
            DependencyProperty.Register("PreviousVisibility", typeof(Visibility), typeof(GalleryTransportControls), new PropertyMetadata(Visibility.Collapsed));

        #endregion

        public bool IsVisible
        {
            get
            {
                var child = VisualTreeHelper.GetChild(this, 0) as FrameworkElement;
                if (child == null)
                {
                    return true;
                }

                var states = VisualStateManager.GetVisualStateGroups(child);
                if (states == null)
                {
                    return true;
                }

                var control = states.FirstOrDefault(x => x.Name == "ControlPanelVisibilityStates");
                if (control == null)
                {
                    return true;
                }

                return control.CurrentState?.Name == "ControlPanelFadeIn";
            }
        }

        public new void Show()
        {
            //if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            //{
            //    base.Show();
            //}
            //else
            {
                VisualStateManager.GoToState(this, "ControlPanelFadeIn", false);
            }
        }

        public new void Hide()
        {
            //if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            //{
            //    base.Hide();
            //}
            //else
            {
                VisualStateManager.GoToState(this, "ControlPanelFadeOut", false);
            }
        }
    }
}
