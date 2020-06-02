using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;

namespace Unigram.Assets.Animations
{
    sealed class ChatArchivedAnimation : IAnimatedVisualSource
    {
        public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)
        {
            diagnostics = null;
            if (!IsRuntimeCompatible())
            {
                return null;
            }
            return new AnimatedVisual(compositor);
        }

        static bool IsRuntimeCompatible()
        {
            if (!Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.Composition.CompositionGeometricClip"))
            {
                return false;
            }
            return true;
        }

        sealed class AnimatedVisual : IAnimatedVisual
        {
            const long c_durationTicks = 7500000;
            readonly Compositor _c;
            readonly ExpressionAnimation _reusableExpressionAnimation;
            ContainerVisual _root;

            // contour
            CompositionColorBrush ColorBrush_Black()
            {
                return _c.CreateColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
            }

            // contour2+contour1
            CompositionColorBrush ColorBrush_White()
            {
                return _c.CreateColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            }

            // contour
            //   contour.PathGeometry
            //     TrimEnd
            CubicBezierEasingFunction CubicBezierEasingFunction()
            {
                return _c.CreateCubicBezierEasingFunction(new Vector2(0.815999985F, 0), new Vector2(0.833000004F, 0.833000004F));
            }

            CanvasGeometry Geometry_0()
            {
                var result = CanvasGeometry.CreateGroup(
                    null,
                    new CanvasGeometry[] { Geometry_1(), Geometry_2() },
                    CanvasFilledRegionDetermination.Winding);
                return result;
            }

            CanvasGeometry Geometry_1()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
                    builder.BeginFigure(new Vector2(7.58400011F, -7.5F));
                    builder.AddLine(new Vector2(6.17399979F, -8.90999985F));
                    builder.AddCubicBezier(new Vector2(5.79400015F, -9.28999996F), new Vector2(5.28399992F, -9.5F), new Vector2(4.75400019F, -9.5F));
                    builder.AddLine(new Vector2(-4.75600004F, -9.5F));
                    builder.AddCubicBezier(new Vector2(-5.28599977F, -9.5F), new Vector2(-5.796F, -9.28999996F), new Vector2(-6.17600012F, -8.90999985F));
                    builder.AddLine(new Vector2(-7.58599997F, -7.5F));
                    builder.AddLine(new Vector2(7.58400011F, -7.5F));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return result;
            }

            CanvasGeometry Geometry_2()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
                    builder.BeginFigure(new Vector2(-11, -4.5F));
                    builder.AddLine(new Vector2(-11, -5.26000023F));
                    builder.AddCubicBezier(new Vector2(-11, -6.32000017F), new Vector2(-10.5799999F, -7.34000015F), new Vector2(-9.82999992F, -8.09000015F));
                    builder.AddLine(new Vector2(-7.59000015F, -10.3299999F));
                    builder.AddCubicBezier(new Vector2(-6.84000015F, -11.0799999F), new Vector2(-5.82000017F, -11.5F), new Vector2(-4.76000023F, -11.5F));
                    builder.AddLine(new Vector2(4.76000023F, -11.5F));
                    builder.AddCubicBezier(new Vector2(5.82000017F, -11.5F), new Vector2(6.84000015F, -11.0799999F), new Vector2(7.59000015F, -10.3299999F));
                    builder.AddLine(new Vector2(9.82999992F, -8.09000015F));
                    builder.AddCubicBezier(new Vector2(10.5799999F, -7.34000015F), new Vector2(11, -6.32000017F), new Vector2(11, -5.26000023F));
                    builder.AddLine(new Vector2(11, 8.5F));
                    builder.AddCubicBezier(new Vector2(11, 10.1599998F), new Vector2(9.65999985F, 11.5F), new Vector2(8, 11.5F));
                    builder.AddLine(new Vector2(-8, 11.5F));
                    builder.AddCubicBezier(new Vector2(-9.65999985F, 11.5F), new Vector2(-11, 10.1599998F), new Vector2(-11, 8.5F));
                    builder.AddLine(new Vector2(-11, -4.5F));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return result;
            }

            CanvasGeometry Geometry_3()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.BeginFigure(new Vector2(-5, 0.5F));
                    builder.AddLine(new Vector2(-2, 3.5F));
                    builder.AddLine(new Vector2(5, -3.5F));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return result;
            }

            // contour
            //   contour.PathGeometry
            //     TrimEnd
            StepEasingFunction HoldThenStepEasingFunction()
            {
                var result = _c.CreateStepEasingFunction();
                result.IsFinalStepSingleFrame = true;
                return result;
            }

            // contour2+contour1
            // contour2+contour1.PathGeometry
            CompositionPathGeometry PathGeometry_0()
            {
                var result = _c.CreatePathGeometry(new CompositionPath(Geometry_0()));
                return result;
            }

            // contour
            // contour.PathGeometry
            CompositionPathGeometry PathGeometry_1()
            {
                var result = _c.CreatePathGeometry(new CompositionPath(Geometry_3()));
                result.StartAnimation("TrimEnd", TrimEndScalarAnimation_0_to_1());
                var controller = result.TryGetAnimationController("TrimEnd");
                controller.Pause();
                _reusableExpressionAnimation.ClearAllParameters();
                _reusableExpressionAnimation.Expression = "_.Progress";
                _reusableExpressionAnimation.SetReferenceParameter("_", _root);
                controller.StartAnimation("Progress", _reusableExpressionAnimation);
                return result;
            }

            // The root of the composition.
            ContainerVisual Root()
            {
                var result = _root = _c.CreateContainerVisual();
                var propertySet = result.Properties;
                propertySet.InsertScalar("Progress", 0);
                var children = result.Children;
                children.InsertAtTop(ShapeVisual());
                return result;
            }

            ShapeVisual ShapeVisual()
            {
                var result = _c.CreateShapeVisual();
                result.Size = new Vector2(36, 36);
                var shapes = result.Shapes;
                // contour2+contour1
                shapes.Add(SpriteShape_0());
                // contour
                shapes.Add(SpriteShape_1());
                return result;
            }

            // contour2+contour1
            CompositionSpriteShape SpriteShape_0()
            {
                var result = _c.CreateSpriteShape();
                result.TransformMatrix = new Matrix3x2(1.00002003F, 0, 0, 1.00002003F, 18, 17.5F);
                result.FillBrush = ColorBrush_White();
                result.Geometry = PathGeometry_0();
                return result;
            }

            // contour
            CompositionSpriteShape SpriteShape_1()
            {
                var result = _c.CreateSpriteShape();
                result.TransformMatrix = new Matrix3x2(1.00002003F, 0, 0, 1.00002003F, 18, 19.5F);
                result.Geometry = PathGeometry_1();
                result.StrokeBrush = ColorBrush_Black();
                result.StrokeDashCap = CompositionStrokeCap.Round;
                result.StrokeEndCap = CompositionStrokeCap.Round;
                result.StrokeLineJoin = CompositionStrokeLineJoin.Round;
                result.StrokeStartCap = CompositionStrokeCap.Round;
                result.StrokeMiterLimit = 4;
                result.StrokeThickness = 2;
                return result;
            }

            // contour
            //   contour.PathGeometry
            //     TrimEnd
            StepEasingFunction StepThenHoldEasingFunction()
            {
                var result = _c.CreateStepEasingFunction();
                result.IsInitialStepSingleFrame = true;
                return result;
            }

            // contour
            //   contour.PathGeometry
            // TrimEnd
            ScalarKeyFrameAnimation TrimEndScalarAnimation_0_to_1()
            {
                var result = _c.CreateScalarKeyFrameAnimation();
                result.Duration = TimeSpan.FromTicks(c_durationTicks);
                result.InsertKeyFrame(0, 0, StepThenHoldEasingFunction());
                result.InsertKeyFrame(0.111111112F, 0, HoldThenStepEasingFunction());
                result.InsertKeyFrame(0.444444448F, 1, CubicBezierEasingFunction());
                return result;
            }

            internal AnimatedVisual(Compositor compositor)
            {
                _c = compositor;
                _reusableExpressionAnimation = compositor.CreateExpressionAnimation();
                Root();
            }

            Visual IAnimatedVisual.RootVisual => _root;
            TimeSpan IAnimatedVisual.Duration => TimeSpan.FromTicks(c_durationTicks);
            Vector2 IAnimatedVisual.Size => new Vector2(36, 36);
            void IDisposable.Dispose() => _root?.Dispose();
        }
    }
}
