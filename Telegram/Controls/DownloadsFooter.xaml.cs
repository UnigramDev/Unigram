//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public sealed partial class DownloadsFooter : HyperlinkButton
    {
        private const float ShadowDepth = 32;

        public DownloadsFooter()
        {
            InitializeComponent();
            ElementCompositionPreview.SetIsTranslationEnabled(this, true);

            ActualThemeChanged += OnActualThemeChanged;
        }

        public void UpdateFileDownloads(UpdateFileDownloads update)
        {
            if (update.TotalSize > 0)
            {
                ShowHide(true);

                Icon.Progress = Math.Max((double)update.DownloadedSize / update.TotalSize, 0.05);
                Count.Text = Locale.Declension(Strings.R.Files, update.TotalCount);

                if (update.DownloadedSize < update.TotalSize)
                {
                    Size.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(update.DownloadedSize, update.TotalSize), FileSizeConverter.Convert(update.TotalSize));
                }
                else
                {
                    Size.Text = FileSizeConverter.Convert(update.TotalSize);
                }
            }
            else
            {
                ShowHide(false);
            }
        }

        private bool _collapsed = true;

        public void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            _expand = show;
            Visibility = Visibility.Visible;

            var parent = ElementComposition.GetElementVisual(this);
            var visual = ElementComposition.GetElementVisual(this);
            var insets = ThemeShadowEx.GetInsets(ShadowDepth);
            visual.Clip = visual.Compositor.CreateInsetClip((float)-insets.Left, (float)-insets.Top, (float)-insets.Right, 0);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_collapsed)
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, -40 - (float)insets.Bottom);
            clip.InsertKeyFrame(show ? 1 : 0, (float)-insets.Bottom);
            clip.Duration = Constants.SoftAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.SoftAnimation;

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, new Vector3(0.5f, 0.5f, 1));
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = Constants.SoftAnimation;

            visual.Clip.StartAnimation("BottomInset", clip);
            parent.StartAnimation("Translation", offset);
            parent.StartAnimation("Scale", scale);

            batch.End();
        }

        #region Resize

        // The card is drawn at an animated width, which only composition follows: the layout jumps
        // straight to the new one. The acrylic background paints nothing past its own bounds and a
        // clip can only take away, so a shrinking card keeps the width it shrinks from until done.
        private float _width;
        private double _hold;
        private bool _expand;
        private bool _resize;

        private Visual _card;
        private SpriteVisual _shadow;
        private CompositionPropertySet _props;
        private CompositionGeometricClip _clip;
        private CompositionEasingFunction _easing;
        private CompositionScopedBatch _batch;

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            var width = (float)size.Width;

            if (width != _width || _expand)
            {
                if (_width == 0)
                {
                    _expand = true;
                }
                else if (!_expand)
                {
                    _hold = Math.Max(_hold, _width);
                }

                _width = width;
                _resize = true;
            }

            return new Size(Math.Max(size.Width, _hold), size.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ElementComposition.GetElementVisual(this).CenterPoint = new Vector3((float)finalSize.Width / 2, (float)finalSize.Height / 2, 0);

            if (_props == null && _resize)
            {
                CreateResize();
            }

            if (_props != null)
            {
                _props.InsertScalar("Layout", (float)finalSize.Width);
                _props.InsertScalar("Height", (float)finalSize.Height);
            }

            // Started here rather than in MeasureOverride: the expressions have to see this layout
            // before the animation does, or the first frame is drawn against the previous one.
            if (_resize)
            {
                _resize = false;
                Resize();
            }

            return base.ArrangeOverride(finalSize);
        }

        private void Resize()
        {
            var expand = _expand;
            _expand = false;

            _card.Clip = _clip;

            var compositor = _card.Compositor;

            var animation = compositor.CreateScalarKeyFrameAnimation();

            if (expand)
            {
                // Shown as a circle around the icon, which grows to fit the text once it has slid in.
                // The arranged height, not the measured one: MinHeight is applied outside MeasureOverride.
                animation.InsertExpressionKeyFrame(0, "p.Height");
                animation.SetReferenceParameter("p", _props);
                animation.DelayTime = Constants.SoftAnimation;
                animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            }
            else
            {
                animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            }

            animation.InsertKeyFrame(1, _width, _easing ??= compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1)));
            animation.Duration = Constants.SoftAnimation;

            if (_batch != null)
            {
                _batch.Completed -= OnResizeCompleted;
            }

            _batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _batch.Completed += OnResizeCompleted;

            _props.StartAnimation("Width", animation);
            _batch.End();
        }

        private void OnResizeCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            _batch.Completed -= OnResizeCompleted;
            _batch = null;

            _card.Clip = null;

            if (_hold > 0)
            {
                _hold = 0;
                InvalidateMeasure();
            }
        }

        private void CreateResize()
        {
            _card = ElementComposition.GetElementVisual(Card);

            var compositor = _card.Compositor;

            _props = compositor.CreatePropertySet();
            _props.InsertScalar("Width", _width);
            _props.InsertScalar("Layout", _width);
            _props.InsertScalar("Height", 0);

            var geometry = compositor.CreateRoundedRectangleGeometry();
            geometry.CornerRadius = new Vector2(20);

            StartExpression(geometry, "Offset", "Vector2((p.Layout - p.Width) / 2, 0)");
            StartExpression(geometry, "Size", "Vector2(p.Width, p.Height)");

            _clip = compositor.CreateGeometricClip(geometry);

            ElementCompositionPreview.SetIsTranslationEnabled(CardContent, true);
            StartExpression(ElementComposition.GetElementVisual(CardContent), "Translation", "Vector3((p.Layout - p.Width) / 2, 0, 0)");

            // The clip would cut the stroke off the card's ends, so it is drawn in two halves that
            // follow the edges instead: the left half of one outline, the right half of another.
            ElementCompositionPreview.SetIsTranslationEnabled(CardStrokeLeft, true);
            ElementCompositionPreview.SetIsTranslationEnabled(CardStrokeRight, true);

            var strokeLeft = ElementComposition.GetElementVisual(CardStrokeLeft);
            strokeLeft.Clip = compositor.CreateInsetClip();
            StartExpression(strokeLeft.Clip, "RightInset", "p.Layout - p.Width / 2");
            StartExpression(strokeLeft, "Translation", "Vector3((p.Layout - p.Width) / 2, 0, 0)");

            var strokeRight = ElementComposition.GetElementVisual(CardStrokeRight);
            strokeRight.Clip = compositor.CreateInsetClip();
            StartExpression(strokeRight.Clip, "LeftInset", "p.Layout - p.Width / 2");
            StartExpression(strokeRight, "Translation", "Vector3((p.Width - p.Layout) / 2, 0, 0)");

            // Hosted here rather than through ThemeShadowEx, which sizes its sprite to the layout:
            // scaling that down to the card's width squashes the rounded ends of the nine grid.
            var insets = ThemeShadowEx.GetInsets(ShadowDepth);
            _props.InsertVector4("Insets", new Vector4((float)insets.Left, (float)insets.Top, (float)insets.Right, (float)insets.Bottom));

            _shadow = compositor.CreateSpriteVisual();
            _shadow.Brush = ThemeShadowEx.CreateBrush(compositor, ShadowDepth, ActualTheme, 20, 20);

            StartExpression(_shadow, "Offset", "Vector3((p.Layout - p.Width) / 2 - p.Insets.X, -p.Insets.Y, 0)");
            StartExpression(_shadow, "Size", "Vector2(p.Width + p.Insets.X + p.Insets.Z, p.Height + p.Insets.Y + p.Insets.W)");

            ElementComposition.SetElementChildVisual(CardShadow, _shadow);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_shadow != null)
            {
                _shadow.Brush = ThemeShadowEx.CreateBrush(_shadow.Compositor, ShadowDepth, ActualTheme, 20, 20);
            }
        }

        private void StartExpression(CompositionObject target, string property, string expression)
        {
            var animation = target.Compositor.CreateExpressionAnimation(expression);
            animation.SetReferenceParameter("p", _props);

            target.StartAnimation(property, animation);
        }

        #endregion
    }
}
