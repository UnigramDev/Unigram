using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Unigram.Controls.Messages.Content;
using Unigram.Converters;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class FileButton : GlyphHyperlinkButton
    {
        private TextBlock _label1;
        private TextBlock _label2;

        private Visual _visual1;
        private Visual _visual2;

        private TextBlock _label;
        private Visual _visual;

        private ContainerVisual _container;

        private long _fileId;
        private MessageContentState _state;

        public FileButton()
        {
            DefaultStyleKey = typeof(FileButton);
        }

        public bool IsSmall { get; set; }

        protected override void OnApplyTemplate()
        {
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

            ElementCompositionPreview.SetElementChildVisual(GetTemplateChild("RootGrid") as Grid, _container);

            base.OnApplyTemplate();
        }

        #region Progress



        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Progress.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(FileButton), new PropertyMetadata(0.0));



        #endregion

        #region ProgressVisibility

        public Visibility ProgressVisibility
        {
            get { return (Visibility)GetValue(ProgressVisibilityProperty); }
            set { SetValue(ProgressVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ProgressVisibilityProperty =
            DependencyProperty.Register("ProgressVisibility", typeof(Visibility), typeof(FileButton), new PropertyMetadata(Visibility.Visible));

        #endregion


        //public void SetGlyph(string glyph, bool animate)
        //{
        //    OnGlyphChanged(glyph, Glyph, animate);
        //}

        public void SetGlyph(int fileId, MessageContentState state)
        {
            if (fileId != _fileId)
            {
                _state = MessageContentState.None;
            }

            switch (state)
            {
                case MessageContentState.Download:
                    OnGlyphChanged(IsSmall ? Icons.DownloadSmall : Icons.Download, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Downloading:
                    OnGlyphChanged(IsSmall ? Icons.CancelSmall : Icons.Cancel, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Uploading:
                    OnGlyphChanged(IsSmall ? Icons.CancelSmall : Icons.Cancel, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Confirm:
                    OnGlyphChanged(Icons.Confirm, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Document:
                    OnGlyphChanged(Icons.Document, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Animation:
                    OnGlyphChanged(Icons.Animation, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Photo:
                    OnGlyphChanged(Icons.Photo, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Play:
                    //if (_state == MessageContentState.Pause && ApiInfo.CanUseDirectComposition)
                    //{
                    //    OnPauseToPlay();
                    //}
                    //else
                    {
                        OnGlyphChanged(Icons.Play, Glyph, _state != state && _state != MessageContentState.None);
                    }
                    break;
                case MessageContentState.Pause:
                    //if (_state == MessageContentState.Play && ApiInfo.CanUseDirectComposition)
                    //{
                    //    OnPlayToPause();
                    //}
                    //else
                    {
                        OnGlyphChanged(Icons.Pause, Glyph, _state != state && _state != MessageContentState.None);
                    }
                    break;
                case MessageContentState.Ttl:
                    OnGlyphChanged(Icons.Ttl, Glyph, _state != state && _state != MessageContentState.None);
                    break;
                case MessageContentState.Theme:
                    OnGlyphChanged(Icons.Theme, Glyph, _state != state && _state != MessageContentState.None);
                    break;
            }

            _fileId = fileId;
            _state = state;
        }

        private void OnGlyphChanged(string newValue, string oldValue, bool animate)
        {
            if (string.IsNullOrEmpty(oldValue) || string.IsNullOrEmpty(newValue))
            {
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Glyph = newValue;

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
        }



        private void OnPauseToPlay()
        {
            var compositor = Window.Current.Compositor;
            var line1 = compositor.CreatePathGeometry();
            line1.Path = GetLine(new Vector2(1, 18), new Vector2(1, 2));

            var shape1 = compositor.CreateSpriteShape(line1);
            shape1.StrokeThickness = 1;
            shape1.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            shape1.StrokeStartCap = CompositionStrokeCap.Round;
            shape1.StrokeEndCap = CompositionStrokeCap.Round;

            var line2 = compositor.CreatePathGeometry();
            line2.Path = GetLine(new Vector2(1, 2), new Vector2(12.5f, 10.5f));

            var shape2 = compositor.CreateSpriteShape(line2);
            shape2.StrokeThickness = 1;
            shape2.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            shape2.StrokeStartCap = CompositionStrokeCap.Round;
            shape2.StrokeEndCap = CompositionStrokeCap.Round;

            var line3 = compositor.CreatePathGeometry();
            line3.Path = GetLine(new Vector2(12.5f, 10.5f), new Vector2(1, 18f));

            var shape3 = compositor.CreateSpriteShape(line3);
            shape3.StrokeThickness = 1;
            shape3.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            shape3.StrokeStartCap = CompositionStrokeCap.Round;
            shape3.StrokeEndCap = CompositionStrokeCap.Round;

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape1);
            visual.Shapes.Add(shape2);
            visual.Shapes.Add(shape3);
            visual.Size = new Vector2(20, 20);
            visual.Offset = new Vector3(((48 - 20) / 2) + 4.5f, (48 - 20) / 2, 0);

            _visual1.Opacity = 0;
            _visual2.Opacity = 0;

            _container.Children.RemoveAll();
            _container.Children.InsertAtTop(visual);


            var anim11 = compositor.CreatePathKeyFrameAnimation();
            anim11.InsertKeyFrame(1, GetLine(new Vector2(1, 18), new Vector2(1, 2)));
            anim11.InsertKeyFrame(0, GetLine(new Vector2(1, 2), new Vector2(10, 2)));

            var anim12 = compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(1, 0);
            anim12.InsertKeyFrame(0, 1);
            anim12.Duration = anim11.Duration;


            var anim21 = compositor.CreatePathKeyFrameAnimation();
            anim21.InsertKeyFrame(1, GetLine(new Vector2(1, 2), new Vector2(12.5f, 10.5f)));
            anim21.InsertKeyFrame(0, GetLine(new Vector2(10, 2), new Vector2(10, 18)));
            anim21.Duration = anim11.Duration;


            var anim31 = compositor.CreatePathKeyFrameAnimation();
            anim31.InsertKeyFrame(1, GetLine(new Vector2(12.5f, 10.5f), new Vector2(1, 18f)));
            anim31.InsertKeyFrame(0, GetLine(new Vector2(1, 18), new Vector2(1, 2)));
            anim31.Duration = anim11.Duration;


            line1.StartAnimation("TrimStart", anim12);
            line1.StartAnimation("Path", anim11);
            line2.StartAnimation("Path", anim21);
            line3.StartAnimation("Path", anim31);
        }

        private void OnPlayToPause()
        {
            var compositor = Window.Current.Compositor;
            var line1 = compositor.CreatePathGeometry();
            line1.Path = GetLine(new Vector2(1, 18), new Vector2(1, 2));

            var shape1 = compositor.CreateSpriteShape(line1);
            shape1.StrokeThickness = 1;
            shape1.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            shape1.StrokeStartCap = CompositionStrokeCap.Round;
            shape1.StrokeEndCap = CompositionStrokeCap.Round;

            var line2 = compositor.CreatePathGeometry();
            line2.Path = GetLine(new Vector2(1, 2), new Vector2(12.5f, 10.5f));

            var shape2 = compositor.CreateSpriteShape(line2);
            shape2.StrokeThickness = 1;
            shape2.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            shape2.StrokeStartCap = CompositionStrokeCap.Round;
            shape2.StrokeEndCap = CompositionStrokeCap.Round;

            var line3 = compositor.CreatePathGeometry();
            line3.Path = GetLine(new Vector2(12.5f, 10.5f), new Vector2(1, 18f));

            var shape3 = compositor.CreateSpriteShape(line3);
            shape3.StrokeThickness = 1;
            shape3.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);
            shape3.StrokeStartCap = CompositionStrokeCap.Round;
            shape3.StrokeEndCap = CompositionStrokeCap.Round;

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape1);
            visual.Shapes.Add(shape2);
            visual.Shapes.Add(shape3);
            visual.Size = new Vector2(20, 20);
            visual.Offset = new Vector3(((48 - 20) / 2) + 4.5f, (48 - 20) / 2, 0);

            _visual1.Opacity = 0;
            _visual2.Opacity = 0;

            _container.Children.RemoveAll();
            _container.Children.InsertAtTop(visual);


            var anim11 = compositor.CreatePathKeyFrameAnimation();
            anim11.InsertKeyFrame(0, GetLine(new Vector2(1, 18), new Vector2(1, 2)));
            anim11.InsertKeyFrame(1, GetLine(new Vector2(1, 2), new Vector2(10, 2)));

            var anim12 = compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(0, 0);
            anim12.InsertKeyFrame(1, 1);
            anim12.Duration = anim11.Duration;


            var anim21 = compositor.CreatePathKeyFrameAnimation();
            anim21.InsertKeyFrame(0, GetLine(new Vector2(1, 2), new Vector2(12.5f, 10.5f)));
            anim21.InsertKeyFrame(1, GetLine(new Vector2(10, 2), new Vector2(10, 18)));
            anim21.Duration = anim11.Duration;


            var anim31 = compositor.CreatePathKeyFrameAnimation();
            anim31.InsertKeyFrame(0, GetLine(new Vector2(11, 8), new Vector2(1, 15)));
            anim31.InsertKeyFrame(1, GetLine(new Vector2(1, 18), new Vector2(1, 2)));
            anim31.Duration = anim11.Duration;


            line1.StartAnimation("TrimStart", anim12);
            line1.StartAnimation("Path", anim11);
            line2.StartAnimation("Path", anim21);
            line3.StartAnimation("Path", anim31);
        }

        CompositionPath GetLine(Vector2 pt1, Vector2 pt2)
        {
            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                builder.BeginFigure(pt1);
                builder.AddLine(pt2);
                builder.EndFigure(CanvasFigureLoop.Open);
                result = CanvasGeometry.CreatePath(builder);
            }
            return new CompositionPath(result);
        }
    }
}
