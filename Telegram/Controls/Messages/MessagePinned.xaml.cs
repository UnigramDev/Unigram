//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public sealed partial class MessagePinned : MessageReferenceBase
    {
        private UIElement _parent;

        private readonly Visual _textVisual1;
        private readonly Visual _textVisual2;

        private Visual _textVisual;

        private long _chatId;
        private new MessageViewModel _message;

        private bool _loading;

        private string _alternativeText;

        public MessagePinned()
        {
            InitializeComponent();

            var root = ElementCompositionPreview.GetElementVisual(this);
            root.Clip = root.Compositor.CreateInsetClip();

            _textVisual1 = ElementCompositionPreview.GetElementVisual(TextLabel1);
            _textVisual2 = ElementCompositionPreview.GetElementVisual(TextLabel2);

            _textVisual = _textVisual1;

            _templateApplied = true;

            Unloaded += OnUnloaded;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessagePinnedAutomationPeer(this);
        }

        public string GetNameCore()
        {
            return _alternativeText;
        }

        public new MessageViewModel Message => _message;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _chatId = 0;
            _message = null;
            _loading = false;

            _collapsed = true;
            Visibility = Visibility.Collapsed;
        }

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        private readonly Queue<(Chat, MessageViewModel, bool, int, int)> _queue = new();
        private bool _playing;

        public void UpdateMessage(Chat chat, MessageViewModel message, bool known, int value, int maximum, bool intermediate)
        {
            if (message?.ReplyMarkup is ReplyMarkupInlineKeyboard inlineKeyboard
                && inlineKeyboard.Rows.Count == 1
                && inlineKeyboard.Rows[0].Count == 1)
            {
                ActionButton.Content = inlineKeyboard.Rows[0][0].Text;
                ActionButton.Visibility = Visibility.Visible;
                HideButton.Visibility = Visibility.Collapsed;
                ListButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                ActionButton.Visibility = Visibility.Collapsed;
                HideButton.Visibility = maximum > 1 ? Visibility.Collapsed : Visibility.Visible;
                ListButton.Visibility = maximum > 1 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (message == null && !known)
            {
                _chatId = 0;
                _message = null;

                _loading = false;
                ShowHide(false);
                return;
            }

            if (message != null || known)
            {
                ShowHide(true);
            }

            var title = Strings.PinnedMessage + (value >= 0 && maximum > 1 && value + 1 < maximum ? $" #{value + 1}" : "");

            if (_loading || (_chatId == chat.Id && _message == null))
            {
                _textVisual = _textVisual == _textVisual1 ? _textVisual2 : _textVisual1;
                UpdateMessage(message, message == null, title);

                Line.UpdateIndex(value, maximum, 0);

                _chatId = chat.Id;
                _message = message;

                _loading = known;
                return;
            }
            else if (_chatId == chat.Id && _message?.Id == message?.Id)
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
            var prev = _message?.Id < message?.Id;

            Line.UpdateIndex(value, maximum, prev ? 1 : -1);
            TitleLabel.Text = title;

            _chatId = chat.Id;
            _message = message;

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
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            var parent = ElementCompositionPreview.GetElementVisual(_parent);
            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

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
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -48, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }

        public event RoutedEventHandler HideClick
        {
            add => HideButton.Click += value;
            remove => HideButton.Click -= value;
        }

        public event RoutedEventHandler ListClick
        {
            add => ListButton.Click += value;
            remove => ListButton.Click -= value;
        }

        public event RoutedEventHandler ActionClick
        {
            add => ActionButton.Click += value;
            remove => ActionButton.Click -= value;
        }

        #region Overrides

        private static readonly CornerRadius _defaultRadius = new CornerRadius(2);

        protected override void HideThumbnail()
        {
            if (ThumbRoot != null)
            {
                ThumbRoot.Visibility = Visibility.Collapsed;
            }
        }

        protected override void ShowThumbnail(CornerRadius radius = default)
        {
            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
            {
                ThumbRoot.Visibility = Visibility.Visible;
            }

            ThumbRoot.CornerRadius =
                ThumbEllipse.CornerRadius = radius == default ? _defaultRadius : radius;
        }

        protected override void SetThumbnail(ImageSource value)
        {
            if (ThumbImage != null)
            {
                ThumbImage.ImageSource = value;
            }
        }

        protected override void SetText(IClientService clientService, bool outgoing, MessageSender sender, string title, string service, FormattedText text, bool quote, bool white)
        {
            _alternativeText = title + ": ";
            TitleLabel.Text = title;

            var serviceShow = _textVisual == _textVisual1 ? ServiceLabel2 : ServiceLabel1;
            serviceShow.Text = service;

            if (!string.IsNullOrEmpty(service))
            {
                _alternativeText += service;

                if (!string.IsNullOrEmpty(text?.Text))
                {
                    _alternativeText += ", " + text.Text;
                }
            }
            else if (!string.IsNullOrEmpty(text?.Text))
            {
                _alternativeText += text.Text;
            }

            if (!string.IsNullOrEmpty(text?.Text) && !string.IsNullOrEmpty(service))
            {
                serviceShow.Text += ", ";
            }

            var messageShow = _textVisual == _textVisual1 ? MessageLabel2 : MessageLabel1;
            var labelShow = _textVisual == _textVisual1 ? TextLabel2 : TextLabel1;
            messageShow.Inlines.Clear();

            if (text != null)
            {
                var clean = text.ReplaceSpoilers();
                var previous = 0;

                if (text.Entities != null)
                {
                    foreach (var entity in clean.Entities)
                    {
                        if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                        {
                            continue;
                        }

                        if (entity.Offset > previous)
                        {
                            messageShow.Inlines.Add(new Run { Text = clean.Text.Substring(previous, entity.Offset - previous) });
                        }

                        //MessageLabel.Inlines.Add(new Run { Text = clean.Substring(entity.Offset, entity.Length), FontFamily = BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily });

                        var player = new CustomEmojiIcon();
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                        player.Style = BootStrapper.Current.Resources["MessageCustomEmojiStyle"] as Style;

                        var inline = new InlineUIContainer();
                        inline.Child = new CustomEmojiContainer(labelShow, player, -2);

                        messageShow.Inlines.Add(inline);
                        messageShow.Inlines.Add(Icons.ZWJ);

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Text.Length > previous)
                {
                    messageShow.Inlines.Add(new Run { Text = clean.Text.Substring(previous) });
                }
            }
        }

        #endregion
    }

    public class MessagePinnedAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private readonly MessagePinned _owner;

        public MessagePinnedAutomationPeer(MessagePinned owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetNameCore();
        }
    }

    public class MessagePinnedLine : Control
    {
        private readonly CompositionSpriteShape _back;
        private readonly CompositionSpriteShape _fore;
        private readonly CompositionRectangleGeometry _forePath;
        private readonly CompositionGeometricClip _mask;
        private readonly CompositionPathGeometry _maskPath;

        public MessagePinnedLine()
        {
            RegisterPropertyChangedCallback(BorderBrushProperty, OnBorderBrushChanged);

            var compositor = Window.Current.Compositor;

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(2, 36);

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
            var maskShape = compositor.CreateGeometricClip(mask);

            visual.Shapes.Add(backShape);
            visual.Shapes.Add(foreShape);
            visual.Clip = maskShape;

            _back = backShape;
            _fore = foreShape;
            _forePath = fore;
            _mask = maskShape;
            _maskPath = mask;

            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        #region Stroke

        private long _strokeToken;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(MessagePinnedLine), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as MessagePinnedLine;
            var solid = e.NewValue as SolidColorBrush;

            if (e.OldValue is SolidColorBrush old && sender._strokeToken != 0)
            {
                old.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, sender._strokeToken);
            }

            if (solid == null || sender._fore == null)
            {
                return;
            }

            sender._fore.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
            sender._strokeToken = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, sender.OnStrokeChanged);
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _fore == null)
            {
                return;
            }

            _fore.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

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

        private readonly Queue<(int, int, int)> _queue = new Queue<(int, int, int)>();
        private bool _playing;

        private int _nextValue;


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
                h = (36f - (maximum - 1) * m) / maximum;
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
                anim1.InsertKeyFrame(0, new Vector2(0, initial1 + 2), easing);
                anim1.InsertKeyFrame(1, new Vector2(0, final1 + 2), easing);

                _mask.StartAnimation("Offset", anim1);
            }
            else
            {
                _mask.Offset = new Vector2(0, final1 + 2);
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
                h = (36f - (maximum - 1) * m) / maximum;
            }

            var geometries = new CanvasGeometry[4];

            for (int i = 0; i < geometries.Length; i++)
            {
                geometries[i] = CanvasGeometry.CreateRectangle(null, 0, 0 + i * (h + m), 4, h);
            }

            var rectangle = CanvasGeometry.CreateRectangle(null, -2, -2, 8, (h + m) * geometries.Length + 1);
            return new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding));
        }

    }
}
