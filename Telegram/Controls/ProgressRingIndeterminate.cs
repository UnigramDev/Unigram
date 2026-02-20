//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Numerics;
using Telegram.Composition;
using Telegram.Native.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class ProgressRingIndeterminate : ControlEx
    {
        private CompositionPropertySet _themeProperties;
        private CompositionPropertySet _props;

        private IAnimatedVisual _visual;

        private Color _themeBackground = Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3);
        private Color _themeForeground = Color.FromArgb(0xFF, 0x00, 0x78, 0xD7);
        private float _themeStrokeThickness = 1.5f;

        public ProgressRingIndeterminate()
        {
            DefaultStyleKey = typeof(ProgressRingIndeterminate);
        }

        protected override void OnLoaded()
        {
            _fillBrush?.Register();
            _strokeBrush?.Register();
        }

        protected override void OnUnloaded()
        {
            _fillBrush?.Unregister();
            _strokeBrush?.Unregister();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_visual == null)
            {
                _visual = GetVisual(Window.Current.Compositor, out _props);
                ElementCompositionPreview.SetElementChildVisual(this, _visual.RootVisual);

                var linearEasing = _props.Compositor.CreateLinearEasingFunction();
                var animation = _props.Compositor.CreateScalarKeyFrameAnimation();
                animation.Duration = _visual.Duration;
                animation.InsertKeyFrame(0, 0, linearEasing);
                animation.InsertKeyFrame(1, 1, linearEasing);
                animation.IterationBehavior = AnimationIterationBehavior.Forever;

                _props.StartAnimation("Progress", animation);
            }

            var newSize = availableSize.ToVector2();

            _visual.RootVisual.Size = newSize;
            _themeProperties.InsertVector2("Center", newSize / 2);
            _themeProperties.InsertVector2("Radius", newSize / 2 - new Vector2(_themeStrokeThickness / 2));

            return base.MeasureOverride(availableSize);
        }

        private IAnimatedVisual GetVisual(Compositor compositor, out CompositionPropertySet properties)
        {
            var visual = TryCreateAnimatedVisual(compositor, out _);

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 0.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 0.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            return visual;
        }

        public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)
        {
            var _ = EnsureThemeProperties(compositor);
            diagnostics = null;

            return new AnimatedVisual(compositor, _themeProperties);
        }

        CompositionPropertySet EnsureThemeProperties(Compositor compositor)
        {
            if (_themeProperties == null)
            {
                _themeProperties = compositor.CreatePropertySet();
                _themeProperties.InsertVector4("Background", CompositionPropertySetColorSource.ColorAsVector4(_themeBackground));
                _themeProperties.InsertVector4("Foreground", CompositionPropertySetColorSource.ColorAsVector4(_themeForeground));
                _themeProperties.InsertVector2("Center", new Vector2(7.0f));
                _themeProperties.InsertVector2("Radius", new Vector2(7.0f));
                _themeProperties.InsertScalar("StrokeThickness", _themeStrokeThickness);

                _fillBrush = new CompositionPropertySetColorSource(Fill, _themeProperties, "Background", IsConnected);
                _strokeBrush = new CompositionPropertySetColorSource(Stroke, _themeProperties, "Foreground", IsConnected);
            }

            return _themeProperties;
        }

        CompositionPropertySet GetThemeProperties(Compositor compositor)
        {
            return EnsureThemeProperties(compositor);
        }

        public double StrokeThickness
        {
            get => (double)_themeStrokeThickness;
            set
            {
                _themeStrokeThickness = (float)value;
                if (_themeProperties != null)
                {
                    _themeProperties.InsertScalar("StrokeThickness", _themeStrokeThickness);
                    _themeProperties.InsertVector2("Radius", ActualSize / 2 - new Vector2(_themeStrokeThickness / 2));
                }
            }
        }

        #region Stroke

        private CompositionPropertySetColorSource _strokeBrush;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ProgressRingIndeterminate), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProgressRingIndeterminate)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _strokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        #region Fill

        private CompositionPropertySetColorSource _fillBrush;

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register("Fill", typeof(Brush), typeof(ProgressRingIndeterminate), new PropertyMetadata(null, OnFillChanged));

        private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProgressRingIndeterminate)d).OnFillChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnFillChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _fillBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        #region Visual

        partial class AnimatedVisual : IAnimatedVisual
        {
            static long c_durationTicks = 20000000;
            private Compositor _c;
            private ExpressionAnimation _reusableExpressionAnimation;
            private CompositionPropertySet _themeProperties;
            private CompositionColorBrush _themeColor_Foreground_0;
            private CompositionColorBrush _themeColor_Foreground_1;
            private ContainerVisual _root;
            private CubicBezierEasingFunction _cubicBezierEasingFunction_0;
            private ExpressionAnimation _rootProgress;
            private StepEasingFunction _holdThenStepEasingFunction;

            static void StartProgressBoundAnimation(
                CompositionObject target,
                string animatedPropertyName,
                CompositionAnimation animation,
                ExpressionAnimation controllerProgressExpression)
            {
                target.StartAnimation(animatedPropertyName, animation);
                var controller = target.TryGetAnimationController(animatedPropertyName);
                controller.Pause();
                controller.StartAnimation("Progress", controllerProgressExpression);
            }

            void BindProperty(
                CompositionObject target,
                string animatedPropertyName,
                string expression,
                string referenceParameterName,
                CompositionObject referencedObject)
            {
                _reusableExpressionAnimation.ClearAllParameters();
                _reusableExpressionAnimation.Expression = expression;
                _reusableExpressionAnimation.SetReferenceParameter(referenceParameterName, referencedObject);
                target.StartAnimation(animatedPropertyName, _reusableExpressionAnimation);
            }

            void BindProperty2(
                CompositionObject target,
                string animatedPropertyName,
                string expression,
                string referenceParameterName0,
                CompositionObject referencedObject0,
                string referenceParameterName1,
                CompositionObject referencedObject1)
            {
                _reusableExpressionAnimation.ClearAllParameters();
                _reusableExpressionAnimation.Expression = expression;
                _reusableExpressionAnimation.SetReferenceParameter(referenceParameterName0, referencedObject0);
                _reusableExpressionAnimation.SetReferenceParameter(referenceParameterName1, referencedObject1);
                target.StartAnimation(animatedPropertyName, _reusableExpressionAnimation);
            }

            ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation(float initialProgress, float initialValue, CompositionEasingFunction initialEasingFunction)
            {
                var result = _c.CreateScalarKeyFrameAnimation();
                result.Duration = new TimeSpan(c_durationTicks);
                result.InsertKeyFrame(initialProgress, initialValue, initialEasingFunction);
                return result;
            }

            CompositionSpriteShape CreateSpriteShape(CompositionGeometry geometry, Matrix3x2 transformMatrix)
            {
                var result = _c.CreateSpriteShape(geometry);
                result.TransformMatrix = transformMatrix;
                return result;
            }

            // - Layer aggregator
            // Scale:5,5, Offset:<40, 40>
            // Color bound to theme property value: Background
            CompositionColorBrush ThemeColor_Background()
            {
                var result = _c.CreateColorBrush();
                BindProperty(result, "Color", "ColorRGB(_theme.Background.W*1,_theme.Background.X,_theme.Background.Y,_theme.Background.Z)", "_theme", _themeProperties);
                return result;
            }

            // - - Layer aggregator
            // -  Scale:5,5, Offset:<40, 40>
            // ShapeGroup: Ellipse B
            // Color bound to theme property value: Foreground
            CompositionColorBrush ThemeColor_Foreground_0()
            {
                var result = _themeColor_Foreground_0 = _c.CreateColorBrush();
                var propertySet = result.Properties;
                propertySet.InsertScalar("Opacity0", 0.0F);
                BindProperty2(result, "Color", "ColorRGB(_theme.Foreground.W*my.Opacity0,_theme.Foreground.X,_theme.Foreground.Y,_theme.Foreground.Z)", "_theme", _themeProperties, "my", propertySet);
                StartProgressBoundAnimation(propertySet, "Opacity0", Opacity0ScalarAnimation_0_to_1(), _rootProgress);
                return result;
            }

            // - - Layer aggregator
            // -  Scale:5,5, Offset:<40, 40>
            // ShapeGroup: Ellipse B
            // Color bound to theme property value: Foreground
            CompositionColorBrush ThemeColor_Foreground_1()
            {
                var result = _themeColor_Foreground_1 = _c.CreateColorBrush();
                var propertySet = result.Properties;
                propertySet.InsertScalar("Opacity0", 1.0F);
                BindProperty2(result, "Color", "ColorRGB(_theme.Foreground.W*my.Opacity0,_theme.Foreground.X,_theme.Foreground.Y,_theme.Foreground.Z)", "_theme", _themeProperties, "my", propertySet);
                StartProgressBoundAnimation(propertySet, "Opacity0", Opacity0ScalarAnimation_1_to_0(), _rootProgress);
                return result;
            }

            // Layer aggregator
            // Transforms for Radial
            CompositionContainerShape ContainerShape()
            {
                var result = _c.CreateContainerShape();
                // Offset:<40, 40>, Scale:<5, 5>
                //result.TransformMatrix = new Matrix3x2(5.0F, 0.0F, 0.0F, 5.0F, 40.0F, 40.0F);
                var shapes = result.Shapes;
                // ShapeGroup: Ellipse B
                shapes.Add(SpriteShape_1());
                // ShapeGroup: Ellipse B
                shapes.Add(SpriteShape_2());
                BindProperty(result, "TransformMatrix", "Matrix3x2.CreateTranslation(_theme.Center)", "_theme", _themeProperties);
                StartProgressBoundAnimation(result, "RotationAngleInDegrees", RotationAngleInDegreesScalarAnimation_0_to_900(), _rootProgress);
                return result;
            }

            // - Layer aggregator
            // Scale:5,5, Offset:<40, 40>
            // Ellipse Path.EllipseGeometry
            CompositionEllipseGeometry Ellipse_7_0()
            {
                var result = _c.CreateEllipseGeometry();
                result.Radius = new Vector2(7.0F, 7.0F);
                BindProperty(result, "Radius", "_theme.Radius", "_theme", _themeProperties);
                return result;
            }

            // - - Layer aggregator
            // -  Scale:5,5, Offset:<40, 40>
            // ShapeGroup: Ellipse B
            // Ellipse Path.EllipseGeometry
            CompositionEllipseGeometry Ellipse_7_1()
            {
                var result = _c.CreateEllipseGeometry();
                result.TrimEnd = 0.5F;
                result.Radius = new Vector2(7.0F, 7.0F);
                BindProperty(result, "Radius", "_theme.Radius", "_theme", _themeProperties);
                StartProgressBoundAnimation(result, "TrimStart", TrimStartScalarAnimation_0_to_0p5(), RootProgress());
                return result;
            }

            // - - Layer aggregator
            // -  Scale:5,5, Offset:<40, 40>
            // ShapeGroup: Ellipse B
            // Ellipse Path.EllipseGeometry
            CompositionEllipseGeometry Ellipse_7_2()
            {
                var result = _c.CreateEllipseGeometry();
                result.Radius = new Vector2(7.0F, 7.0F);
                BindProperty(result, "Radius", "_theme.Radius", "_theme", _themeProperties);
                StartProgressBoundAnimation(result, "TrimEnd", TrimEndScalarAnimation_0_to_0p5(), _rootProgress);
                return result;
            }

            // Layer aggregator
            // Ellipse Path
            CompositionSpriteShape SpriteShape_0()
            {
                // Offset:<40, 40>, Scale:<5, 5>
                var result = CreateSpriteShape(Ellipse_7_0(), Matrix3x2.Identity /*new Matrix3x2(5.0F, 0.0F, 0.0F, 5.0F, 40.0F, 40.0F)*/);
                result.StrokeBrush = ThemeColor_Background();
                result.StrokeDashCap = CompositionStrokeCap.Round;
                result.StrokeThickness = 1.5F;
                BindProperty(result, "TransformMatrix", "Matrix3x2.CreateTranslation(_theme.Center)", "_theme", _themeProperties);
                BindProperty(result, "StrokeThickness", "_theme.StrokeThickness", "_theme", _themeProperties);
                return result;
            }

            // - Layer aggregator
            // Scale:5,5, Offset:<40, 40>
            // Ellipse Path
            CompositionSpriteShape SpriteShape_1()
            {
                var result = _c.CreateSpriteShape(Ellipse_7_1());
                result.StrokeBrush = ThemeColor_Foreground_0();
                result.StrokeDashCap = CompositionStrokeCap.Round;
                result.StrokeStartCap = CompositionStrokeCap.Round;
                result.StrokeEndCap = CompositionStrokeCap.Round;
                result.StrokeThickness = 1.5F;
                BindProperty(result, "StrokeThickness", "_theme.StrokeThickness", "_theme", _themeProperties);
                return result;
            }

            // - Layer aggregator
            // Scale:5,5, Offset:<40, 40>
            // Ellipse Path
            CompositionSpriteShape SpriteShape_2()
            {
                var result = _c.CreateSpriteShape(Ellipse_7_2());
                result.StrokeBrush = ThemeColor_Foreground_1();
                result.StrokeDashCap = CompositionStrokeCap.Round;
                result.StrokeStartCap = CompositionStrokeCap.Round;
                result.StrokeEndCap = CompositionStrokeCap.Round;
                result.StrokeThickness = 1.5F;
                BindProperty(result, "StrokeThickness", "_theme.StrokeThickness", "_theme", _themeProperties);
                return result;
            }

            // The root of the composition.
            ContainerVisual Root()
            {
                var result = _root = _c.CreateContainerVisual();
                var propertySet = result.Properties;
                propertySet.InsertScalar("Progress", 0.0F);
                // Layer aggregator
                result.Children.InsertAtTop(ShapeVisual_0());
                return result;
            }

            CubicBezierEasingFunction CubicBezierEasingFunction_0()
            {
                return _cubicBezierEasingFunction_0 = _c.CreateCubicBezierEasingFunction(new Vector2(0.166999996F, 0.166999996F), new Vector2(0.833000004F, 0.833000004F));
            }

            ExpressionAnimation RootProgress()
            {
                var result = _rootProgress = _c.CreateExpressionAnimation("_.Progress");
                result.SetReferenceParameter("_", _root);
                return result;
            }

            // Opacity0
            ScalarKeyFrameAnimation Opacity0ScalarAnimation_0_to_1()
            {
                var result = CreateScalarKeyFrameAnimation(0.0F, 0.0F, _holdThenStepEasingFunction);
                result.InsertKeyFrame(0.5F, 1.0F, _holdThenStepEasingFunction);
                return result;
            }

            // Opacity0
            ScalarKeyFrameAnimation Opacity0ScalarAnimation_1_to_0()
            {
                var result = CreateScalarKeyFrameAnimation(0.0F, 1.0F, _holdThenStepEasingFunction);
                result.InsertKeyFrame(0.5F, 0.0F, _holdThenStepEasingFunction);
                return result;
            }

            // - Layer aggregator
            // Scale:5,5, Offset:<40, 40>
            // Rotation
            ScalarKeyFrameAnimation RotationAngleInDegreesScalarAnimation_0_to_900()
            {
                var result = CreateScalarKeyFrameAnimation(0.0F, 0.0F, _holdThenStepEasingFunction);
                result.InsertKeyFrame(0.5F, 450.0F, _cubicBezierEasingFunction_0);
                result.InsertKeyFrame(1.0F, 900.0F, _cubicBezierEasingFunction_0);
                return result;
            }

            // - - - Layer aggregator
            // - -  Scale:5,5, Offset:<40, 40>
            // - ShapeGroup: Ellipse B
            // Ellipse Path.EllipseGeometry
            // TrimEnd
            ScalarKeyFrameAnimation TrimEndScalarAnimation_0_to_0p5()
            {
                var result = CreateScalarKeyFrameAnimation(0.0F, 9.99999975E-05F, _holdThenStepEasingFunction);
                result.InsertKeyFrame(0.5F, 0.5F, _cubicBezierEasingFunction_0);
                return result;
            }

            // - - - Layer aggregator
            // - -  Scale:5,5, Offset:<40, 40>
            // - ShapeGroup: Ellipse B
            // Ellipse Path.EllipseGeometry
            // TrimStart
            ScalarKeyFrameAnimation TrimStartScalarAnimation_0_to_0p5()
            {
                var result = CreateScalarKeyFrameAnimation(0.0F, 0.0F, StepThenHoldEasingFunction());
                result.InsertKeyFrame(0.5F, 0.0F, HoldThenStepEasingFunction());
                result.InsertKeyFrame(1.0F, 0.5F, CubicBezierEasingFunction_0());
                return result;
            }

            // Layer aggregator
            ShapeVisual ShapeVisual_0()
            {
                var result = _c.CreateShapeVisual();
                result.RelativeSizeAdjustment = Vector2.One;
                //result.Size = new Vector2(80.0F, 80.0F);
                var shapes = result.Shapes;
                // Scale:5,5, Offset:<40, 40>
                shapes.Add(SpriteShape_0());
                // Scale:5,5, Offset:<40, 40>
                shapes.Add(ContainerShape());
                return result;
            }

            StepEasingFunction HoldThenStepEasingFunction()
            {
                var result = _holdThenStepEasingFunction = _c.CreateStepEasingFunction();
                result.IsFinalStepSingleFrame = true;
                return result;
            }

            // - - - - Layer aggregator
            // - - -  Scale:5,5, Offset:<40, 40>
            // - - ShapeGroup: Ellipse B
            // - Ellipse Path.EllipseGeometry
            // TrimStart
            StepEasingFunction StepThenHoldEasingFunction()
            {
                var result = _c.CreateStepEasingFunction();
                result.IsInitialStepSingleFrame = true;
                return result;
            }

            public AnimatedVisual(Compositor compositor, CompositionPropertySet themeProperties)
            {
                _c = compositor;
                _themeProperties = themeProperties;
                _reusableExpressionAnimation = compositor.CreateExpressionAnimation();
                var _ = Root();
            }

            public void Dispose()
            {
                _root?.Dispose();
            }

            public TimeSpan Duration => new(c_durationTicks);

            public Visual RootVisual => _root;

            public Vector2 Size => new(80, 80);
        }

        #endregion
    }
}
