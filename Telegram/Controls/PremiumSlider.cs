//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Native.Composition;
using Telegram.Views.Stars.Popups;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Controls
{
    public partial class PremiumSlider : Slider
    {
        private AnimatedTextBlock ValueText;
        private Grid ValueRoot;
        private Border ValueThumb;
        private Canvas ThumbRoot;
        private Grid Thumb;
        private Path Arrow;

        private Grid SliderContainer;
        private Thumb HorizontalThumb;

        private DirectRectangleClip2 _thumbClip;
        private DirectRectangleClip2 _valueClip;

        private Visual _valueThumb;
        private Visual _thumbRoot;
        private Visual _thumb;
        private Visual _arrow;

        private HorizontalAlignment _arrowAlignment;
        private double _prevValue = 0;

        private SteppedValue _stepped;

        public PremiumSlider()
        {
            DefaultStyleKey = typeof(PremiumSlider);
        }

        public void Initialize(int value, long maxRealValue)
        {
            var sliderSteps = new List<int> { 1, 10, 50, 100, 500, 1_000, 2_000, 5_000, 7_500, 10_000 };
            sliderSteps.RemoveAll(x => x >= maxRealValue);
            sliderSteps.Add((int)maxRealValue);

            _stepped = new SteppedValue(100, sliderSteps);
            Value = _stepped.getProgress(value) * Maximum;
        }

        private void UpdateText()
        {
            ValueText.Text = _stepped.getValue(Value / Maximum).ToString("N0");
        }

        public int RealValue => _stepped.getValue(Value / Maximum);

        protected override void OnApplyTemplate()
        {
            ValueText = GetTemplateChild(nameof(ValueText)) as AnimatedTextBlock;
            ValueRoot = GetTemplateChild(nameof(ValueRoot)) as Grid;
            ValueThumb = GetTemplateChild(nameof(ValueThumb)) as Border;
            ThumbRoot = GetTemplateChild(nameof(ThumbRoot)) as Canvas;
            Thumb = GetTemplateChild(nameof(Thumb)) as Grid;
            Arrow = GetTemplateChild(nameof(Arrow)) as Path;

            ElementCompositionPreview.SetIsTranslationEnabled(Arrow, true);

            _valueThumb = ElementComposition.GetElementVisual(ValueThumb);
            _thumbRoot = ElementComposition.GetElementVisual(ThumbRoot);
            _thumb = ElementComposition.GetElementVisual(Thumb);
            _arrow = ElementComposition.GetElementVisual(Arrow);

            var radius1 = new Vector2(20);
            var radius2 = new Vector2(10);

            _thumbRoot.CenterPoint = new Vector3(0, 46, 0);

            _thumbClip = CompositionDevice.CreateRectangleClip2(Thumb.Children[0]);
            _thumbClip.Set(radius1);

            _valueClip = CompositionDevice.CreateRectangleClip2(ValueRoot);
            _valueClip.Set(radius2);

            Thumb.SizeChanged += OnSizeChanged;

            HorizontalThumb = GetTemplateChild(nameof(HorizontalThumb)) as Thumb;
            SliderContainer = GetTemplateChild(nameof(SliderContainer)) as Grid;

            SliderContainer.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            SliderContainer.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);

            UpdateText();

            OnValueChanged(_prevValue, Value);
            base.OnApplyTemplate();
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)
            {
                var increment = (e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1);

                var prev = _stepped.getValue(Value / Maximum);
                var next = _stepped.getValue((Value + increment) / Maximum);

                if (prev == next)
                {
                    Value = _stepped.getProgress(prev + increment) * Maximum;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        enum PointerState
        {
            Released = 0,
            Moved = 1,
            Pressed = 2
        }

        private PointerState _animateState;

        private double _animateTo;
        private double _animateFrom;

        private double _oldValue;
        private double _newValue;
        private float _angle;

        private bool _animating;

        private EventHandler<object> _layoutUpdated;
        private EventHandler<object> _rendering;

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _animateState = PointerState.Pressed;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_animateState != PointerState.Moved)
            {
                return;
            }

            var anim = new DoubleAnimation();
            anim.From = _animateFrom;
            anim.To = _animateTo;
            anim.Duration = TimeSpan.FromMilliseconds(333);
            anim.EnableDependentAnimation = true;
            anim.EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
            {
                EasingMode = EasingMode.EaseInOut
            };

            Storyboard.SetTarget(anim, this);
            Storyboard.SetTargetProperty(anim, "Value");

            _animating = true;

            _oldValue = _animateFrom;
            _newValue = _animateFrom;

            var storyboard = new Storyboard();
            storyboard.Children.Add(anim);
            storyboard.Begin();
        }

        private void OnRendering(object sender, object e)
        {
            var progress = Math.Max((float)Math.Abs(_oldValue - _newValue), 1);
            progress = MathF.Log(progress, (float)Maximum / 16);
            progress = progress * MathF.Pow(progress, 2);

            var bend = 24 * Math.Clamp(progress, 0, 10);
            var angle = _oldValue < _newValue ? bend : -bend;

            if (Math.Abs(_angle - angle) > .01f)
            {
                angle = MathFEx.Lerp(_angle, angle, .1f);
            }

            _thumbRoot.RotationAngleInDegrees = angle;

            _oldValue = _newValue;
            _angle = angle;

            if (angle.AlmostEqualsToZero(1e-2f) && (HorizontalThumb.PointerCaptures == null || HorizontalThumb.PointerCaptures.Count == 0))
            {
                _thumbRoot.RotationAngleInDegrees = 0;
                _rendering = null;

                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
            }
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            if (_animateState == PointerState.Pressed || _animating)
            {
                _animateFrom = oldValue;
                _animateTo = newValue;

                _animateState = PointerState.Moved;
                _animating = false;
                return;
            }

            _newValue = newValue;
            _animateState = PointerState.Released;

            base.OnValueChanged(oldValue, newValue);

            if (ValueText == null)
            {
                return;
            }

            UpdateText();
            UpdateClip();

            //if (_layoutUpdated == null)
            //{
            //    LayoutUpdated += _layoutUpdated = new EventHandler<object>(OnLayoutUpdated);
            //}

            if (_rendering == null)
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += _rendering = new EventHandler<object>(OnRendering);
            }
        }

        private void OnLayoutUpdated(object sender, object e)
        {
            _layoutUpdated = null;
            LayoutUpdated -= OnLayoutUpdated;

            UpdateClip();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_layoutUpdated == null)
            {
                UpdateClip();
            }
        }

        private void UpdateClip()
        {
            if (ValueRoot == null || Thumb == null)
            {
                return;
            }

            var value = (float)((Value - Minimum) / (Maximum - Minimum));
            var width = (ValueRoot.ActualSize.X - (ValueRoot.ActualSize.Y)) * (float.IsNaN(value) ? 0 : value);

            var radius = new Vector2(ValueRoot.ActualSize.Y / 2);

            _valueClip.SetInset(0, 0, width + ValueRoot.ActualSize.Y, ValueRoot.ActualSize.Y);
            _valueThumb.Offset = new Vector3(width + 2, 2, 0);
            _thumbRoot.Offset = new Vector3(width + radius.X, 0, 0);

            var center = Thumb.ActualSize.X / 2;
            width = (ValueRoot.ActualSize.X - (ValueRoot.ActualSize.Y)) * (float)((Value - Minimum) / (Maximum - Minimum));
            width += ValueRoot.ActualSize.Y / 2;

            var radiusLeft = 20f;
            var radiusRight = 20f;

            if (width < center - 12 || Value == Minimum)
            {
                radiusLeft = width - center + 12;

                _thumb.Offset = new Vector3(-width - 12, 0, 0);
                _arrow.Properties.InsertVector3("Translation", new Vector3(radiusLeft, 0, 0));

                Arrow.Fill = new SolidColorBrush(ColorsHelper.Mix(
                    Color.FromArgb(0xFF, 0xEE, 0xAC, 0x0D),
                    Color.FromArgb(0xFF, 0xF9, 0xD3, 0x16), 0.25));
            }
            else if (width > ValueRoot.ActualSize.X - center + 12 || Value == Maximum)
            {
                radiusRight = ValueRoot.ActualSize.X - width - Thumb.ActualSize.X + center + 12;

                _thumb.Offset = new Vector3((ValueRoot.ActualSize.X - width - Thumb.ActualSize.X + 12), 0, 0);
                _arrow.Properties.InsertVector3("Translation", new Vector3(-radiusRight, 0, 0));

                Arrow.Fill = new SolidColorBrush(ColorsHelper.Mix(
                    Color.FromArgb(0xFF, 0xEE, 0xAC, 0x0D),
                    Color.FromArgb(0xFF, 0xF9, 0xD3, 0x16), 0.75));
            }
            else
            {
                _thumb.Offset = new Vector3(-Thumb.ActualSize.X / 2, 0, 0);
                _arrow.Properties.InsertVector3("Translation", new Vector3());

                Arrow.Fill = new SolidColorBrush(ColorsHelper.Mix(
                    Color.FromArgb(0xFF, 0xEE, 0xAC, 0x0D),
                    Color.FromArgb(0xFF, 0xF9, 0xD3, 0x16), 0.5));
            }

            Vector2 CalculateRadius(float diff)
            {
                diff = center + diff - Arrow.ActualSize.X / 2;
                diff = Math.Min(diff + 2, 20);
                diff = Math.Max(diff, 6);
                return new Vector2(diff, 20);
            }

            _thumbClip.SetInset(0, 0, Thumb.ActualSize.X, 40);

            _thumbClip.BottomLeft = CalculateRadius(radiusLeft);
            _thumbClip.BottomRight = CalculateRadius(radiusRight);
        }

        #region MinimumText

        public string MinimumText
        {
            get { return (string)GetValue(MinimumTextProperty); }
            set { SetValue(MinimumTextProperty, value); }
        }

        public static readonly DependencyProperty MinimumTextProperty =
            DependencyProperty.Register("MinimumText", typeof(string), typeof(PremiumSlider), new PropertyMetadata(string.Empty));

        #endregion

        #region MaximumText

        public string MaximumText
        {
            get { return (string)GetValue(MaximumTextProperty); }
            set { SetValue(MaximumTextProperty, value); }
        }

        public static readonly DependencyProperty MaximumTextProperty =
            DependencyProperty.Register("MaximumText", typeof(string), typeof(PremiumSlider), new PropertyMetadata(string.Empty));

        #endregion

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(PremiumSlider), new PropertyMetadata(string.Empty));

        #endregion
    }
}
