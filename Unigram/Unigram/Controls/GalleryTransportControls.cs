using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [ContentProperty(Name = "Content")]
    public class GalleryTransportControls : MediaTransportControls
    {
        public GalleryTransportControls()
        {
            DefaultStyleKey = typeof(GalleryTransportControls);
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
