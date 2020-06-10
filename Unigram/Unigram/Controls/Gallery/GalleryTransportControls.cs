using System.Linq;
using Windows.Foundation;
using Windows.UI.ViewManagement;
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

        private Button FullWindowButton;

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

            FullWindowButton = GetTemplateChild("FullButton") as Button;
            FullWindowButton.Click += FullWindow_Click;
        }

        private void FullWindow_Click(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
            {
                view.ExitFullScreenMode();
                VisualStateManager.GoToState(this, "NonFullWindowState2", false);
            }
            else
            {
                if (view.TryEnterFullScreenMode())
                {
                    VisualStateManager.GoToState(this, "FullWindowState2", false);
                }
            }
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

        #region DownloadValue

        public double DownloadValue
        {
            get { return (double)GetValue(DownloadValueProperty); }
            set { SetValue(DownloadValueProperty, value); }
        }

        public static readonly DependencyProperty DownloadValueProperty =
            DependencyProperty.Register("DownloadValue", typeof(double), typeof(GalleryTransportControls), new PropertyMetadata(0d));

        #endregion

        #region DownloadMaximum

        public double DownloadMaximum
        {
            get { return (double)GetValue(DownloadMaximumProperty); }
            set { SetValue(DownloadMaximumProperty, value); }
        }

        public static readonly DependencyProperty DownloadMaximumProperty =
            DependencyProperty.Register("DownloadMaximum", typeof(double), typeof(GalleryTransportControls), new PropertyMetadata(0d));

        #endregion

        #region Volume

        public double Volume
        {
            get { return (double)GetValue(VolumeProperty); }
            set { SetValue(VolumeProperty, value); }
        }

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register("Volume", typeof(double), typeof(GalleryTransportControls), new PropertyMetadata(1d));

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

    public class GalleryTransportSlider : Slider
    {
        #region DownloadValue

        public double DownloadValue
        {
            get { return (double)GetValue(DownloadValueProperty); }
            set { SetValue(DownloadValueProperty, value); }
        }

        public static readonly DependencyProperty DownloadValueProperty =
            DependencyProperty.Register("DownloadValue", typeof(double), typeof(GalleryTransportSlider), new PropertyMetadata(0d));

        #endregion

        #region DownloadMaximum

        public double DownloadMaximum
        {
            get { return (double)GetValue(DownloadMaximumProperty); }
            set { SetValue(DownloadMaximumProperty, value); }
        }

        public static readonly DependencyProperty DownloadMaximumProperty =
            DependencyProperty.Register("DownloadMaximum", typeof(double), typeof(GalleryTransportSlider), new PropertyMetadata(0d));

        #endregion
    }
}
