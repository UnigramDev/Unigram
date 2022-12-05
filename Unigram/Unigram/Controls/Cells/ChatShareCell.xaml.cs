using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class ChatShareCell : Grid, IMultipleElement
    {
        private bool _selected;

        public ChatShareCell()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            _selectionPhoto = ElementCompositionPreview.GetElementVisual(Photo);
            _selectionOutline = ElementCompositionPreview.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(18);
            _selectionOutline.CenterPoint = new Vector3(18);
            _selectionOutline.Opacity = 0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Stroke is SolidColorBrush stroke && _strokeToken == 0 && _visual != null)
            {
                _strokeToken = stroke.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
            }

            if (SelectionStroke is SolidColorBrush selectionStroke && _selectionStrokeToken == 0 && _visual != null)
            {
                _selectionStrokeToken = selectionStroke.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnSelectionStrokeChanged);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (Stroke is SolidColorBrush stroke && _strokeToken != 0)
            {
                stroke.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }

            if (SelectionStroke is SolidColorBrush selectionStroke && _selectionStrokeToken != 0)
            {
                selectionStroke.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _selectionStrokeToken);
                _selectionStrokeToken = 0;
            }
        }

        public void UpdateChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var chat = args.Item as Chat;
            if (chat == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                if (chat.Type is ChatTypeSecret)
                {
                    TitleLabel.Foreground = TypeIcon.Foreground;
                    TypeIcon.Visibility = Visibility.Visible;
                }
                else
                {
                    TitleLabel.ClearValue(TextBlock.ForegroundProperty);
                    TypeIcon.Visibility = Visibility.Collapsed;
                }

                TitleLabel.Text = clientService.GetTitle(chat);
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }


        public void UpdateMessageSender(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var messageSender = args.Item as MessageSender;
            if (messageSender == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                if (clientService.TryGetUser(messageSender, out User user))
                {
                    TitleLabel.Text = user.FullName();
                }
                else if (clientService.TryGetChat(messageSender, out Chat chat))
                {
                    TitleLabel.Text = clientService.GetTitle(chat);
                }
            }
            else if (args.Phase == 2)
            {
                if (clientService.TryGetUser(messageSender, out User user))
                {
                    Photo.SetUser(clientService, user, 36);
                    Identity.SetStatus(clientService, user);
                }
                else if (clientService.TryGetChat(messageSender, out Chat chat))
                {
                    Photo.SetChat(clientService, chat, 36);
                    Identity.SetStatus(clientService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        #region Stroke

        private long _strokeToken;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ChatShareCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatShareCell)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            if (oldValue != null && _strokeToken != 0)
            {
                oldValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }

            if (newValue == null || _ellipse == null)
            {
                return;
            }

            _ellipse.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);
            _strokeToken = newValue.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _ellipse == null)
            {
                return;
            }

            _ellipse.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

        #region SelectionStroke

        private long _selectionStrokeToken;

        public SolidColorBrush SelectionStroke
        {
            get => (SolidColorBrush)GetValue(SelectionStrokeProperty);
            set => SetValue(SelectionStrokeProperty, value);
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChatShareCell), new PropertyMetadata(null, OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatShareCell)d).OnSelectionStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnSelectionStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            if (oldValue != null && _selectionStrokeToken != 0)
            {
                oldValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _selectionStrokeToken);
                _selectionStrokeToken = 0;
            }

            if (newValue == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);
            _selectionStrokeToken = newValue.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnSelectionStrokeChanged);
        }

        private void OnSelectionStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

        #region Selection Animation

        private readonly Visual _selectionOutline;
        private readonly Visual _selectionPhoto;

        private CompositionPathGeometry _polygon;
        private CompositionSpriteShape _ellipse;
        private CompositionSpriteShape _stroke;
        private ShapeVisual _visual;

        private CompositionBrush GetBrush(DependencyProperty dp, ref long token, DependencyPropertyChangedCallback callback)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                if (token == 0)
                {
                    token = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, callback);
                }

                return Window.Current.Compositor.CreateColorBrush(solid.Color);
            }

            return Window.Current.Compositor.CreateColorBrush(Colors.Black);
        }

        private void InitializeSelection()
        {
            static CompositionPath GetCheckMark()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    //builder.BeginFigure(new Vector2(3.821f, 7.819f));
                    //builder.AddLine(new Vector2(6.503f, 10.501f));
                    //builder.AddLine(new Vector2(12.153f, 4.832f));
                    builder.BeginFigure(new Vector2(5.821f, 9.819f));
                    builder.AddLine(new Vector2(7.503f, 12.501f));
                    builder.AddLine(new Vector2(14.153f, 6.832f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var compositor = Window.Current.Compositor;
            //12.711,5.352 11.648,4.289 6.5,9.438 4.352,7.289 3.289,8.352 6.5,11.563

            var polygon = compositor.CreatePathGeometry();
            polygon.Path = GetCheckMark();

            var shape1 = compositor.CreateSpriteShape();
            shape1.Geometry = polygon;
            shape1.StrokeThickness = 1.5f;
            shape1.StrokeBrush = compositor.CreateColorBrush(Colors.White);

            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2(8);
            ellipse.Center = new Vector2(10);

            var shape2 = compositor.CreateSpriteShape();
            shape2.Geometry = ellipse;
            shape2.FillBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);

            var outer = compositor.CreateEllipseGeometry();
            outer.Radius = new Vector2(10);
            outer.Center = new Vector2(10);

            var shape3 = compositor.CreateSpriteShape();
            shape3.Geometry = outer;
            shape3.FillBrush = GetBrush(SelectionStrokeProperty, ref _selectionStrokeToken, OnSelectionStrokeChanged);

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape3);
            visual.Shapes.Add(shape2);
            visual.Shapes.Add(shape1);
            visual.Size = new Vector2(20, 20);
            visual.Offset = new Vector3(36 - 17, 36 - 17, 0);
            visual.CenterPoint = new Vector3(8);
            visual.Scale = new Vector3(0);

            ElementCompositionPreview.SetElementChildVisual(PhotoPanel, visual);

            _polygon = polygon;
            _ellipse = shape2;
            _stroke = shape3;
            _visual = visual;
        }

        public void UpdateState(bool selected, bool animate)
        {
            if (_selected == selected)
            {
                return;
            }

            if (_visual == null)
            {
                InitializeSelection();
            }

            if (animate)
            {
                var compositor = Window.Current.Compositor;

                var anim3 = compositor.CreateScalarKeyFrameAnimation();
                anim3.InsertKeyFrame(selected ? 0 : 1, 0);
                anim3.InsertKeyFrame(selected ? 1 : 0, 1);

                var anim1 = compositor.CreateScalarKeyFrameAnimation();
                anim1.InsertKeyFrame(selected ? 0 : 1, 0);
                anim1.InsertKeyFrame(selected ? 1 : 0, 1);
                anim1.DelayTime = TimeSpan.FromMilliseconds(anim1.Duration.TotalMilliseconds / 2);
                anim1.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                var anim2 = compositor.CreateVector3KeyFrameAnimation();
                anim2.InsertKeyFrame(selected ? 0 : 1, new Vector3(0));
                anim2.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));

                _polygon.StartAnimation("TrimEnd", anim1);
                _visual.StartAnimation("Scale", anim2);
                _visual.StartAnimation("Opacity", anim3);

                var anim4 = compositor.CreateVector3KeyFrameAnimation();
                anim4.InsertKeyFrame(selected ? 0 : 1, new Vector3(1));
                anim4.InsertKeyFrame(selected ? 1 : 0, new Vector3(28f / 36f));

                var anim5 = compositor.CreateVector3KeyFrameAnimation();
                anim5.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));
                anim5.InsertKeyFrame(selected ? 0 : 1, new Vector3(28f / 36f));

                _selectionPhoto.StartAnimation("Scale", anim4);
                _selectionOutline.StartAnimation("Scale", anim5);
                _selectionOutline.StartAnimation("Opacity", anim3);
            }
            else
            {
                _polygon.TrimEnd = selected ? 1 : 0;
                _visual.Scale = new Vector3(selected ? 1 : 0);
                _visual.Opacity = selected ? 1 : 0;

                _selectionPhoto.Scale = new Vector3(selected ? 28f / 36f : 1);
                _selectionOutline.Scale = new Vector3(selected ? 1 : 28f / 36f);
                _selectionOutline.Opacity = selected ? 1 : 0;
            }

            _selected = selected;
        }

        #endregion
    }
}
