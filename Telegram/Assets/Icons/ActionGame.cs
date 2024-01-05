//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//       LottieGen version:
//           7.1.0+ge1fa92580f
//       
//       Command:
//           LottieGen -Language CSharp -Namespace Telegram.Assets.Icons -Public -WinUIVersion 2.7 -InputFile ActionGame.json
//       
//       Input file:
//           ActionGame.json (8007 bytes created 16:38+01:00 Dec 22 2021)
//       
//       LottieGen source:
//           http://aka.ms/Lottie
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
// ____________________________________
// |       Object stats       | Count |
// |__________________________|_______|
// | All CompositionObjects   |    78 |
// |--------------------------+-------|
// | Expression animators     |    10 |
// | KeyFrame animators       |    10 |
// | Reference parameters     |    10 |
// | Expression operations    |     0 |
// |--------------------------+-------|
// | Animated brushes         |     4 |
// | Animated gradient stops  |     - |
// | ExpressionAnimations     |     1 |
// | PathKeyFrameAnimations   |     - |
// |--------------------------+-------|
// | ContainerVisuals         |     1 |
// | ShapeVisuals             |     1 |
// |--------------------------+-------|
// | ContainerShapes          |     - |
// | CompositionSpriteShapes  |     6 |
// |--------------------------+-------|
// | Brushes                  |     5 |
// | Gradient stops           |     - |
// | CompositionVisualSurface |     - |
// ------------------------------------
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.Composition;

namespace Telegram.Assets.Icons
{
    // Name:        u_playing_game
    // Frame rate:  60 fps
    // Frame count: 40
    // Duration:    666.7 mS
    public sealed class ActionGame
        : Microsoft.UI.Xaml.Controls.IAnimatedVisualSource
        , Microsoft.UI.Xaml.Controls.IAnimatedVisualSource2
    {
        // Animation duration: 0.667 seconds.
        internal const long c_durationTicks = 6666666;
        internal readonly Color m_foreground;

        public ActionGame(Color foreground)
        {
            m_foreground = foreground;
        }

        public Microsoft.UI.Xaml.Controls.IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor)
        {
            object ignored = null;
            return TryCreateAnimatedVisual(compositor, out ignored);
        }

        public Microsoft.UI.Xaml.Controls.IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)
        {
            diagnostics = null;

            if (ActionGame_AnimatedVisual.IsRuntimeCompatible())
            {
                return
                    new ActionGame_AnimatedVisual(
                        compositor,
                        m_foreground
                        );
            }

            return null;
        }

        /// <summary>
        /// Gets the number of frames in the animation.
        /// </summary>
        public double FrameCount => 40d;

        /// <summary>
        /// Gets the frame rate of the animation.
        /// </summary>
        public double Framerate => 60d;

        /// <summary>
        /// Gets the duration of the animation.
        /// </summary>
        public TimeSpan Duration => TimeSpan.FromTicks(c_durationTicks);

        /// <summary>
        /// Converts a zero-based frame number to the corresponding progress value denoting the
        /// start of the frame.
        /// </summary>
        public double FrameToProgress(double frameNumber)
        {
            return frameNumber / 40d;
        }

        /// <summary>
        /// Returns a map from marker names to corresponding progress values.
        /// </summary>
        public IReadOnlyDictionary<string, double> Markers =>
            new Dictionary<string, double>
            {
            };

        /// <summary>
        /// Sets the color property with the given name, or does nothing if no such property
        /// exists.
        /// </summary>
        public void SetColorProperty(string propertyName, Color value)
        {
        }

        /// <summary>
        /// Sets the scalar property with the given name, or does nothing if no such property
        /// exists.
        /// </summary>
        public void SetScalarProperty(string propertyName, double value)
        {
        }

        sealed class ActionGame_AnimatedVisual : Microsoft.UI.Xaml.Controls.IAnimatedVisual
        {
            const long c_durationTicks = 6666666;
            readonly Compositor _c;
            readonly Color _f;
            readonly ExpressionAnimation _reusableExpressionAnimation;
            CompositionColorBrush _colorBrush_Black;
            CompositionPathGeometry _pathGeometry_0;
            CompositionPathGeometry _pathGeometry_1;
            ContainerVisual _root;
            CubicBezierEasingFunction _cubicBezierEasingFunction_0;
            ExpressionAnimation _rootProgress;
            StepEasingFunction _holdThenStepEasingFunction;
            StepEasingFunction _stepThenHoldEasingFunction;

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

            ColorKeyFrameAnimation CreateColorKeyFrameAnimation(float initialProgress, Color initialValue, CompositionEasingFunction initialEasingFunction)
            {
                var result = _c.CreateColorKeyFrameAnimation();
                result.Duration = TimeSpan.FromTicks(c_durationTicks);
                result.InterpolationColorSpace = CompositionColorSpace.Rgb;
                result.InsertKeyFrame(initialProgress, initialValue, initialEasingFunction);
                return result;
            }

            ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation(float initialProgress, float initialValue, CompositionEasingFunction initialEasingFunction)
            {
                var result = _c.CreateScalarKeyFrameAnimation();
                result.Duration = TimeSpan.FromTicks(c_durationTicks);
                result.InsertKeyFrame(initialProgress, initialValue, initialEasingFunction);
                return result;
            }

            Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation(float initialProgress, Vector2 initialValue, CompositionEasingFunction initialEasingFunction)
            {
                var result = _c.CreateVector2KeyFrameAnimation();
                result.Duration = TimeSpan.FromTicks(c_durationTicks);
                result.InsertKeyFrame(initialProgress, initialValue, initialEasingFunction);
                return result;
            }

            CanvasGeometry Geometry_0()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
                    builder.BeginFigure(new Vector2(-11.7159996F, 38.2840004F));
                    builder.AddLine(new Vector2(-40F, 10F));
                    builder.AddLine(new Vector2(-11.7159996F, -18.2840004F));
                    builder.AddCubicBezier(new Vector2(-19.5209999F, -26.0890007F), new Vector2(-29.75F, -29.9950008F), new Vector2(-39.9799995F, -30F));
                    builder.AddCubicBezier(new Vector2(-50.223999F, -30.0049992F), new Vector2(-60.4679985F, -26.1000004F), new Vector2(-68.2839966F, -18.2840004F));
                    builder.AddCubicBezier(new Vector2(-83.9049988F, -2.66300011F), new Vector2(-83.9049988F, 22.6630001F), new Vector2(-68.2839966F, 38.2840004F));
                    builder.AddCubicBezier(new Vector2(-60.4700012F, 46.0979996F), new Vector2(-50.2270012F, 50.0040016F), new Vector2(-39.9850006F, 50F));
                    builder.AddCubicBezier(new Vector2(-29.7530003F, 49.9959984F), new Vector2(-19.5230007F, 46.0909996F), new Vector2(-11.7159996F, 38.2840004F));
                    builder.EndFigure(CanvasFigureLoop.Closed);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return result;
            }

            CanvasGeometry Geometry_1()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
                    builder.BeginFigure(new Vector2(40F, 20F));
                    builder.AddCubicBezier(new Vector2(45.5229988F, 20F), new Vector2(50F, 15.5229998F), new Vector2(50F, 10F));
                    builder.AddCubicBezier(new Vector2(50F, 4.47700024F), new Vector2(45.5229988F, 0F), new Vector2(40F, 0F));
                    builder.AddCubicBezier(new Vector2(34.4770012F, 0F), new Vector2(30F, 4.47700024F), new Vector2(30F, 10F));
                    builder.AddCubicBezier(new Vector2(30F, 15.5229998F), new Vector2(34.4770012F, 20F), new Vector2(40F, 20F));
                    builder.EndFigure(CanvasFigureLoop.Closed);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return result;
            }

            // - - Shape tree root for layer: icon
            // - ShapeGroup: Group 3
            // Color
            ColorKeyFrameAnimation ColorAnimation_Black_to_Transparent_0()
            {
                // Frame 0.
                var result = CreateColorKeyFrameAnimation(0F, Color.FromArgb(0xFF, _f.R, _f.G, _f.B), StepThenHoldEasingFunction());
                // Frame 29.
                // Black
                result.InsertKeyFrame(0.725000024F, Color.FromArgb(0xFF, _f.R, _f.G, _f.B), _holdThenStepEasingFunction);
                // Frame 30.
                // Transparent
                result.InsertKeyFrame(0.75F, Color.FromArgb(0x00, _f.R, _f.G, _f.B), _cubicBezierEasingFunction_0);
                return result;
            }

            // - - Shape tree root for layer: icon
            // - ShapeGroup: Group 8
            // Color
            ColorKeyFrameAnimation ColorAnimation_Black_to_Transparent_1()
            {
                // Frame 0.
                var result = CreateColorKeyFrameAnimation(0F, Color.FromArgb(0xFF, _f.R, _f.G, _f.B), _stepThenHoldEasingFunction);
                // Frame 9.
                // Black
                result.InsertKeyFrame(0.224999994F, Color.FromArgb(0xFF, _f.R, _f.G, _f.B), _holdThenStepEasingFunction);
                // Frame 10.
                // Transparent
                result.InsertKeyFrame(0.25F, Color.FromArgb(0x00, _f.R, _f.G, _f.B), _cubicBezierEasingFunction_0);
                return result;
            }

            // - - Shape tree root for layer: icon
            // - ShapeGroup: Group 9
            // Color
            ColorKeyFrameAnimation ColorAnimation_Transparent_to_Black_0()
            {
                // Frame 0.
                var result = CreateColorKeyFrameAnimation(0F, Color.FromArgb(0x00, _f.R, _f.G, _f.B), _holdThenStepEasingFunction);
                // Frame 10.
                // Black
                result.InsertKeyFrame(0.25F, Color.FromArgb(0xFF, _f.R, _f.G, _f.B), _cubicBezierEasingFunction_0);
                return result;
            }

            // - - Shape tree root for layer: icon
            // - ShapeGroup: Group 10
            // Color
            ColorKeyFrameAnimation ColorAnimation_Transparent_to_Black_1()
            {
                // Frame 0.
                var result = CreateColorKeyFrameAnimation(0F, Color.FromArgb(0x00, _f.R, _f.G, _f.B), _stepThenHoldEasingFunction);
                // Frame 20.
                // Transparent
                result.InsertKeyFrame(0.5F, Color.FromArgb(0x00, _f.R, _f.G, _f.B), _holdThenStepEasingFunction);
                // Frame 30.
                // Black
                result.InsertKeyFrame(0.75F, Color.FromArgb(0xFF, _f.R, _f.G, _f.B), _cubicBezierEasingFunction_0);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 3
            CompositionColorBrush AnimatedColorBrush_Black_to_Transparent_0()
            {
                var result = _c.CreateColorBrush();
                StartProgressBoundAnimation(result, "Color", ColorAnimation_Black_to_Transparent_0(), _rootProgress);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 8
            CompositionColorBrush AnimatedColorBrush_Black_to_Transparent_1()
            {
                var result = _c.CreateColorBrush();
                StartProgressBoundAnimation(result, "Color", ColorAnimation_Black_to_Transparent_1(), _rootProgress);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 9
            CompositionColorBrush AnimatedColorBrush_Transparent_to_Black_0()
            {
                var result = _c.CreateColorBrush();
                StartProgressBoundAnimation(result, "Color", ColorAnimation_Transparent_to_Black_0(), _rootProgress);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 10
            CompositionColorBrush AnimatedColorBrush_Transparent_to_Black_1()
            {
                var result = _c.CreateColorBrush();
                StartProgressBoundAnimation(result, "Color", ColorAnimation_Transparent_to_Black_1(), _rootProgress);
                return result;
            }

            CompositionColorBrush ColorBrush_Black()
            {
                return _colorBrush_Black = _c.CreateColorBrush(Color.FromArgb(0xFF, _f.R, _f.G, _f.B));
            }

            CompositionPathGeometry PathGeometry_0()
            {
                return _pathGeometry_0 = _c.CreatePathGeometry(new CompositionPath(Geometry_0()));
            }

            CompositionPathGeometry PathGeometry_1()
            {
                return _pathGeometry_1 = _c.CreatePathGeometry(new CompositionPath(Geometry_1()));
            }

            // Shape tree root for layer: icon
            // Path 1
            CompositionSpriteShape SpriteShape_0()
            {
                var result = _c.CreateSpriteShape(PathGeometry_0());
                result.CenterPoint = new Vector2(-40F, 10F);
                result.Offset = new Vector2(100F, 100F);
                result.FillBrush = ColorBrush_Black();
                StartProgressBoundAnimation(result, "RotationAngleInDegrees", RotationAngleInDegreesScalarAnimation_0_to_0_0(), RootProgress());
                return result;
            }

            // Shape tree root for layer: icon
            // Path 1
            CompositionSpriteShape SpriteShape_1()
            {
                var result = _c.CreateSpriteShape(_pathGeometry_0);
                result.CenterPoint = new Vector2(-40F, 10F);
                result.Offset = new Vector2(100F, 100F);
                result.FillBrush = _colorBrush_Black;
                StartProgressBoundAnimation(result, "RotationAngleInDegrees", RotationAngleInDegreesScalarAnimation_0_to_0_1(), _rootProgress);
                return result;
            }

            // Shape tree root for layer: icon
            // Path 1
            CompositionSpriteShape SpriteShape_2()
            {
                var result = _c.CreateSpriteShape(PathGeometry_1());
                result.FillBrush = AnimatedColorBrush_Black_to_Transparent_0();
                StartProgressBoundAnimation(result, "Offset", OffsetVector2Animation_0(), _rootProgress);
                return result;
            }

            // Shape tree root for layer: icon
            // Path 1
            CompositionSpriteShape SpriteShape_3()
            {
                var result = _c.CreateSpriteShape(_pathGeometry_1);
                result.FillBrush = AnimatedColorBrush_Transparent_to_Black_0();
                StartProgressBoundAnimation(result, "Offset", OffsetVector2Animation_1(), _rootProgress);
                return result;
            }

            // Shape tree root for layer: icon
            // Path 1
            CompositionSpriteShape SpriteShape_4()
            {
                var result = _c.CreateSpriteShape(_pathGeometry_1);
                result.FillBrush = AnimatedColorBrush_Transparent_to_Black_1();
                StartProgressBoundAnimation(result, "Offset", OffsetVector2Animation_2(), _rootProgress);
                return result;
            }

            // Shape tree root for layer: icon
            // Path 1
            CompositionSpriteShape SpriteShape_5()
            {
                var result = _c.CreateSpriteShape(_pathGeometry_1);
                result.FillBrush = AnimatedColorBrush_Black_to_Transparent_1();
                StartProgressBoundAnimation(result, "Offset", OffsetVector2Animation_3(), _rootProgress);
                return result;
            }

            // The root of the composition.
            ContainerVisual Root()
            {
                var result = _root = _c.CreateContainerVisual();
                var propertySet = result.Properties;
                propertySet.InsertScalar("Progress", 0F);
                // Shape tree root for layer: icon
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

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 6
            // Rotation
            ScalarKeyFrameAnimation RotationAngleInDegreesScalarAnimation_0_to_0_0()
            {
                // Frame 0.
                var result = CreateScalarKeyFrameAnimation(0F, 0F, HoldThenStepEasingFunction());
                // Frame 8.
                result.InsertKeyFrame(0.200000003F, 46F, CubicBezierEasingFunction_0());
                // Frame 10.
                result.InsertKeyFrame(0.25F, 46F, _cubicBezierEasingFunction_0);
                // Frame 18.
                result.InsertKeyFrame(0.449999988F, 0F, _cubicBezierEasingFunction_0);
                // Frame 20.
                result.InsertKeyFrame(0.5F, 0F, _cubicBezierEasingFunction_0);
                // Frame 28.
                result.InsertKeyFrame(0.699999988F, 46F, _cubicBezierEasingFunction_0);
                // Frame 30.
                result.InsertKeyFrame(0.75F, 46F, _cubicBezierEasingFunction_0);
                // Frame 38.
                result.InsertKeyFrame(0.949999988F, 0F, _cubicBezierEasingFunction_0);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 7
            // Rotation
            ScalarKeyFrameAnimation RotationAngleInDegreesScalarAnimation_0_to_0_1()
            {
                // Frame 0.
                var result = CreateScalarKeyFrameAnimation(0F, 0F, _holdThenStepEasingFunction);
                // Frame 8.
                result.InsertKeyFrame(0.200000003F, -45F, _cubicBezierEasingFunction_0);
                // Frame 10.
                result.InsertKeyFrame(0.25F, -45F, _cubicBezierEasingFunction_0);
                // Frame 18.
                result.InsertKeyFrame(0.449999988F, 0F, _cubicBezierEasingFunction_0);
                // Frame 20.
                result.InsertKeyFrame(0.5F, 0F, _cubicBezierEasingFunction_0);
                // Frame 28.
                result.InsertKeyFrame(0.699999988F, -45F, _cubicBezierEasingFunction_0);
                // Frame 30.
                result.InsertKeyFrame(0.75F, -45F, _cubicBezierEasingFunction_0);
                // Frame 38.
                result.InsertKeyFrame(0.949999988F, 0F, _cubicBezierEasingFunction_0);
                return result;
            }

            // Shape tree root for layer: icon
            ShapeVisual ShapeVisual_0()
            {
                var result = _c.CreateShapeVisual();
                result.Size = new Vector2(200F, 200F);
                var shapes = result.Shapes;
                // ShapeGroup: Group 6
                shapes.Add(SpriteShape_0());
                // ShapeGroup: Group 7
                shapes.Add(SpriteShape_1());
                // ShapeGroup: Group 3
                shapes.Add(SpriteShape_2());
                // ShapeGroup: Group 9
                shapes.Add(SpriteShape_3());
                // ShapeGroup: Group 10
                shapes.Add(SpriteShape_4());
                // ShapeGroup: Group 8
                shapes.Add(SpriteShape_5());
                return result;
            }

            StepEasingFunction HoldThenStepEasingFunction()
            {
                var result = _holdThenStepEasingFunction = _c.CreateStepEasingFunction();
                result.IsFinalStepSingleFrame = true;
                return result;
            }

            StepEasingFunction StepThenHoldEasingFunction()
            {
                var result = _stepThenHoldEasingFunction = _c.CreateStepEasingFunction();
                result.IsInitialStepSingleFrame = true;
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 3
            // Offset
            Vector2KeyFrameAnimation OffsetVector2Animation_0()
            {
                // Frame 0.
                var result = CreateVector2KeyFrameAnimation(0F, new Vector2(100F, 100F), _holdThenStepEasingFunction);
                // Frame 10.
                result.InsertKeyFrame(0.25F, new Vector2(75F, 100F), _cubicBezierEasingFunction_0);
                // Frame 20.
                result.InsertKeyFrame(0.5F, new Vector2(50F, 100F), _cubicBezierEasingFunction_0);
                // Frame 30.
                result.InsertKeyFrame(0.75F, new Vector2(25F, 100F), _cubicBezierEasingFunction_0);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 9
            // Offset
            Vector2KeyFrameAnimation OffsetVector2Animation_1()
            {
                // Frame 0.
                var result = CreateVector2KeyFrameAnimation(0F, new Vector2(150F, 100F), _holdThenStepEasingFunction);
                // Frame 10.
                result.InsertKeyFrame(0.25F, new Vector2(125F, 100F), _cubicBezierEasingFunction_0);
                // Frame 20.
                result.InsertKeyFrame(0.5F, new Vector2(100F, 100F), _cubicBezierEasingFunction_0);
                // Frame 30.
                result.InsertKeyFrame(0.75F, new Vector2(75F, 100F), _cubicBezierEasingFunction_0);
                // Frame 40.
                result.InsertKeyFrame(1F, new Vector2(50F, 100F), _cubicBezierEasingFunction_0);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 10
            // Offset
            Vector2KeyFrameAnimation OffsetVector2Animation_2()
            {
                // Frame 0.
                var result = CreateVector2KeyFrameAnimation(0F, new Vector2(150F, 100F), _stepThenHoldEasingFunction);
                // Frame 20.
                result.InsertKeyFrame(0.5F, new Vector2(150F, 100F), _holdThenStepEasingFunction);
                // Frame 30.
                result.InsertKeyFrame(0.75F, new Vector2(125F, 100F), _cubicBezierEasingFunction_0);
                // Frame 40.
                result.InsertKeyFrame(1F, new Vector2(100F, 100F), _cubicBezierEasingFunction_0);
                return result;
            }

            // - Shape tree root for layer: icon
            // ShapeGroup: Group 8
            // Offset
            Vector2KeyFrameAnimation OffsetVector2Animation_3()
            {
                // Frame 0.
                var result = CreateVector2KeyFrameAnimation(0F, new Vector2(50F, 100F), _holdThenStepEasingFunction);
                // Frame 10.
                result.InsertKeyFrame(0.25F, new Vector2(25F, 100F), _cubicBezierEasingFunction_0);
                return result;
            }

            internal ActionGame_AnimatedVisual(
                Compositor compositor,
                Color foreground
                )
            {
                _c = compositor;
                _f = foreground;
                _reusableExpressionAnimation = compositor.CreateExpressionAnimation();
                Root();
            }

            public Visual RootVisual => _root;
            public TimeSpan Duration => TimeSpan.FromTicks(c_durationTicks);
            public Vector2 Size => new Vector2(200F, 200F);
            void IDisposable.Dispose() => _root?.Dispose();

            internal static bool IsRuntimeCompatible()
            {
                return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7);
            }
        }
    }
}
