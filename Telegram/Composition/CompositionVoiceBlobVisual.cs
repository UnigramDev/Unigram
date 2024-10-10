using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public partial class CompositionVoiceBlobVisual
    {
        private readonly SpriteVisual _visual;

        private readonly CompositionBlobShape _smallBlob;
        private readonly CompositionBlobShape _mediumBlob;
        private readonly CompositionBlobShape _largeBlob;

        private readonly CompositionRadialGradientBrush _radial;

        private readonly CompositionVSync _vsync = new(30);

        private readonly float _maxLevel;

        private float _presentationAudioLevel;
        private float _audioLevel;

        private bool _animating;

        public CompositionVoiceBlobVisual(UIElement element, float width, float height, float maxLevel)
        {
            _maxLevel = maxLevel;

            var compositor = BootStrapper.Current.Compositor;
            var owner = ElementCompositionPreview.GetElementVisual(element);

            var size = new Vector2(width, height);
            var halfSize = size / 2;

            owner.CenterPoint = new Vector3(halfSize, 0);

            var gradient = compositor.CreateRectangleGeometry();
            var small = compositor.CreateEllipseGeometry();
            var medium = compositor.CreatePathGeometry();
            var large = compositor.CreatePathGeometry();

            gradient.Size = size;
            small.Radius = new Vector2(96 / 2);

            var temp = compositor.CreateRadialGradientBrush();
            temp.EllipseCenter = new Vector2(0.5f);
            temp.EllipseRadius = new Vector2(0.5f);
            temp.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(0x77, 0xFF, 0xFF, 0xFF)));
            temp.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF)));

            var gradientShape = compositor.CreateSpriteShape(gradient);
            gradientShape.FillBrush = temp;
            gradientShape.CenterPoint = halfSize;

            var smallShape = compositor.CreateSpriteShape(small);
            smallShape.Offset = halfSize;
            smallShape.FillBrush = compositor.CreateColorBrush(Color.FromArgb(0x77, 0xFF, 0xFF, 0xFF));

            var mediumShape = compositor.CreateSpriteShape(medium);
            mediumShape.Offset = halfSize;

            var largeShape = compositor.CreateSpriteShape(large);
            largeShape.Offset = halfSize;

            var visual = compositor.CreateShapeVisual();
            visual.Size = size;
            visual.CenterPoint = new Vector3(halfSize, 0);

            visual.Shapes.Add(gradientShape);
            visual.Shapes.Add(mediumShape);
            visual.Shapes.Add(largeShape);
            visual.Shapes.Add(smallShape);

            _smallBlob = new CompositionBlobShape(gradientShape, size, 8, 0.1f, 0.5f, 0.2f, 0.6f, 0.87f, 1.0f);
            _mediumBlob = new CompositionBlobShape(mediumShape, new Vector2(192), 8, 1, 1, 0.9f, 4, 0.69f, 0.87f);
            _largeBlob = new CompositionBlobShape(largeShape, new Vector2(192), 8, 1, 1, 0.9f, 4, 0.71f, 1.0f);

            _mediumBlob.FillColor = Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF); // 0x4D 0.3 alpha
            _largeBlob.FillColor = Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF); // 0x26 0.15 alpha

            var surfaceBrush = compositor.CreateRedirectBrush(visual, Vector2.Zero, visual.Size);

            _radial = compositor.CreateRadialGradientBrush();
            //radial.CenterPoint = new Vector2(0.5f, 0.0f);
            _radial.EllipseCenter = new Vector2(300, 0);
            _radial.EllipseRadius = new Vector2(MathF.Sqrt(200 * 200 + 200 * 200));
            _radial.MappingMode = CompositionMappingMode.Absolute;
            _radial.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Red));
            _radial.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.Blue));

            CompositionMaskBrush maskBrush = compositor.CreateMaskBrush();
            maskBrush.Source = _radial; // Set source to content that is to be masked 
            maskBrush.Mask = surfaceBrush; // Set mask to content that is the opacity mask 

            _visual = compositor.CreateSpriteVisual();
            _visual.Size = new Vector2(width, height);
            _visual.CenterPoint = new Vector3(halfSize, 0);
            _visual.Brush = maskBrush;

            ElementCompositionPreview.SetElementChildVisual(element, _visual);
        }

        private void OnRendering(object sender, object e)
        {
            _presentationAudioLevel = _presentationAudioLevel * 0.9f + _audioLevel * 0.1f;

            _smallBlob.Level = _presentationAudioLevel;
            _mediumBlob.Level = _presentationAudioLevel;
            _largeBlob.Level = _presentationAudioLevel;
        }

        public void SetColorStops(params uint[] colorStops)
        {
            _radial.ColorStops.Clear();

            for (int i = 0; i < colorStops.Length; i++)
            {
                _radial.ColorStops.Add(_radial.Compositor.CreateColorGradientStop(i / (colorStops.Length - 1f), ColorEx.FromHex(colorStops[i])));
            }
        }

        public Vector3 Scale
        {
            get => _visual.Scale;
            set => _visual.Scale = value;
        }

        public void UpdateLevel(float level)
        {
            UpdateLevel(level, immediately: false);
        }

        public void UpdateLevel(float level, bool immediately = false)
        {
            var normalizedLevel = MathF.Min(1, MathF.Max(level / _maxLevel, 0));

            _smallBlob.UpdateSpeedLevel(normalizedLevel);
            _mediumBlob.UpdateSpeedLevel(normalizedLevel);
            _largeBlob.UpdateSpeedLevel(normalizedLevel);

            _audioLevel = normalizedLevel;

            if (immediately)
            {
                _presentationAudioLevel = normalizedLevel;
            }
        }

        public void StartAnimating()
        {
            StartAnimating(false);
        }

        public void StartAnimating(bool immediately = false)
        {
            if (_animating)
            {
                return;
            }

            _animating = true;

            //if (!immediately)
            //{
            //    _mediumBlob.layer.animateScale(from: 0.75, to: 1, duration: 0.35, removeOnCompletion: false);
            //    _largeBlob.layer.animateScale(from: 0.75, to: 1, duration: 0.35, removeOnCompletion: false);
            //}
            //else
            //{
            //    _mediumBlob.layer.removeAllAnimations();
            //    _largeBlob.layer.removeAllAnimations();
            //}

            UpdateBlobsState();
            _vsync.Rendering += OnRendering;
        }

        public void StopAnimating()
        {
            StopAnimating(duration: 0.15);
        }

        public void StopAnimating(double duration)
        {
            if (!_animating)
            {
                return;
            }

            _animating = false;

            //_mediumBlob.layer.animateScale(from: 1.0, to: 0.75, duration: duration, removeOnCompletion: false);
            //_largeBlob.layer.animateScale(from: 1.0, to: 0.75, duration: duration, removeOnCompletion: false);

            UpdateBlobsState();
            _vsync.Rendering -= OnRendering;
        }

        private void UpdateBlobsState()
        {
            if (_animating)
            {
                //_smallBlob.StartAnimating();
                _mediumBlob.StartAnimating();
                _largeBlob.StartAnimating();
            }
            else
            {
                //_smallBlob.StopAnimating();
                _mediumBlob.StopAnimating();
                _largeBlob.StopAnimating();
            }
        }

        public void Clear()
        {
            _mediumBlob.Clear();
            _largeBlob.Clear();
        }
    }
}
