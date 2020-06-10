using System;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class AnimatedBitmapButton : SimpleButton
    {
        private BitmapIcon _label1;
        private BitmapIcon _label2;

        private Visual _visual1;
        private Visual _visual2;

        private BitmapIcon _label;
        private Visual _visual;

        public AnimatedBitmapButton()
        {
            DefaultStyleKey = typeof(AnimatedBitmapButton);
        }

        protected override void OnApplyTemplate()
        {
            _label1 = _label = GetTemplateChild("ContentPresenter1") as BitmapIcon;
            _label2 = GetTemplateChild("ContentPresenter2") as BitmapIcon;

            _visual1 = _visual = ElementCompositionPreview.GetElementVisual(_label1);
            _visual2 = ElementCompositionPreview.GetElementVisual(_label2);

            _label2.UriSource = null;

            _visual2.Opacity = 0;
            _visual2.Scale = new Vector3();
            _visual2.CenterPoint = new Vector3(18);

            _label1.UriSource = UriSource;

            _visual1.Opacity = 1;
            _visual1.Scale = new Vector3(1);
            _visual1.CenterPoint = new Vector3(18);

            base.OnApplyTemplate();
        }

        #region UriSource
        public Uri UriSource
        {
            get { return (Uri)GetValue(UriSourceProperty); }
            set { SetValue(UriSourceProperty, value); }
        }

        public static readonly DependencyProperty UriSourceProperty =
            DependencyProperty.Register("UriSource", typeof(Uri), typeof(AnimatedBitmapButton), new PropertyMetadata(null, OnUriSourceChanged));

        private static void OnUriSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedBitmapButton)d).OnUriSourceChanged((Uri)e.NewValue, (Uri)e.OldValue);
        }
        #endregion

        private void OnUriSourceChanged(Uri newValue, Uri oldValue)
        {
            if (oldValue == null || newValue == null)
            {
                return;
            }

            if (Equals(newValue, oldValue))
            {
                return;
            }

            if (_visual == null || _label == null)
            {
                return;
            }

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var labelShow = _visual == _visual1 ? _label2 : _label1;
            var labelHide = _visual == _visual1 ? _label1 : _label2;

            var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(1));
            hide1.InsertKeyFrame(1, new Vector3(0));

            var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            labelShow.UriSource = newValue;

            var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(1, new Vector3(1));
            show1.InsertKeyFrame(0, new Vector3(0));

            var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(1, 1);
            show2.InsertKeyFrame(0, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);

            _visual = visualShow;
            _label = labelShow;
        }
    }
}
