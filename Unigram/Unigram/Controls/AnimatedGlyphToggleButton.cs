﻿using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class AnimatedGlyphToggleButton : ToggleButton
    {
        private FrameworkElement _label1;
        private FrameworkElement _label2;

        private Visual _visual1;
        private Visual _visual2;

        private FrameworkElement _label;
        private Visual _visual;

        public AnimatedGlyphToggleButton()
        {
            DefaultStyleKey = typeof(AnimatedGlyphToggleButton);

            Checked += OnToggle;
            Unchecked += OnToggle;
        }

        protected override void OnApplyTemplate()
        {
            _label1 = _label = GetTemplateChild("ContentPresenter1") as FrameworkElement;
            _label2 = GetTemplateChild("ContentPresenter2") as FrameworkElement;

            _visual1 = _visual = ElementCompositionPreview.GetElementVisual(_label1);
            _visual2 = ElementCompositionPreview.GetElementVisual(_label2);

            if (_label2 is TextBlock text2)
            {
                text2.Text = string.Empty;
            }
            else if (_label2 is ContentPresenter presenter2)
            {
                presenter2.Content = new object();
            }

            _visual2.Opacity = 0;
            _visual2.Scale = new Vector3();
            _visual2.CenterPoint = new Vector3(10);

            if (_label1 is TextBlock text1)
            {
                text1.Text = IsChecked == true ? CheckedGlyph : Glyph ?? string.Empty;
            }
            else if (_label1 is ContentPresenter presenter1)
            {
                presenter1.Content = IsChecked == true ? CheckedContent : Content ?? new object();
            }

            _visual1.Opacity = 1;
            _visual1.Scale = new Vector3(1);
            _visual1.CenterPoint = new Vector3(10);

            base.OnApplyTemplate();
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (_visual == null || _label == null)
            {
                return;
            }

            _visual1.CenterPoint = new Vector3((float)_label1.ActualWidth / 2f, (float)_label1.ActualHeight / 2f, 0);
            _visual2.CenterPoint = new Vector3((float)_label2.ActualWidth / 2f, (float)_label2.ActualHeight / 2f, 0);

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

            if (labelShow is TextBlock textShow)
            {
                textShow.Text = IsChecked == true ? CheckedGlyph : Glyph;
            }
            else if (labelShow is ContentPresenter presenterShow)
            {
                presenterShow.Content = IsChecked == true ? CheckedContent : Content;
            }

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

        #region CheckedContent

        public object CheckedContent
        {
            get { return (object)GetValue(CheckedContentProperty); }
            set { SetValue(CheckedContentProperty, value); }
        }

        public static readonly DependencyProperty CheckedContentProperty =
            DependencyProperty.Register("CheckedContent", typeof(object), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region CheckedGlyph

        public string CheckedGlyph
        {
            get { return (string)GetValue(CheckedGlyphProperty); }
            set { SetValue(CheckedGlyphProperty, value); }
        }

        public static readonly DependencyProperty CheckedGlyphProperty =
            DependencyProperty.Register("CheckedGlyph", typeof(string), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region IsOneWay

        public bool IsOneWay
        {
            get { return (bool)GetValue(IsOneWayProperty); }
            set { SetValue(IsOneWayProperty, value); }
        }

        public static readonly DependencyProperty IsOneWayProperty =
            DependencyProperty.Register("IsOneWay", typeof(bool), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(true));

        #endregion

        #region Radius

        public CornerRadius Radius
        {
            get { return (CornerRadius)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(CornerRadius), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(default(CornerRadius)));

        #endregion

        protected override void OnToggle()
        {
            if (IsOneWay)
            {
                var binding = GetBindingExpression(IsCheckedProperty);
                if (binding != null && binding.ParentBinding.Mode == BindingMode.TwoWay)
                {
                    base.OnToggle();
                }
            }
            else
            {
                base.OnToggle();
            }
        }
    }
}
