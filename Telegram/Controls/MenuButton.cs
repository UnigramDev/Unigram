//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI;

namespace Telegram.Controls
{
    public enum MenuButtonType
    {
        Back,
        Dismiss,
    }

    public class MenuButton : ToggleButton
    {
        private CompositionSpriteShape _shape1;
        private CompositionSpriteShape _shape2;
        private CompositionSpriteShape _shape3;
        private Visual _content;
        private Visual _visual;

        public MenuButton()
        {
            DefaultStyleKey = typeof(MenuButton);

            Checked += OnToggle;
            Unchecked += OnToggle;

            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_shape1 != null)
            {
                _shape1.StrokeBrush = GetBrush(dp);
                _shape2.StrokeBrush = GetBrush(dp);
                _shape3.StrokeBrush = GetBrush(dp);
            }
        }

        private CompositionBrush GetBrush(DependencyProperty dp)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                return BootStrapper.Current.Compositor.CreateColorBrush(solid.Color);
            }

            return BootStrapper.Current.Compositor.CreateColorBrush(Colors.White);
        }

        protected override void OnApplyTemplate()
        {
            var line1 = BootStrapper.Current.Compositor.CreateLineGeometry();
            var line2 = BootStrapper.Current.Compositor.CreateLineGeometry();
            var line3 = BootStrapper.Current.Compositor.CreateLineGeometry();

            line1.Start = new Vector2(0, 3.5f);
            line1.End = new Vector2(16, 3.5f);

            line2.Start = new Vector2(0, 8.5f);
            line2.End = new Vector2(16, 8.5f);

            line3.Start = new Vector2(0, 13.5f);
            line3.End = new Vector2(16, 13.5f);

            var shape1 = BootStrapper.Current.Compositor.CreateSpriteShape(line1);
            shape1.StrokeThickness = 1;
            shape1.StrokeBrush = GetBrush(ForegroundProperty);
            shape1.IsStrokeNonScaling = true;
            shape1.CenterPoint = new Vector2(8, 4);

            var shape2 = BootStrapper.Current.Compositor.CreateSpriteShape(line2);
            shape2.StrokeThickness = 1;
            shape2.StrokeBrush = GetBrush(ForegroundProperty);
            shape2.IsStrokeNonScaling = true;

            var shape3 = BootStrapper.Current.Compositor.CreateSpriteShape(line3);
            shape3.StrokeThickness = 1;
            shape3.StrokeBrush = GetBrush(ForegroundProperty);
            shape3.IsStrokeNonScaling = true;
            shape3.CenterPoint = new Vector2(8, 12);

            var visual1 = BootStrapper.Current.Compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape3);
            visual1.Shapes.Add(shape2);
            visual1.Shapes.Add(shape1);
            visual1.Size = new Vector2(16, 16);
            visual1.CenterPoint = new Vector3(8, 8, 0);

            //visual1.BorderMode = CompositionBorderMode.Hard;

            _shape1 = shape1;
            _shape2 = shape2;
            _shape3 = shape3;
            _visual = visual1;

            var layoutRoot = GetTemplateChild("LayoutRoot") as UIElement;
            if (layoutRoot != null)
            {
                ElementCompositionPreview.SetElementChildVisual(layoutRoot, visual1);
            }

            var presenter = GetTemplateChild("Presenter") as UIElement;
            if (presenter != null)
            {
                _content = ElementComposition.GetElementVisual(presenter);
            }
        }

        protected override void OnToggle()
        {
            if (Type == MenuButtonType.Dismiss)
            {
                base.OnToggle();
            }
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (_visual == null)
            {
                return;
            }

            var show = IsChecked == true;

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            //batch.Completed += (s, args) =>
            //{
            //    _visual.BorderMode = show /*&& Type == MenuButtonType.Dismiss*/ ? CompositionBorderMode.Soft : CompositionBorderMode.Hard;
            //    _visual.BorderMode = CompositionBorderMode.Soft;
            //};

            if (Type == MenuButtonType.Back)
            {
                var angle = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                angle.InsertKeyFrame(0, show ? 0 : -180);
                angle.InsertKeyFrame(1, show ? 180 : 0);

                var start1 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                start1.InsertKeyFrame(show ? 0 : 1, new Vector2(0, 3.5f));
                start1.InsertKeyFrame(show ? 1 : 0, new Vector2(8.5f, 1.5f));

                var start3 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                start3.InsertKeyFrame(show ? 0 : 1, new Vector2(0, 13.5f));
                start3.InsertKeyFrame(show ? 1 : 0, new Vector2(8.5f, 15.5f));

                var end1 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                end1.InsertKeyFrame(show ? 0 : 1, new Vector2(16, 3.5f));
                end1.InsertKeyFrame(show ? 1 : 0, new Vector2(15.5f, 8.5f));

                var end3 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                end3.InsertKeyFrame(show ? 0 : 1, new Vector2(16, 13.5f));
                end3.InsertKeyFrame(show ? 1 : 0, new Vector2(15.5f, 8.5f));

                //_visual.BorderMode = CompositionBorderMode.Soft;
                _visual.StartAnimation("RotationAngleInDegrees", angle);
                _shape1.Geometry.StartAnimation("Start", start1);
                _shape3.Geometry.StartAnimation("Start", start3);
                _shape1.Geometry.StartAnimation("End", end1);
                _shape3.Geometry.StartAnimation("End", end3);
            }
            else if (Type == MenuButtonType.Dismiss)
            {
                var angle2 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                angle2.InsertKeyFrame(show ? 0 : 1, 0);
                angle2.InsertKeyFrame(show ? 1 : 0, 90 + 45);

                var angle1 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                angle1.InsertKeyFrame(show ? 0 : 1, 0);
                angle1.InsertKeyFrame(show ? 1 : 0, 90);

                var offset1 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                offset1.InsertKeyFrame(show ? 0 : 1, Vector2.Zero);
                offset1.InsertKeyFrame(show ? 1 : 0, new Vector2(0.5f, 4));

                var offset2 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                offset2.InsertKeyFrame(show ? 0 : 1, Vector2.Zero);
                offset2.InsertKeyFrame(show ? 1 : 0, new Vector2(0, -0.5f));

                var offset3 = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                offset3.InsertKeyFrame(show ? 0 : 1, Vector2.Zero);
                offset3.InsertKeyFrame(show ? 1 : 0, new Vector2(0.5f, -4));

                var opacity3 = BootStrapper.Current.Compositor.CreateColorKeyFrameAnimation();
                opacity3.InsertKeyFrame(show ? 0 : 1, Color.FromArgb(0xff, 0xff, 0xff, 0xff));
                opacity3.InsertKeyFrame(show ? 1 : 0, Color.FromArgb(0x00, 0xff, 0xff, 0xff));

                var opacityContent = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                opacityContent.InsertKeyFrame(show ? 0 : 1, 1);
                opacityContent.InsertKeyFrame(show ? 1 : 0, 0);

                _content?.StartAnimation("Opacity", opacityContent);

                _shape3.StrokeBrush.StartAnimation("Color", opacity3);

                //_visual.BorderMode = CompositionBorderMode.Soft;
                _visual.StartAnimation("RotationAngleInDegrees", angle2);
                _shape1.StartAnimation("RotationAngleInDegrees", angle1);
                _shape3.StartAnimation("RotationAngleInDegrees", angle1);
                _shape1.StartAnimation("Offset", offset1);
                _shape2.StartAnimation("Offset", offset2);
                _shape3.StartAnimation("Offset", offset3);
            }

            batch.End();
        }

        #region Type

        public MenuButtonType Type
        {
            get => (MenuButtonType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(MenuButtonType), typeof(MenuButton), new PropertyMetadata(MenuButtonType.Dismiss));

        #endregion

    }
}
