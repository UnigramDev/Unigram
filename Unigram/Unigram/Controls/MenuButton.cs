using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class MenuButton : ToggleButton
    {
        private CompositionSpriteShape _shape1;
        private CompositionSpriteShape _shape2;
        private CompositionSpriteShape _shape3;
        private Visual _visual;

        public MenuButton()
        {
            DefaultStyleKey = typeof(MenuButton);

            Checked += OnToggle;
            Unchecked += OnToggle;
        }

        protected override void OnApplyTemplate()
        {
            var line1 = Window.Current.Compositor.CreateLineGeometry();
            var line2 = Window.Current.Compositor.CreateLineGeometry();
            var line3 = Window.Current.Compositor.CreateLineGeometry();

            line1.Start = new Vector2(0, 4.5f);
            line1.End = new Vector2(16, 4.5f);

            line2.Start = new Vector2(0, 8.5f);
            line2.End = new Vector2(16, 8.5f);

            line3.Start = new Vector2(0, 12.5f);
            line3.End = new Vector2(16, 12.5f);

            var shape1 = Window.Current.Compositor.CreateSpriteShape(line1);
            shape1.StrokeThickness = 1;
            shape1.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Colors.White);
            shape1.IsStrokeNonScaling = true;
            shape1.CenterPoint = new Vector2(8, 4);

            var shape2 = Window.Current.Compositor.CreateSpriteShape(line2);
            shape2.StrokeThickness = 1;
            shape2.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Colors.White);
            shape2.IsStrokeNonScaling = true;

            var shape3 = Window.Current.Compositor.CreateSpriteShape(line3);
            shape3.StrokeThickness = 1;
            shape3.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Colors.White);
            shape3.IsStrokeNonScaling = true;
            shape3.CenterPoint = new Vector2(8, 12);

            var visual1 = Window.Current.Compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape3);
            visual1.Shapes.Add(shape2);
            visual1.Shapes.Add(shape1);
            visual1.Size = new Vector2(16, 16);
            visual1.CenterPoint = new Vector3(8, 8, 0);

            visual1.BorderMode = CompositionBorderMode.Hard;

            _shape1 = shape1;
            _shape2 = shape2;
            _shape3 = shape3;
            _visual = visual1;

            var layoutRoot = GetTemplateChild("LayoutRoot") as UIElement;
            if (layoutRoot != null)
            {
                ElementCompositionPreview.SetElementChildVisual(layoutRoot, visual1);
            }
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (_visual == null)
            {
                return;
            }

            var show = IsChecked == true;

            var angle2 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            angle2.InsertKeyFrame(show ? 0 : 1, 0);
            angle2.InsertKeyFrame(show ? 1 : 0, 90 + 45);

            var angle1 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            angle1.InsertKeyFrame(show ? 0 : 1, 0);
            angle1.InsertKeyFrame(show ? 1 : 0, 90);

            var offset1 = Window.Current.Compositor.CreateVector2KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, Vector2.Zero);
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector2(0.5f, 4));

            var offset2 = Window.Current.Compositor.CreateVector2KeyFrameAnimation();
            offset2.InsertKeyFrame(show ? 0 : 1, Vector2.Zero);
            offset2.InsertKeyFrame(show ? 1 : 0, new Vector2(0, -0.5f));

            var offset3 = Window.Current.Compositor.CreateVector2KeyFrameAnimation();
            offset3.InsertKeyFrame(show ? 0 : 1, Vector2.Zero);
            offset3.InsertKeyFrame(show ? 1 : 0, new Vector2(0.5f, -4));

            var opacity3 = Window.Current.Compositor.CreateColorKeyFrameAnimation();
            opacity3.InsertKeyFrame(show ? 0 : 1, Color.FromArgb(0xff, 0xff, 0xff, 0xff));
            opacity3.InsertKeyFrame(show ? 1 : 0, Color.FromArgb(0x00, 0xff, 0xff, 0xff));

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _visual.BorderMode = show ? CompositionBorderMode.Soft : CompositionBorderMode.Hard;
            };

            _shape3.StrokeBrush.StartAnimation("Color", opacity3);

            _visual.StartAnimation("RotationAngleInDegrees", angle2);
            _shape1.StartAnimation("RotationAngleInDegrees", angle1);
            _shape3.StartAnimation("RotationAngleInDegrees", angle1);
            _shape1.StartAnimation("Offset", offset1);
            _shape2.StartAnimation("Offset", offset2);
            _shape3.StartAnimation("Offset", offset3);

            batch.End();
        }
    }
}
