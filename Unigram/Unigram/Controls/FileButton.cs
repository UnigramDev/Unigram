using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class FileButton : GlyphHyperlinkButton
    {
        private TextBlock _label1;
        private TextBlock _label2;

        private Visual _visual1;
        private Visual _visual2;

        private TextBlock _label;
        private Visual _visual;

        public FileButton()
        {
            DefaultStyleKey = typeof(FileButton);
        }

        protected override void OnApplyTemplate()
        {
            _label1 = _label = GetTemplateChild("ContentPresenter1") as TextBlock;
            _label2 = GetTemplateChild("ContentPresenter2") as TextBlock;

            _visual1 = _visual = ElementCompositionPreview.GetElementVisual(_label1);
            _visual2 = ElementCompositionPreview.GetElementVisual(_label2);

            _label2.Text = string.Empty;

            _visual2.Opacity = 0;
            _visual2.Scale = new Vector3();
            _visual2.CenterPoint = new Vector3(10);

            _label1.Text = Glyph ?? string.Empty;

            _visual1.Opacity = 1;
            _visual1.Scale = new Vector3(1);
            _visual1.CenterPoint = new Vector3(10);

            base.OnApplyTemplate();
        }

        #region Progress



        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Progress.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(FileButton), new PropertyMetadata(0.0));



        #endregion

        #region ProgressVisibility

        public Visibility ProgressVisibility
        {
            get { return (Visibility)GetValue(ProgressVisibilityProperty); }
            set { SetValue(ProgressVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ProgressVisibilityProperty =
            DependencyProperty.Register("ProgressVisibility", typeof(Visibility), typeof(FileButton), new PropertyMetadata(Visibility.Visible));

        #endregion

        public void SetGlyph(string glyph, bool animate)
        {
            OnGlyphChanged(glyph, Glyph, animate);
        }

        private void OnGlyphChanged(string newValue, string oldValue, bool animate)
        {
            if (string.IsNullOrEmpty(oldValue) || string.IsNullOrEmpty(newValue))
            {
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Glyph = newValue;

            if (_visual == null || _label == null)
            {
                return;
            }

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var labelShow = _visual == _visual1 ? _label2 : _label1;
            var labelHide = _visual == _visual1 ? _label1 : _label2;

            if (animate)
            {
                var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                hide1.InsertKeyFrame(0, new Vector3(1));
                hide1.InsertKeyFrame(1, new Vector3(0));

                var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
                hide2.InsertKeyFrame(0, 1);
                hide2.InsertKeyFrame(1, 0);

                visualHide.StartAnimation("Scale", hide1);
                visualHide.StartAnimation("Opacity", hide2);
            }
            else
            {
                visualHide.Scale = new Vector3(0);
                visualHide.Opacity = 0;
            }

            labelShow.Text = newValue;

            if (animate)
            {
                var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(1, new Vector3(1));
                show1.InsertKeyFrame(0, new Vector3(0));

                var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
                show2.InsertKeyFrame(1, 1);
                show2.InsertKeyFrame(0, 0);

                visualShow.StartAnimation("Scale", show1);
                visualShow.StartAnimation("Opacity", show2);
            }
            else
            {
                visualShow.Scale = new Vector3(1);
                visualShow.Opacity = 1;
            }

            _visual = visualShow;
            _label = labelShow;
        }
    }
}
