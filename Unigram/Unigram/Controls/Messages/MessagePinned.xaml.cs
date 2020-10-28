using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessagePinned : MessageReferenceBase
    {
        private UIElement _parent;

        private Visual _textVisual1;
        private Visual _textVisual2;

        private Visual _textVisual;

        private long _chatId;
        private long _messageId;

        private bool _loading;

        public MessagePinned()
        {
            InitializeComponent();

            var root = ElementCompositionPreview.GetElementVisual(this);
            root.Clip = root.Compositor.CreateInsetClip();

            _textVisual1 = ElementCompositionPreview.GetElementVisual(TextLabel1);
            _textVisual2 = ElementCompositionPreview.GetElementVisual(TextLabel2);

            _textVisual = _textVisual1;

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _chatId = 0;
            _messageId = 0;

            _loading = false;

            _collapsed = true;
            Visibility = Visibility.Collapsed;
        }

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
        }

        public void UpdateIndex(int value, int maximum, bool intermediate)
        {
            //Line.UpdateIndex(value, maximum, intermediate);
        }

        private Queue<(Chat, MessageViewModel, bool, int, int)> _queue = new Queue<(Chat, MessageViewModel, bool, int, int)>();
        private bool _playing;

        public void UpdateMessage(Chat chat, MessageViewModel message, bool known, int value, int maximum, bool intermediate)
        {
            HideButton.Visibility = maximum > 1 ? Visibility.Collapsed : Visibility.Visible;
            ListButton.Visibility = maximum > 1 ? Visibility.Visible : Visibility.Collapsed;

            if (message == null && !known)
            {
                _chatId = 0;
                _messageId = 0;

                _loading = false;
                ShowHide(false);
                return;
            }

            if (message != null || known)
            {
                ShowHide(true);
            }

            string title;
            if (ApiInfo.CanUseDirectComposition)
            {
                title = Strings.Resources.PinnedMessage + (value >= 0 && maximum > 1 ? " #" : "");
            }
            else
            {
                title = Strings.Resources.PinnedMessage + (value >= 0 && maximum > 1 ? $" #{value + 1}" : "");
            }

            if (_loading || (_chatId == chat.Id && _messageId == 0))
            {
                _textVisual = _textVisual == _textVisual1 ? _textVisual2 : _textVisual1;
                UpdateMessage(message, message == null, title);

                Line.UpdateIndex(value, maximum, 0);
                Number.Value = maximum > 1 ? value + 1 : -1;

                _chatId = chat.Id;
                _messageId = message?.Id ?? 0;

                _loading = known;
                return;
            }
            else if (_chatId == chat.Id && _messageId == message?.Id)
            {
                return;
            }

            if (!intermediate)
            {
                _queue.Clear();
            }

            if (_playing)
            {
                _queue.Enqueue((chat, message, known, value, maximum));

                if (_queue.Count > 1)
                {
                    _queue.TryDequeue(out var _);
                }

                return;
            }

            _playing = true;
            Debug.WriteLine("Playing text");

            var cross = _chatId == chat.Id;
            var prev = _messageId < message?.Id;

            Line.UpdateIndex(value, maximum, prev ? 1 : -1);
            Number.Value = maximum > 1 ? value + 1 : -1;

            _chatId = chat.Id;
            _messageId = message?.Id ?? 0;

            _loading = known;

            var textVisualShow = _textVisual == _textVisual1 ? _textVisual2 : _textVisual1;
            var textVisualHide = _textVisual == _textVisual1 ? _textVisual1 : _textVisual2;

            var referenceShow = _textVisual == _textVisual1 ? TextLabel2 : TextLabel1;
            var referenceHide = _textVisual == _textVisual1 ? TextLabel1 : TextLabel2;

            Canvas.SetZIndex(referenceShow, 1);
            Canvas.SetZIndex(referenceHide, 0);

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _playing = false;

                if (_queue.TryDequeue(out var auto))
                {
                    UpdateMessage(auto.Item1, auto.Item2, auto.Item3, auto.Item4, auto.Item5, false);
                }
            };

            if (cross)
            {
                var hide1 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                hide1.InsertKeyFrame(0, new Vector3(0));
                hide1.InsertKeyFrame(1, new Vector3(0, prev ? -8 : 8, 0));

                textVisualHide.StartAnimation("Offset", hide1);
            }
            else
            {
                textVisualHide.Offset = Vector3.Zero;
            }

            var hide2 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            textVisualHide.StartAnimation("Opacity", hide2);

            UpdateMessage(message, message == null, title);
            //referenceShow.IsTabStop = true;
            //referenceHide.IsTabStop = false;

            if (cross)
            {
                var show1 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(0, new Vector3(0, prev ? 8 : -8, 0));
                show1.InsertKeyFrame(1, new Vector3(0));

                textVisualShow.StartAnimation("Offset", show1);
            }
            else
            {
                textVisualShow.Offset = Vector3.Zero;
            }

            var show2 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(0, 0);
            show2.InsertKeyFrame(1, 1);

            textVisualShow.StartAnimation("Opacity", show2);
            batch.End();

            _textVisual = textVisualShow;
        }

        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if ((show && Visibility == Visibility.Visible) || (!show && (Visibility == Visibility.Collapsed || _collapsed)))
            {
                return;
            }

            if (show)
            {
                _collapsed = false;
            }
            else
            {
                _collapsed = true;
            }

            Visibility = Visibility.Visible;

            var visual = ElementCompositionPreview.GetElementVisual(_parent);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                visual.Offset = new Vector3();

                if (show)
                {
                    _collapsed = false;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 48);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = TimeSpan.FromMilliseconds(150);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -48, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromMilliseconds(150);

            visual.Clip.StartAnimation("TopInset", clip);
            visual.StartAnimation("Offset", offset);

            batch.End();
        }

        public ICommand HideCommand
        {
            get => HideButton.Command;
            set => HideButton.Command = value;
        }

        public ICommand ListCommand
        {
            get => ListButton.Command;
            set => ListButton.Command = value;
        }

        #region Overrides

        protected override void HideThumbnail()
        {
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;
        }

        protected override void ShowThumbnail(CornerRadius radius = default)
        {
            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            ThumbRoot.CornerRadius =
                ThumbEllipse.CornerRadius = radius;
        }

        protected override void SetThumbnail(ImageSource value)
        {
            if (ThumbImage != null)
                ThumbImage.ImageSource = value;
        }

        protected override void SetTitle(string value)
        {
            TitleLabel.Text = value;
        }

        protected override void SetService(string value)
        {
            var referenceShow = _textVisual == _textVisual1 ? ServiceLabel2 : ServiceLabel1;
            referenceShow.Text = value;
        }

        protected override void SetMessage(string value)
        {
            var referenceShow = _textVisual == _textVisual1 ? MessageLabel2 : MessageLabel1;
            referenceShow.Text = value;
        }

        protected override void AppendService(string value)
        {
            var referenceShow = _textVisual == _textVisual1 ? ServiceLabel2 : ServiceLabel1;
            referenceShow.Text += value;
        }

        protected override void AppendMessage(string value)
        {
            var referenceShow = _textVisual == _textVisual1 ? MessageLabel2 : MessageLabel1;
            referenceShow.Text += value;
        }

        #endregion
    }

    public class MessagePinnedLine : Control
    {
        private CompositionSpriteShape _back;
        private CompositionSpriteShape _fore;
        private CompositionRectangleGeometry _forePath;
        private CompositionSpriteShape _mask;
        private CompositionPathGeometry _maskPath;

        public MessagePinnedLine()
        {
            if (!ApiInfo.CanUseDirectComposition)
            {
                return;
            }

            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
            RegisterPropertyChangedCallback(BorderBrushProperty, OnBorderBrushChanged);

            var compositor = Window.Current.Compositor;

            var visual = compositor.CreateShapeVisual();
            visual.Clip = compositor.CreateInsetClip(0, 0, 1, 0);
            visual.Size = new Vector2(4, 36);

            var back = compositor.CreateRectangleGeometry();
            back.Offset = Vector2.Zero;
            back.Size = new Vector2(4, 36);

            var backShape = compositor.CreateSpriteShape(back);
            backShape.FillBrush = GetBrush(BorderBrushProperty);

            var fore = compositor.CreateRectangleGeometry();
            fore.Offset = Vector2.Zero;
            fore.Size = new Vector2(4, 36);
            //fore.CornerRadius = Vector2.One;

            var foreShape = compositor.CreateSpriteShape(fore);
            foreShape.FillBrush = GetBrush(ForegroundProperty);

            var mask = compositor.CreatePathGeometry(GetMask(1));
            var maskShape = compositor.CreateSpriteShape(mask);
            maskShape.FillBrush = GetBrush(BackgroundProperty);
            maskShape.Offset = new Vector2(-2);

            visual.Shapes.Add(backShape);
            visual.Shapes.Add(foreShape);
            visual.Shapes.Add(maskShape);

            _back = backShape;
            _fore = foreShape;
            _forePath = fore;
            _mask = maskShape;
            _maskPath = mask;

            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_fore != null)
            {
                _fore.FillBrush = GetBrush(dp);
            }
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_mask != null)
            {
                _mask.FillBrush = GetBrush(dp);
            }
        }

        private void OnBorderBrushChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_back != null)
            {
                _back.FillBrush = GetBrush(dp);
            }
        }

        private CompositionBrush GetBrush(DependencyProperty dp)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                return Window.Current.Compositor.CreateColorBrush(solid.Color);
            }

            return Window.Current.Compositor.CreateColorBrush(Colors.White);
        }

        private int _oldMaximum = 4;
        private float _oldHeight = 12;

        private int _prevValue;
        private int _nextValue;

        private Queue<(int, int, int)> _queue = new Queue<(int, int, int)>();
        private bool _playing;

        public void UpdateIndex(int value, int maximum, int direction, bool intermediate)
        {
            if (_prevValue == value && intermediate)
            {
                return;
            }

            if (!intermediate)
            {
                _queue.Clear();
            }

            UpdateIndex(value, maximum, direction);
            _prevValue = value;
        }

        public void UpdateIndex(int value, int maximum, int direction)
        {
            Debug.WriteLine("UpdateIndex({0}, {1}, {2})", value, maximum, direction);

            if (_maskPath == null || _nextValue == value)
            {
                return;
            }

            if (_playing)
            {
                Enqueue(value, maximum, direction);
                return;
            }

            _playing = true;
            _nextValue = value;

            Debug.WriteLine("Playing line");

            var h = 12f;
            var m = 3f;

            if (maximum < 4)
            {
                h = (36f - ((maximum - 1) * m)) / maximum;
            }

            _forePath.Size = new Vector2(2, h);
            _maskPath.Path = GetMask(maximum);

            var easing = _mask.Compositor.CreateLinearEasingFunction();

            //if (_oldHeight != h)
            //{
            //    var animFore = _mask.Compositor.CreateVector2KeyFrameAnimation();
            //    animFore.InsertKeyFrame(0, new Vector2(4, _oldHeight));
            //    animFore.InsertKeyFrame(1, new Vector2(4, h));

            //    _forePath.StartAnimation("Size", animFore);
            //    _oldHeight = h;
            //}

            //if (_oldMaximum != maximum)
            //{
            //    var animMask = _mask.Compositor.CreatePathKeyFrameAnimation();
            //    animMask.InsertKeyFrame(0, GetMask(_oldMaximum));
            //    animMask.InsertKeyFrame(1, GetMask(maximum));

            //    _maskPath.StartAnimation("Path", animMask);
            //    _oldMaximum = maximum;
            //}

            float initial1 = -2;
            float initial2 = 0;

            float final1 = initial1;
            float final2 = initial2;

            if (maximum > 3)
            {
                float height = (h + m) * 3 - m;

                initial1 = (32 - height) / 2f;
                initial2 = (36 - h) / 2f;

                final1 = initial1;
                final2 = initial2;

                if (direction > 0)
                {
                    if (value - direction == 0)
                    {
                        initial1 = -2;
                        initial2 = 0;
                    }
                    else if (value == maximum - 1)
                    {
                        initial1 -= h + m;

                        final1 = 34 - (h + m) * 4 + m;
                        final2 = 36 - h;
                    }
                    else
                    {
                        final1 -= h + m;
                    }
                }
                else if (direction < 0)
                {
                    if (value == 0)
                    {
                        final1 = -2;
                        final2 = 0;
                    }
                    else if (value - direction == maximum - 1)
                    {
                        final1 -= h + m;

                        initial1 = 34 - (h + m) * 4 + m;
                        initial2 = 36 - h;
                    }
                    else
                    {
                        //final1 += h + m;

                        initial1 -= h + m;
                    }
                }
                else if (value == 0)
                {
                    initial1 = final1 = -2;
                    initial2 = final2 = 0;
                }
                else if (value == maximum - 1)
                {
                    initial1 -= h + m;

                    initial1 = final1 = 34 - (h + m) * 4 + m;
                    initial2 = final2 = 36 - h;
                }
            }
            else
            {
                var prev = value - direction;
                var next = value;

                initial2 = prev * (h + m);
                final2 = next * (h + m);
            }

            Debug.WriteLine("Initial1: " + initial1 + ", final1: " + final1);

            var batch = _mask.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _playing = false;

                if (_queue.TryDequeue(out var auto))
                {
                    UpdateIndex(auto.Item1, auto.Item2, auto.Item3);
                }
            };

            if (initial1 != final1)
            {
                var anim1 = _mask.Compositor.CreateVector2KeyFrameAnimation();
                anim1.InsertKeyFrame(0, new Vector2(-2, initial1), easing);
                anim1.InsertKeyFrame(1, new Vector2(-2, final1), easing);

                _mask.StartAnimation("Offset", anim1);
            }
            else
            {
                _mask.Offset = new Vector2(-2, final1);
            }

            if (initial2 != final2 && maximum > 1)
            {
                var anim2 = _mask.Compositor.CreateVector2KeyFrameAnimation();
                anim2.InsertKeyFrame(0, new Vector2(0, initial2), easing);
                anim2.InsertKeyFrame(1, new Vector2(0, final2), easing);

                _fore.StartAnimation("Offset", anim2);
            }
            else
            {
                _fore.Offset = new Vector2(0, final2);
            }

            batch.End();
        }

        private void Enqueue(int value, int maximum, int direction)
        {
            _queue.Enqueue((value, maximum, direction));

            if (_queue.Count > 1)
            {
                _queue.TryDequeue(out var _);
            }
        }

        CompositionPath GetMask(int maximum)
        {
            var h = 12f;
            var m = 3f;

            if (maximum < 4)
            {
                h = (36f - ((maximum - 1) * m)) / maximum;
            }

            var geometries = new CanvasGeometry[4];

            for (int i = 0; i < geometries.Length; i++)
            {
                geometries[i] = CanvasGeometry.CreateRectangle(null, 2, 2 + i * (h + m), 4, h);
            }

            var rectangle = CanvasGeometry.CreateRectangle(null, 0, 0, 8, (h + m) * geometries.Length + 1);

            var group1 = CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding);
            var group2 = CanvasGeometry.CreateGroup(null, new[] { rectangle, group1 }, CanvasFilledRegionDetermination.Alternate);

            return new CompositionPath(group2);
        }

    }
}
