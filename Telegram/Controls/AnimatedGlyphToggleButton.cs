//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Numerics;
using Telegram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public class AnimatedGlyphToggleButton : ToggleButton
    {
        private FrameworkElement _label1;
        private FrameworkElement _label2;

        private Visual _visual1;
        private Visual _visual2;

        private FrameworkElement _label;
        private Visual _visual;

        protected bool _animateOnToggle = true;

        public AnimatedGlyphToggleButton()
        {
            DefaultStyleKey = typeof(AnimatedGlyphToggleButton);

            Checked += OnToggle;
            Unchecked += OnToggle;
        }

        protected virtual bool IsRuntimeCompatible()
        {
            return false;
        }

        protected override void OnApplyTemplate()
        {
            if (IsRuntimeCompatible())
            {
                return;
            }

            _label1 = _label = GetTemplateChild("ContentPresenter1") as FrameworkElement;
            _label2 = GetTemplateChild("ContentPresenter2") as FrameworkElement;

            if (_label1 != null && _label2 != null)
            {
                _visual1 = _visual = ElementComposition.GetElementVisual(_label1);
                _visual2 = ElementComposition.GetElementVisual(_label2);

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
                    text1.Text = (IsChecked == true ? CheckedGlyph : Glyph) ?? string.Empty;
                }
                else if (_label1 is ContentPresenter presenter1)
                {
                    presenter1.Content = (IsChecked == true ? CheckedContent : Content) ?? new object();
                }

                _visual1.Opacity = 1;
                _visual1.Scale = new Vector3(1);
                _visual1.CenterPoint = new Vector3(10);
            }

            base.OnApplyTemplate();
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (_animateOnToggle is false)
            {
                return;
            }

            if (_label is TextBlock)
            {
                OnGlyphChanged(IsChecked == true ? CheckedGlyph : Glyph);
            }
            else
            {
                OnGlyphChanged(IsChecked == true ? CheckedContent : Content);
            }
        }

        protected async void OnGlyphChanged(object newValue)
        {
            if (_visual == null || _label == null)
            {
                return;
            }

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var labelShow = _visual == _visual1 ? _label2 : _label1;
            var labelHide = _visual == _visual1 ? _label1 : _label2;

            if (labelShow is TextBlock textShow && newValue is string glyph)
            {
                textShow.Text = glyph;
            }
            else if (labelShow is ContentPresenter presenterShow)
            {
                presenterShow.Content = newValue;
            }

            await this.UpdateLayoutAsync();

            _visual1.CenterPoint = new Vector3(_label1.ActualSize / 2f, 0);
            _visual2.CenterPoint = new Vector3(_label2.ActualSize / 2f, 0);

            var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(1));
            hide1.InsertKeyFrame(1, new Vector3(0));

            var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

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
            get => GetValue(CheckedContentProperty);
            set => SetValue(CheckedContentProperty, value);
        }

        public static readonly DependencyProperty CheckedContentProperty =
            DependencyProperty.Register("CheckedContent", typeof(object), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region CheckedGlyph

        public string CheckedGlyph
        {
            get => (string)GetValue(CheckedGlyphProperty);
            set => SetValue(CheckedGlyphProperty, value);
        }

        public static readonly DependencyProperty CheckedGlyphProperty =
            DependencyProperty.Register("CheckedGlyph", typeof(string), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region Glyph

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region IsOneWay

        public bool IsOneWay
        {
            get => (bool)GetValue(IsOneWayProperty);
            set => SetValue(IsOneWayProperty, value);
        }

        public static readonly DependencyProperty IsOneWayProperty =
            DependencyProperty.Register("IsOneWay", typeof(bool), typeof(AnimatedGlyphToggleButton), new PropertyMetadata(true));

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

    public class AnimatedGlyphToggleButtonAutomationPeer : ToggleButtonAutomationPeer
    {
        private readonly AnimatedGlyphToggleButton _owner;

        public AnimatedGlyphToggleButtonAutomationPeer(AnimatedGlyphToggleButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Toggle)
            {
                return null;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override string GetNameCore()
        {
            if (_owner.IsChecked == true && _owner.CheckedContent is string checkedContent)
            {
                return checkedContent;
            }
            else if (_owner.IsChecked == false && _owner.Content is string content)
            {
                return content;
            }

            return base.GetNameCore();
        }
    }
}
