//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Native;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class LoadingTextBlock : Control
    {
        private ContainerVisual _skeleton;
        private SpriteVisual _foreground;

        private TextBlock _placeholder;
        private TextBlock _presenter;

        public LoadingTextBlock()
        {
            DefaultStyleKey = typeof(LoadingTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            var ease = Window.Current.Compositor.CreateLinearEasingFunction();
            var animation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector3(-1, 0, 0), ease);
            animation.InsertKeyFrame(1, new Vector3(0, 0, 0), ease);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            var backgroundColor = GetColor(BorderBrushProperty);
            var foregroundColor = GetColor(BackgroundProperty);

            var gradient = Window.Current.Compositor.CreateLinearGradientBrush();
            gradient.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(0, Color.FromArgb(0x00, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
            gradient.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(0.67f, Color.FromArgb(0x67, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
            gradient.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(1, Color.FromArgb(0x00, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(0.5f, 0);
            gradient.ExtendMode = CompositionGradientExtendMode.Wrap;

            var background = Window.Current.Compositor.CreateSpriteVisual();
            background.RelativeSizeAdjustment = Vector2.One;
            background.Brush = Window.Current.Compositor.CreateColorBrush(foregroundColor);

            _foreground = Window.Current.Compositor.CreateSpriteVisual();
            _foreground.RelativeSizeAdjustment = new Vector2(2, 1);
            _foreground.Brush = gradient;
            _foreground.StartAnimation("RelativeOffsetAdjustment", animation);

            _placeholder = GetTemplateChild("Placeholder") as TextBlock;
            _presenter = GetTemplateChild("Presenter") as TextBlock;

            _skeleton = Window.Current.Compositor.CreateContainerVisual();
            _skeleton.Children.InsertAtTop(background);
            _skeleton.Children.InsertAtTop(_foreground);
            _skeleton.Opacity = 0.67f;

            _skeleton.AnchorPoint = new Vector2(IsPlaceholderRightToLeft ? 1 : 0, 0);
            _skeleton.RelativeOffsetAdjustment = new Vector3(IsPlaceholderRightToLeft ? 1 : 0, 0, 0);

            ElementCompositionPreview.SetElementChildVisual(_placeholder, _skeleton);

            base.OnApplyTemplate();
        }

        private Color GetColor(DependencyProperty dp)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                return solid.Color;
            }

            return Colors.Black;
        }

        private CompositionBrush GetBrush(DependencyProperty dp)
        {
            return Window.Current.Compositor.CreateColorBrush(GetColor(dp));
        }

        #region PlaceholderText

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(LoadingTextBlock), new PropertyMetadata(null));

        #endregion

        #region PlaceholderBrush

        public Brush PlaceholderBrush
        {
            get { return (Brush)GetValue(PlaceholderBrushProperty); }
            set { SetValue(PlaceholderBrushProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register("PlaceholderBrush", typeof(Brush), typeof(LoadingTextBlock), new PropertyMetadata(null));

        #endregion

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(LoadingTextBlock), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingTextBlock)d).OnTextChanged((string)e.NewValue, ((LoadingTextBlock)d).PlaceholderText);
        }

        private async void OnTextChanged(string text, string placeholder)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            InvalidateMeasure();
            InvalidateArrange();

            await this.UpdateLayoutAsync();

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _placeholder.Visibility = Visibility.Collapsed;
            };

            var fadeIn = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0, 0);
            fadeIn.InsertKeyFrame(1, 1);

            var visual2 = ElementCompositionPreview.GetElementVisual(_placeholder);
            var visual1 = ElementCompositionPreview.GetElementVisual(_presenter);

            visual1.StartAnimation("Opacity", fadeIn);

            var size1 = _presenter.DesiredSize.ToVector2();
            var size2 = _placeholder.DesiredSize.ToVector2();

            var final = new Vector2(MathF.Max(size1.X, size2.X), MathF.Max(size1.Y, size2.Y));

            StartClip(visual1, true, final);
            StartClip(visual2, false, final);

            batch.End();
        }

        private void StartClip(Visual visual, bool show, Vector2 desiredSize)
        {
            var actualWidth = desiredSize.X;
            var actualHeight = desiredSize.Y;
            var left = (float)Padding.Left;
            var top = (float)Padding.Top;

            var width = MathF.Max(actualWidth - left, actualHeight - top);
            var diaginal = MathF.Sqrt((width * width) + (width * width));

            var device = CanvasDevice.GetSharedDevice();

            var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, show ? 0 : actualWidth, show ? 0 : actualHeight);

            var elli1 = CanvasGeometry.CreateCircle(device, left, top, 0);
            var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var elli2 = CanvasGeometry.CreateCircle(device, left, top, diaginal);
            var group2 = CanvasGeometry.CreateGroup(device, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var ellipse = Window.Current.Compositor.CreatePathGeometry(new CompositionPath(group2));
            var clip = Window.Current.Compositor.CreateGeometricClip(ellipse);

            var ease = Window.Current.Compositor.CreateCubicBezierEasingFunction(new Vector2(.42f, 0), new Vector2(1, 1));
            var anim = Window.Current.Compositor.CreatePathKeyFrameAnimation();
            anim.InsertKeyFrame(0, new CompositionPath(group1), ease);
            anim.InsertKeyFrame(1, new CompositionPath(group2), ease);
            anim.Duration = TimeSpan.FromMilliseconds(500);

            ellipse.StartAnimation("Path", anim);
            visual.Clip = clip;
        }

        #endregion

        #region IsTextSelectionEnabled

        public bool IsTextSelectionEnabled
        {
            get { return (bool)GetValue(IsTextSelectionEnabledProperty); }
            set { SetValue(IsTextSelectionEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsTextSelectionEnabledProperty =
            DependencyProperty.Register("IsTextSelectionEnabled", typeof(bool), typeof(LoadingTextBlock), new PropertyMetadata(false));

        #endregion

        private bool _isPlaceholderRightToLeft;
        public bool IsPlaceholderRightToLeft
        {
            get => _isPlaceholderRightToLeft;
            set
            {
                if (_skeleton != null)
                {
                    _skeleton.AnchorPoint = new Vector2(value ? 1 : 0, 0);
                    _skeleton.RelativeOffsetAdjustment = new Vector3(value ? 1 : 0, 0, 0);
                }

                _isPlaceholderRightToLeft = value;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Telegram.App.Track();

            availableSize = base.MeasureOverride(availableSize);

            if (HorizontalAlignment != HorizontalAlignment.Stretch)
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return _placeholder.DesiredSize;
                }

                return _presenter.DesiredSize;
            }

            if (string.IsNullOrEmpty(Text))
            {
                return availableSize;
            }

            return new Size(availableSize.Width, _presenter.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Telegram.App.Track();

            finalSize = base.ArrangeOverride(finalSize);

            if (_placeholder.DesiredSize.Width == 0)
            {
                return finalSize;
            }

            var device = CanvasDevice.GetSharedDevice();
            var list = new List<CanvasGeometry>();

            var left = (float)Padding.Left;
            var top = (float)Padding.Top;
            var rects = PlaceholderImageHelper.Current.LineMetrics(PlaceholderText ?? string.Empty, _placeholder.FontSize, _placeholder.DesiredSize.Width - Padding.Left - Padding.Right, IsPlaceholderRightToLeft);

            foreach (var rect in rects)
            {
                if (rect.Width < 1 || rect.Height < 1)
                {
                    continue;
                }

                list.Add(CanvasGeometry.CreateRoundedRectangle(device, new Rect(left + rect.X - 4, top + rect.Y - 2, rect.Width + 6, rect.Height + 6), 4, 4));
            }

            _skeleton.Clip = Window.Current.Compositor.CreateGeometricClip(Window.Current.Compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(device, list.ToArray(), CanvasFilledRegionDetermination.Winding))));
            _skeleton.Size = _placeholder.DesiredSize.ToVector2();

            return finalSize;
        }
    }
}
