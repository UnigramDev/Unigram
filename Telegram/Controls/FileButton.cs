//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Converters;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public enum MessageContentState
    {
        None,
        Download,
        Downloading,
        Uploading,
        Confirm,
        Document,
        Photo,
        Animation,
        Ttl,
        Unlock,
        Play,
        Pause,
        Theme,
    }

    public class FileButton : GlyphHyperlinkButton
    {
        private Grid _root;

        private TextBlock _label1;
        private TextBlock _label2;

        private Visual _visual1;
        private Visual _visual2;

        private TextBlock _label;
        private Visual _visual;

        private ContainerVisual _container;

        private long _fileId;
        private MessageContentState _state;
        private double _enqueuedProgress;
        private bool _shouldEnqueueProgress;

        public FileButton()
        {
            DefaultStyleKey = typeof(FileButton);
        }

        public MessageContentState State => _state;

        public bool IsSmall { get; set; }

        protected override void OnApplyTemplate()
        {
            _root = GetTemplateChild("RootGrid") as Grid;

            _label1 = _label = GetTemplateChild("ContentPresenter1") as TextBlock;
            _label2 = GetTemplateChild("ContentPresenter2") as TextBlock;

            _visual1 = _visual = ElementCompositionPreview.GetElementVisual(_label1);
            _visual2 = ElementCompositionPreview.GetElementVisual(_label2);

            _label2.Text = string.Empty;

            _visual2.Opacity = 0;
            _visual2.Scale = new Vector3();
            _visual2.CenterPoint = new Vector3(10);

            _label1.Text = Glyph ?? string.Empty;

            _visual1.Opacity = 1;
            _visual1.Scale = new Vector3(1);
            _visual1.CenterPoint = new Vector3(10);

            _container = Window.Current.Compositor.CreateContainerVisual();
            _container.Size = new Vector2(48, 48);
            _container.CenterPoint = new Vector3(24, 24, 0);

            if (_state == MessageContentState.Download && !IsSmall)
            {
                SetDownloadGlyph(false, false);
            }

            ElementCompositionPreview.SetElementChildVisual(_root, _container);

            base.OnApplyTemplate();
        }

        #region Progress

        public double InternalProgress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("InternalProgress", typeof(double), typeof(FileButton), new PropertyMetadata(0.0));

        #endregion

        #region ProgressVisibility

        public Visibility ProgressVisibility
        {
            get => (Visibility)GetValue(ProgressVisibilityProperty);
            set => SetValue(ProgressVisibilityProperty, value);
        }

        public static readonly DependencyProperty ProgressVisibilityProperty =
            DependencyProperty.Register("ProgressVisibility", typeof(Visibility), typeof(FileButton), new PropertyMetadata(Visibility.Visible));

        #endregion


        //public void SetGlyph(string glyph, bool animate)
        //{
        //    OnGlyphChanged(glyph, Glyph, animate);
        //}
        public double Progress
        {
            set
            {
                if (_shouldEnqueueProgress)
                {
                    _enqueuedProgress = value;
                }
                else if (_state is MessageContentState.Downloading or MessageContentState.Uploading)
                {
                    InternalProgress = Math.Max(0.05, value);
                }
                else
                {
                    InternalProgress = value;
                }
            }
        }

        public void SetGlyph(int fileId, MessageContentState state)
        {
            if (fileId != _fileId)
            {
                _state = MessageContentState.None;
            }

            switch (state)
            {
                case MessageContentState.Download:
                    if (IsSmall)
                    {
                        OnGlyphChanged(IsSmall ? Icons.DownloadSmall : Icons.ArrowDownload, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionDownload);
                    }
                    else
                    {
                        SetDownloadGlyph(false, _state != state && _state != MessageContentState.None);
                    }
                    break;
                case MessageContentState.Downloading:
                    if (IsSmall || (_state != MessageContentState.Download && _state != MessageContentState.Downloading))
                    {
                        OnGlyphChanged(IsSmall ? Icons.CancelSmall : Icons.DismissFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionCancelDownload, false);
                    }
                    else if (_state != MessageContentState.Downloading)
                    {
                        SetDownloadGlyph(true, false);
                    }
                    break;
                case MessageContentState.Uploading:
                    OnGlyphChanged(IsSmall ? Icons.CancelSmall : Icons.DismissFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionCancelDownload);
                    break;
                case MessageContentState.Confirm:
                    OnGlyphChanged(Icons.CheckmarkFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionCancelDownload);
                    break;
                case MessageContentState.Document:
                    OnGlyphChanged(Icons.DocumentFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionOpenFile);
                    break;
                case MessageContentState.Animation:
                    OnGlyphChanged(Icons.Animation, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionPlay);
                    break;
                case MessageContentState.Photo:
                    OnGlyphChanged(Icons.ImageFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionOpenFile);
                    break;
                case MessageContentState.Play:
                    OnGlyphChanged(Icons.PlayFilled, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionPlay);
                    break;
                case MessageContentState.Pause:
                    OnGlyphChanged(Icons.PauseFilled, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionPause);
                    break;
                case MessageContentState.Ttl:
                    OnGlyphChanged(Icons.TtlFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionOpenFile);
                    break;
                case MessageContentState.Unlock:
                    OnGlyphChanged(Icons.LockClosedFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionOpenFile);
                    break;
                case MessageContentState.Theme:
                    OnGlyphChanged(Icons.ColorFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionOpenFile);
                    break;
            }

            _fileId = fileId;
            _state = state;
        }

        private void OnGlyphChanged(string newValue, string oldValue, bool animate, string automation, bool clearContainer = true)
        {
            //if (string.IsNullOrEmpty(oldValue) || string.IsNullOrEmpty(newValue))
            //{
            //    return;
            //}

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Glyph = newValue;
            AutomationProperties.SetName(this, automation);

            if (_visual == null || _label == null)
            {
                return;
            }

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var labelShow = _visual == _visual1 ? _label2 : _label1;
            var labelHide = _visual == _visual1 ? _label1 : _label2;

            if (animate)
            {
                var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                hide1.InsertKeyFrame(0, new Vector3(1));
                hide1.InsertKeyFrame(1, new Vector3(0));

                var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
                hide2.InsertKeyFrame(0, 1);
                hide2.InsertKeyFrame(1, 0);

                visualHide.StartAnimation("Scale", hide1);
                visualHide.StartAnimation("Opacity", hide2);
            }
            else
            {
                visualHide.Scale = new Vector3(0);
                visualHide.Opacity = 0;
            }

            labelShow.Text = newValue;

            if (animate)
            {
                var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(1, new Vector3(1));
                show1.InsertKeyFrame(0, new Vector3(0));

                var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
                show2.InsertKeyFrame(1, 1);
                show2.InsertKeyFrame(0, 0);

                visualShow.StartAnimation("Scale", show1);
                visualShow.StartAnimation("Opacity", show2);
            }
            else
            {
                visualShow.Scale = new Vector3(1);
                visualShow.Opacity = 1;
            }

            _visual = visualShow;
            _label = labelShow;

            if (clearContainer || !animate)
            {
                _container.Children.RemoveAll();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDownloadGlyph(bool downloading, bool animate)
        {
            try
            {
                SetDownloadGlyphInternal(downloading, animate);
            }
            catch
            {
                // Compositor.CreateSpriteShape can throw InvalidCastException
                // TODO: fallback to glyph icon?
            }
        }

        private void SetDownloadGlyphInternal(bool downloading, bool animate)
        {
            if (_container == null)
            {
                return;
            }

            var compositor = Window.Current.Compositor;
            var diameter = 48f; // min(bounds.size.width, bounds.size.height)
            var factor = diameter / 48f;


            var lineWidth = 2; //MathF.Max(1.6f, 2.25f * factor);

            //self.leftLine.lineWidth = lineWidth
            //self.rightLine.lineWidth = lineWidth
            //self.arrowBody.lineWidth = lineWidth


            var arrowHeadSize = 15.0f * factor;
            var arrowLength = 18.0f * factor;
            var arrowHeadOffset = 1.0f * factor;

            var leftLine = compositor.CreateLineGeometry();
            leftLine.Start = new Vector2(x: diameter / 2.0f, y: diameter / 2.0f + arrowLength / 2.0f + arrowHeadOffset);
            leftLine.End = new Vector2(x: diameter / 2.0f - arrowHeadSize / 2.0f, y: diameter / 2.0f + arrowLength / 2.0f - arrowHeadSize / 2.0f + arrowHeadOffset);

            var rightLine = compositor.CreateLineGeometry();
            rightLine.Start = new Vector2(x: diameter / 2.0f, y: diameter / 2.0f + arrowLength / 2.0f + arrowHeadOffset);
            rightLine.End = new Vector2(x: diameter / 2.0f + arrowHeadSize / 2.0f, y: diameter / 2.0f + arrowLength / 2.0f - arrowHeadSize / 2.0f + arrowHeadOffset);

            var leftShape = compositor.CreateSpriteShape(leftLine);
            leftShape.StrokeThickness = lineWidth;
            leftShape.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            leftShape.StrokeStartCap = CompositionStrokeCap.Round;
            leftShape.StrokeEndCap = CompositionStrokeCap.Round;

            var rightShape = compositor.CreateSpriteShape(rightLine);
            rightShape.StrokeThickness = lineWidth;
            rightShape.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            rightShape.StrokeStartCap = CompositionStrokeCap.Round;
            rightShape.StrokeEndCap = CompositionStrokeCap.Round;

            var arrowPath = compositor.CreatePathGeometry();
            //arrowPath.Path = svgPath("M4.2,31.1 C4.7,32.5 5.4,33.8 6.2,35 C12.1,44 24,44.7 24,33.3 L24,16 ", Vector2.One, Vector2.Zero);
            arrowPath.Path = GetArrowShape();
            arrowPath.TrimStart = 0.65f;

            var arrowShape = compositor.CreateSpriteShape(arrowPath);
            arrowShape.StrokeThickness = lineWidth;
            arrowShape.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            arrowShape.StrokeStartCap = CompositionStrokeCap.Round;
            arrowShape.StrokeEndCap = CompositionStrokeCap.Round;
            arrowShape.StrokeLineJoin = CompositionStrokeLineJoin.Round;

            var visual1 = compositor.CreateShapeVisual();
            visual1.Shapes.Add(leftShape);
            visual1.Shapes.Add(rightShape);
            visual1.Size = new Vector2(48, 48);

            var visual3 = compositor.CreateShapeVisual();
            var visual2 = compositor.CreateShapeVisual();
            visual3.Shapes.Add(arrowShape);
            visual3.Size = new Vector2(48, 48);
            //visual.Offset = new Vector3((48 - 20) / 2 + 4.5f, (48 - 20) / 2, 0);

            //_visual1.Opacity = 0;
            //_visual2.Opacity = 0;

            _container.Children.RemoveAll();
            _container.Children.InsertAtTop(visual1);
            _container.Children.InsertAtTop(visual2);
            _container.Children.InsertAtTop(visual3);


            //var anim11 = compositor.CreatePathKeyFrameAnimation();
            //anim11.InsertKeyFrame(1, GetLine(new Vector2(1, 18), new Vector2(1, 2)));
            //anim11.InsertKeyFrame(0, GetLine(new Vector2(1, 2), new Vector2(10, 2)));
            OnGlyphChanged(string.Empty, Glyph, true, Strings.AccActionDownload, false);

            if (downloading)
            {
                _shouldEnqueueProgress = true;

                _container.Opacity = 1;
                _container.Scale = Vector3.One;

                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    if (_state == MessageContentState.Downloading)
                    {
                        OnGlyphChanged(Icons.Cancel, Icons.ArrowDownload, true, Strings.AccActionCancelDownload, false);
                        InternalProgress = _enqueuedProgress;
                    }

                    _shouldEnqueueProgress = false;
                    //_container.Children.RemoveAll();
                };

                var animLeft = compositor.CreateScalarKeyFrameAnimation();
                animLeft.InsertKeyFrame(0, 1);
                animLeft.InsertKeyFrame(1, 0);
                animLeft.Duration = TimeSpan.FromMilliseconds(200);

                leftLine.StartAnimation("TrimEnd", animLeft);
                rightLine.StartAnimation("TrimEnd", animLeft);

                var opacityLeft = compositor.CreateScalarKeyFrameAnimation();
                opacityLeft.InsertKeyFrame(0, 1);
                opacityLeft.InsertKeyFrame(1, 0);
                opacityLeft.Duration = TimeSpan.FromSeconds(0.10);
                opacityLeft.DelayTime = TimeSpan.FromSeconds(0.06);

                var opacityRight = compositor.CreateScalarKeyFrameAnimation();
                opacityRight.InsertKeyFrame(0, 1);
                opacityRight.InsertKeyFrame(1, 0);
                opacityRight.Duration = TimeSpan.FromMilliseconds(300);

                visual1.StartAnimation("Opacity", opacityLeft);
                visual2.StartAnimation("Opacity", opacityRight);

                batch.End();

                var animBody = compositor.CreateScalarKeyFrameAnimation();
                animBody.InsertKeyFrame(0, 0.65f);
                animBody.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
                animBody.Duration = TimeSpan.FromMilliseconds(400);

                var animBodyEnd = compositor.CreateScalarKeyFrameAnimation();
                animBodyEnd.InsertKeyFrame(0, 1);
                animBodyEnd.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
                animBodyEnd.Duration = TimeSpan.FromMilliseconds(400);

                var animBodyAlpha = compositor.CreateScalarKeyFrameAnimation();
                animBodyAlpha.InsertKeyFrame(0, 1);
                animBodyAlpha.InsertKeyFrame(1, 0);
                animBodyAlpha.Duration = TimeSpan.FromSeconds(0.3);
                animBodyAlpha.DelayTime = TimeSpan.FromMilliseconds(200);

                arrowPath.StartAnimation("TrimStart", animBody);
                arrowPath.StartAnimation("TrimEnd", animBodyEnd);
                //visual3.StartAnimation("Opacity", animBodyAlpha);
            }
            else if (animate)
            {
                var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(1, new Vector3(1));
                show1.InsertKeyFrame(0, new Vector3(0));

                var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
                show2.InsertKeyFrame(1, 1);
                show2.InsertKeyFrame(0, 0);

                _container.StartAnimation("Scale", show1);
                _container.StartAnimation("Opacity", show2);

                _shouldEnqueueProgress = false;
            }
            else
            {
                _shouldEnqueueProgress = false;
            }
        }

        private CompositionPath GetArrowShape()
        {
            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                builder.BeginFigure(4.2f, 31.1f);
                builder.AddCubicBezier(new Vector2(4.7f, 32.5f), new Vector2(5.4f, 33.8f), new Vector2(6.2f, 35f));
                builder.AddCubicBezier(new Vector2(12.1f, 44f), new Vector2(24f, 44.7f), new Vector2(24f, 33.3f));
                builder.AddLine(24f, 16f);
                builder.EndFigure(CanvasFigureLoop.Open);
                result = CanvasGeometry.CreatePath(builder);
            }
            return new CompositionPath(result);
        }
    }
}
