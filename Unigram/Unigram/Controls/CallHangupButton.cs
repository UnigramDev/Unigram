using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class CallHangupButton : AnimatedGlyphToggleButton
    {
        private Visual _visual;

        public CallHangupButton()
        {
            DefaultStyleKey = typeof(CallHangupButton);

            Checked += OnToggle;
            Unchecked += OnToggle;
        }

        protected override void OnApplyTemplate()
        {
            var presenter = GetTemplateChild("ContentPresenter") as TextBlock;
            if (presenter != null)
            {
                var hangup = IsChecked == true;

                _visual = ElementCompositionPreview.GetElementVisual(presenter);
                _visual.CenterPoint = new Vector3(10, 10, 0);
                _visual.RotationAngleInDegrees = hangup ? 135 : 0;
                _visual.Scale = hangup ? new Vector3(1.1f, 1.1f, 1) : Vector3.One;
            }

            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (_visual == null)
            {
                return;
            }

            var hangup = IsChecked == true;

            var rotation = _visual.Compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(0, hangup ? 0 : 135);
            rotation.InsertKeyFrame(1, hangup ? 135 : 0);

            var scale = _visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, hangup ? Vector3.One : new Vector3(1.1f, 1.1f, 1));
            scale.InsertKeyFrame(1, hangup ? new Vector3(1.1f, 1.1f, 1) : Vector3.One);

            _visual.StartAnimation("RotationAngleInDegrees", rotation);
            _visual.StartAnimation("Scale", scale);
        }
    }
}
