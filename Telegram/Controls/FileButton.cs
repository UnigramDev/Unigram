//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Controls.Media;
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
        private Grid RootGrid;

        private TextBlock ContentPresenter1;
        private TextBlock ContentPresenter2;
        private TextBlock _label;

        private bool _hasContainer;

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
            RootGrid = GetTemplateChild(nameof(RootGrid)) as Grid;

            ContentPresenter1 = GetTemplateChild(nameof(ContentPresenter1)) as TextBlock;
            ContentPresenter2 = GetTemplateChild(nameof(ContentPresenter2)) as TextBlock;

            ContentPresenter1.Text = Glyph ?? string.Empty;
            ContentPresenter2.Text = string.Empty;

            _label = ContentPresenter1;
        }

        #region Progress

        public double InternalProgress
        {
            get => (double)GetValue(InternalProgressProperty);
            set
            {
                try
                {
                    SetValue(InternalProgressProperty, value);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public static readonly DependencyProperty InternalProgressProperty =
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
                    OnGlyphChanged(IsSmall ? Icons.DownloadSmall : Icons.ArrowDownloadFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionDownload);
                    break;
                case MessageContentState.Downloading:
                    if (IsSmall || (_state != MessageContentState.Download && _state != MessageContentState.Downloading))
                    {
                        OnGlyphChanged(IsSmall ? Icons.CancelSmall : Icons.DismissFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionCancelDownload, false);
                    }
                    else if (_state != MessageContentState.Downloading)
                    {
                        SetDownloadGlyph(_state != MessageContentState.Download);
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
                    OnGlyphChanged(string.Empty, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionOpenFile);
                    break;
                case MessageContentState.Play:
                    OnGlyphChanged(Icons.PlayFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionPlay);
                    break;
                case MessageContentState.Pause:
                    OnGlyphChanged(Icons.PauseFilled24, Glyph, _state != state && _state != MessageContentState.None, Strings.AccActionPause);
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
            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Glyph = newValue;
            AutomationProperties.SetName(this, automation);

            if (_label == null)
            {
                return;
            }

            if (animate)
            {
                var labelShow = _label == ContentPresenter1 ? ContentPresenter2 : ContentPresenter1;
                var labelHide = _label == ContentPresenter1 ? ContentPresenter1 : ContentPresenter2;

                var visualShow = ElementComposition.GetElementVisual(labelShow);
                var visualHide = ElementComposition.GetElementVisual(labelHide);

                var compositor = visualShow.Compositor;

                visualShow.CenterPoint = new Vector3(10);
                visualHide.CenterPoint = new Vector3(10);

                var hide1 = compositor.CreateVector3KeyFrameAnimation();
                hide1.InsertKeyFrame(0, Vector3.One);
                hide1.InsertKeyFrame(1, Vector3.Zero);

                var hide2 = compositor.CreateScalarKeyFrameAnimation();
                hide2.InsertKeyFrame(0, 1);
                hide2.InsertKeyFrame(1, 0);

                visualHide.StartAnimation("Scale", hide1);
                visualHide.StartAnimation("Opacity", hide2);

                var show1 = compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(1, Vector3.One);
                show1.InsertKeyFrame(0, Vector3.Zero);

                var show2 = compositor.CreateScalarKeyFrameAnimation();
                show2.InsertKeyFrame(1, 1);
                show2.InsertKeyFrame(0, 0);

                visualShow.StartAnimation("Scale", show1);
                visualShow.StartAnimation("Opacity", show2);

                _label = labelShow;
            }

            _label.Text = newValue;

            if (_hasContainer && (clearContainer || !animate))
            {
                _hasContainer = false;
                ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDownloadGlyph(bool animate)
        {
            try
            {
                SetDownloadGlyphImpl(animate);
            }
            catch
            {
                // Compositor.CreateSpriteShape can throw InvalidCastException
                OnGlyphChanged(Icons.DismissFilled24, Glyph, animate, Strings.AccActionCancelDownload, false);
            }
        }

        private void SetDownloadGlyphImpl(bool animate)
        {
            if (RootGrid == null)
            {
                return;
            }

            OnGlyphChanged(string.Empty, Glyph, animate, Strings.AccActionCancelDownload, false);

            var compositor = Window.Current.Compositor;
            var diameter = 48f; // min(bounds.size.width, bounds.size.height)
            var factor = diameter / 48f;

            var lineWidth = 2; //MathF.Max(1.6f, 2.25f * factor);

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
            visual3.Shapes.Add(arrowShape);
            visual3.Size = new Vector2(48, 48);

            var container = Window.Current.Compositor.CreateContainerVisual();
            container.Children.InsertAtTop(visual1);
            container.Children.InsertAtTop(visual3);
            container.Size = new Vector2(48, 48);

            _hasContainer = true;
            ElementCompositionPreview.SetElementChildVisual(RootGrid, container);

            _shouldEnqueueProgress = true;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += OnDownloadingCompleted;

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

            visual1.StartAnimation("Opacity", opacityLeft);

            batch.End();

            var animBody = compositor.CreateScalarKeyFrameAnimation();
            animBody.InsertKeyFrame(0, 0.65f);
            animBody.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
            animBody.Duration = TimeSpan.FromMilliseconds(400);

            var animBodyEnd = compositor.CreateScalarKeyFrameAnimation();
            animBodyEnd.InsertKeyFrame(0, 1);
            animBodyEnd.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
            animBodyEnd.Duration = TimeSpan.FromMilliseconds(400);

            arrowPath.StartAnimation("TrimStart", animBody);
            arrowPath.StartAnimation("TrimEnd", animBodyEnd);
        }

        private void OnDownloadingCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            try
            {
                if (_state == MessageContentState.Downloading && this.IsConnected())
                {
                    OnGlyphChanged(Icons.Cancel, Icons.ArrowDownload, true, Strings.AccActionCancelDownload, false);
                    InternalProgress = _enqueuedProgress;
                }

                _shouldEnqueueProgress = false;
                //_container.Children.RemoveAll();
            }
            catch
            {
                // May throw MissingInteropDataException, kind of unexplicable as no reflection is involved.
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
