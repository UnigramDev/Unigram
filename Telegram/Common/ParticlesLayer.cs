using System;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Common
{
    public class ParticlesLayer
    {
        private readonly Random _random = new();

        private readonly FrameworkElement _element;

        private readonly Compositor _compositor;
        private readonly ShapeVisual _visual;

        private bool _attached;

        public ParticlesLayer(FrameworkElement element)
        {
            _element = element;

            _compositor = Window.Current.Compositor;
            _visual = Window.Current.Compositor.CreateShapeVisual();
            ElementCompositionPreview.SetElementChildVisual(element, _visual);
        }

        public void Resume()
        {
            if (_attached)
            {
                return;
            }

            _attached = true;
            _element.SizeChanged += OnSizeChanged;

            if (_element.ActualWidth > 0 && _element.ActualHeight > 0)
            {
                Prepare();
            }
        }

        public void Suspend()
        {
            _attached = false;
            _element.SizeChanged -= OnSizeChanged;

            _visual.Shapes.Clear();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _visual.Size = e.NewSize.ToVector2();
            Prepare();
        }

        private const bool IS_MOBILE = false;

        private void Prepare()
        {
            var count = (int)Math.Round(_element.ActualWidth * _element.ActualHeight / (35 * (IS_MOBILE ? 2 : 1)));
            count *= /*this.multiply ||*/ 1;
            count = Math.Min(/*!liteMode.isAvailable('chat_spoilers') ? 400 :*/ IS_MOBILE ? 1000 : 2200, count);
            //count = Math.Round(count);
            //_particles = new Particle[count];

            for (int i = 0; i < Math.Max(count, _visual.Shapes.Count); ++i)
            {
                if (i < count)
                {
                    GenerateParticle(i);
                }
                else
                {
                    _visual.Shapes.RemoveAt(i);
                }
            }
        }

        private void GenerateParticle(int index)
        {
            var opacity = _random.NextDouble();
            var adding = _random.NextDouble() >= .5;

            var delay = adding ? 1 - opacity : 1 + opacity;

            CompositionEllipseGeometry ellipse;
            CompositionSpriteShape shape;

            if (_visual.Shapes.Count <= index)
            {
                ellipse = _compositor.CreateEllipseGeometry();
                shape = _compositor.CreateSpriteShape(ellipse);

                _visual.Shapes.Add(shape);
            }
            else
            {
                shape = _visual.Shapes[index] as CompositionSpriteShape;
                ellipse = shape.Geometry as CompositionEllipseGeometry;
            }

            shape.FillBrush ??= _compositor.CreateColorBrush(Colors.White);

            var animOpacity = _compositor.CreateColorKeyFrameAnimation();
            var animCenter = _compositor.CreateVector2KeyFrameAnimation();
            var animRadius = _compositor.CreateVector2KeyFrameAnimation();

            var cycles = 10;
            var cycle = 1f / cycles;

            var easing = _compositor.CreateStepEasingFunction(1);
            var duration = TimeSpan.FromMilliseconds(1000 * cycles);

            for (int i = 0; i < cycles; i++)
            {
                var x = (float)Math.Floor(_random.NextDouble() * _element.ActualWidth);
                var y = (float)Math.Floor(_random.NextDouble() * _element.ActualHeight);
                var radius = (float)(_random.NextDouble() >= .8 ? 1 : 0.5) * 1 /*canvas.DpiScale*/;

                animCenter.InsertKeyFrame(i * cycle, new Vector2(x, y), easing);
                animRadius.InsertKeyFrame(i * cycle, new Vector2(radius), easing);

                animOpacity.InsertKeyFrame(i * cycle, Color.FromArgb(255, 255, 255, 255));
                animOpacity.InsertKeyFrame(i * cycle + (cycle / 2f), Color.FromArgb(0, 255, 255, 255));

                if (i == 0)
                {
                    ellipse.Center = new Vector2(x, y);
                    ellipse.Radius = new Vector2(radius);

                    animCenter.InsertKeyFrame(1, new Vector2(x, y), easing);
                    animRadius.InsertKeyFrame(1, new Vector2(radius), easing);

                    animOpacity.InsertKeyFrame(1, Color.FromArgb(255, 255, 255, 255));
                }
            }

            //animOpacity.IterationBehavior =
            //    animCenter.IterationBehavior =
            //        animRadius.IterationBehavior = AnimationIterationBehavior.Forever;
            //animOpacity.Duration =
            //     animCenter.Duration =
            //        animRadius.Duration = duration;

            //var controller = _compositor.CreateAnimationController();
            //controller.Progress = (float)delay / cycles;

            //shape.FillBrush.StartAnimation("Color", animOpacity, controller);
            //ellipse.StartAnimation("Center", animCenter, controller);
            //ellipse.StartAnimation("Radius", animRadius, controller);
        }
    }
}
