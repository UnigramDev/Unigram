//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class LoadingTextBlock : Control
    {
        private ContainerVisual _skeleton;
        private SpriteVisual _foreground;

        private TextBlock Placeholder;
        private TextBlock Presenter;

        public LoadingTextBlock()
        {
            DefaultStyleKey = typeof(LoadingTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            var ease = BootStrapper.Current.Compositor.CreateLinearEasingFunction();
            var animation = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector3(-1, 0, 0), ease);
            animation.InsertKeyFrame(1, new Vector3(0, 0, 0), ease);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            var backgroundColor = GetColor(BorderBrushProperty);
            var foregroundColor = GetColor(BackgroundProperty);

            var gradient = BootStrapper.Current.Compositor.CreateLinearGradientBrush();
            gradient.ColorStops.Add(BootStrapper.Current.Compositor.CreateColorGradientStop(0, Color.FromArgb(0x00, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
            gradient.ColorStops.Add(BootStrapper.Current.Compositor.CreateColorGradientStop(0.67f, Color.FromArgb(0x67, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
            gradient.ColorStops.Add(BootStrapper.Current.Compositor.CreateColorGradientStop(1, Color.FromArgb(0x00, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(0.5f, 0);
            gradient.ExtendMode = CompositionGradientExtendMode.Wrap;

            var background = BootStrapper.Current.Compositor.CreateSpriteVisual();
            background.RelativeSizeAdjustment = Vector2.One;
            background.Brush = BootStrapper.Current.Compositor.CreateColorBrush(foregroundColor);

            _foreground = BootStrapper.Current.Compositor.CreateSpriteVisual();
            _foreground.RelativeSizeAdjustment = new Vector2(2, 1);
            _foreground.Brush = gradient;
            _foreground.StartAnimation("RelativeOffsetAdjustment", animation);

            Placeholder = GetTemplateChild(nameof(Placeholder)) as TextBlock;
            Presenter = GetTemplateChild(nameof(Presenter)) as TextBlock;

            _skeleton = BootStrapper.Current.Compositor.CreateContainerVisual();
            _skeleton.Children.InsertAtTop(background);
            _skeleton.Children.InsertAtTop(_foreground);
            _skeleton.Opacity = 0.67f;

            _skeleton.AnchorPoint = new Vector2(IsPlaceholderRightToLeft ? 1 : 0, 0);
            _skeleton.RelativeOffsetAdjustment = new Vector3(IsPlaceholderRightToLeft ? 1 : 0, 0, 0);

            ElementCompositionPreview.SetElementChildVisual(Placeholder, _skeleton);

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
            return BootStrapper.Current.Compositor.CreateColorBrush(GetColor(dp));
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
            if (Presenter == null || Placeholder == null)
            {
                return;
            }

            var visual1 = ElementComposition.GetElementVisual(Presenter);
            var visual2 = ElementComposition.GetElementVisual(Placeholder);

            if (string.IsNullOrEmpty(text))
            {
                Placeholder.Visibility = Visibility.Visible;

                visual1.Clip = null;
                visual2.Clip = null;
                return;
            }

            InvalidateMeasure();

            await this.UpdateLayoutAsync();

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Placeholder.Visibility = Visibility.Collapsed;
            };

            var fadeIn = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0, 0);
            fadeIn.InsertKeyFrame(1, 1);

            visual1.StartAnimation("Opacity", fadeIn);

            var size1 = Presenter.ActualSize;
            var size2 = Placeholder.ActualSize;

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

            var rect1 = CanvasGeometry.CreateRectangle(null, 0, 0, show ? 0 : actualWidth, show ? 0 : actualHeight);

            var elli1 = CanvasGeometry.CreateCircle(null, left, top, 0);
            var group1 = CanvasGeometry.CreateGroup(null, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var elli2 = CanvasGeometry.CreateCircle(null, left, top, diaginal);
            var group2 = CanvasGeometry.CreateGroup(null, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var ellipse = BootStrapper.Current.Compositor.CreatePathGeometry(new CompositionPath(group2));
            var clip = BootStrapper.Current.Compositor.CreateGeometricClip(ellipse);

            var ease = BootStrapper.Current.Compositor.CreateCubicBezierEasingFunction(new Vector2(.42f, 0), new Vector2(1, 1));
            var anim = BootStrapper.Current.Compositor.CreatePathKeyFrameAnimation();
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
            availableSize = base.MeasureOverride(availableSize);

            if (HorizontalAlignment != HorizontalAlignment.Stretch)
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return Placeholder.DesiredSize;
                }

                return Presenter.DesiredSize;
            }

            if (string.IsNullOrEmpty(Text))
            {
                return availableSize;
            }

            return new Size(availableSize.Width, Presenter.DesiredSize.Height);
        }

        // Inputs the skeleton clip is derived from. Cached because rebuilding it costs
        // a native text measurement, one D2D geometry per line, a group, a path, a path
        // geometry and a clip, and none of these change between most arrange passes.
        private string _clipText;
        private double _clipFontSize;
        private double _clipWidth;
        private bool _clipRightToLeft;
        private Thickness _clipPadding;
        private bool _clipValid;

        protected override Size ArrangeOverride(Size finalSize)
        {
            finalSize = base.ArrangeOverride(finalSize);

            if (Placeholder.DesiredSize.Width == 0)
            {
                return finalSize;
            }

            UpdateSkeletonClip();

            _skeleton.Size = Placeholder.DesiredSize.ToVector2();

            return finalSize;
        }

        private void UpdateSkeletonClip()
        {
            var text = PlaceholderText ?? string.Empty;
            var fontSize = Placeholder.FontSize;
            var width = Placeholder.DesiredSize.Width;
            var rightToLeft = IsPlaceholderRightToLeft;
            var padding = Padding;

            if (_clipValid
                && _clipFontSize == fontSize
                && _clipWidth == width
                && _clipRightToLeft == rightToLeft
                && _clipPadding.Equals(padding)
                && _clipText == text)
            {
                return;
            }

            _clipText = text;
            _clipFontSize = fontSize;
            _clipWidth = width;
            _clipRightToLeft = rightToLeft;
            _clipPadding = padding;
            _clipValid = true;

            var left = (float)padding.Left;
            var top = (float)padding.Top;
            var rects = PlaceholderHelper.Foreground.LineMetrics(text, Array.Empty<TextStylePart>(), fontSize, width - padding.Left - padding.Right, rightToLeft);

            // Sized up front and filled by index: CreateGroup takes an array, and the
            // count is known before the loop starts.
            var geometries = new CanvasGeometry[rects.Count];
            var count = 0;

            for (int i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];

                if (rect.Width < 1 || rect.Height < 1)
                {
                    continue;
                }

                geometries[count++] = CanvasGeometry.CreateRoundedRectangle(null,
                    new Rect(left + rect.X - 4, top + rect.Y - 2, rect.Width + 6, rect.Height + 6), 4, 4);
            }

            if (count < geometries.Length)
            {
                Array.Resize(ref geometries, count);
            }

            var compositor = BootStrapper.Current.Compositor;
            var group = CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding);

            _skeleton.Clip = compositor.CreateGeometricClip(compositor.CreatePathGeometry(new CompositionPath(group)));
        }
    }
}
