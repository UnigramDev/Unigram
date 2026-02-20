//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatPinnedMessage : MessageReferenceBase
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private ChatView _chatView;

        private readonly Visual _textVisual1;
        private readonly Visual _textVisual2;

        private Visual _textVisual;

        private long _chatId;
        private new MessageViewModel _message;

        private bool _animate;

        private string _alternativeText;

        public ChatPinnedMessage()
        {
            InitializeComponent();

            this.CreateInsetClip();

            _collapsed = new SlidePanel.SlideState(this, false, 48);

            ElementCompositionPreview.SetIsTranslationEnabled(ContentRoot, true);

            _textVisual1 = ElementComposition.GetElementVisual(TextLabel1);
            _textVisual2 = ElementComposition.GetElementVisual(TextLabel2);

            _textVisual = _textVisual1;

            _templateApplied = true;

            Unloaded += OnUnloaded;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatPinnedMessageAutomationPeer(this);
        }

        public string GetNameCore()
        {
            return _alternativeText;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _chatId = 0;
            _message = null;
            _loading = false;
            _animate = false;

            _collapsed.Collapse();
        }

        public float AnimatedHeight => _collapsed ? 0 : 48;

        public void InitializeParent(ChatView chatView)
        {
            _chatView = chatView;
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
            else if (message?.Content is MessageText { LinkPreview: LinkPreview { Type: LinkPreviewTypeGroupCall } })
            {
                ActionButton.Content = Strings.VoipChatJoin;
                ActionButton.Visibility = Visibility.Visible;
                HideButton.Visibility = Visibility.Collapsed;
                ListButton.Visibility = Visibility.Collapsed;
            }
            else if (message != null || known)
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
                ShowHide(chat.BusinessBotManageBar == null);
            }

            if (value < 0)
            {
                value = maximum - 1;
            }
            else if (maximum <= value)
            {
                maximum = value + 1;
            }

            var title = Strings.PinnedMessage + (value >= 0 && maximum > 1 && value + 1 < maximum ? $" #{value + 1}" : "");

            if (_loading || (_chatId == chat.Id && _message == null))
            {
                _chatId = chat.Id;
                _message = message;

                _animate = _loading != (_message == null);
                _loading = known;

                _textVisual = _textVisual == _textVisual1 ? _textVisual2 : _textVisual1;
                UpdateMessage(message, message == null, title);

                Line.UpdateIndex(value, maximum, 0);
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

            var cross = _chatId == chat.Id;
            var prev = _message?.Id < message?.Id;

            Line.UpdateIndex(value, maximum, cross ? prev ? 1 : -1 : 0);
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

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
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
                var hide1 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                hide1.InsertKeyFrame(0, new Vector3(0));
                hide1.InsertKeyFrame(1, new Vector3(0, prev ? -8 : 8, 0));

                textVisualHide.StartAnimation("Offset", hide1);
            }
            else
            {
                textVisualHide.Offset = Vector3.Zero;
            }

            var hide2 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            textVisualHide.StartAnimation("Opacity", hide2);

            UpdateMessage(message, message == null, title);
            //referenceShow.IsTabStop = true;
            //referenceHide.IsTabStop = false;

            if (cross)
            {
                var show1 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(0, new Vector3(0, prev ? 8 : -8, 0));
                show1.InsertKeyFrame(1, new Vector3(0));

                textVisualShow.StartAnimation("Offset", show1);
            }
            else
            {
                textVisualShow.Offset = Vector3.Zero;
            }

            var show2 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(0, 0);
            show2.InsertKeyFrame(1, 1);

            textVisualShow.StartAnimation("Opacity", show2);
            batch.End();

            _textVisual = textVisualShow;
        }

        private SlidePanel.SlideState _collapsed;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed.IsVisible = show;
            _chatView.UpdateMessagesHeaderPadding();
        }

        public IEnumerable<UIElement> GetAnimatableVisuals()
        {
            if (_collapsed)
            {
                yield break;
            }

            yield return ActionButton.Visibility == Visibility.Visible ? ActionButton : ListButton.Visibility == Visibility.Visible ? ListButton : HideButton;
        }


        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.HidePinnedMessage();
        }

        private void ListButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenPinnedMessages();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.ReplyMarkup is ReplyMarkupInlineKeyboard inlineKeyboard)
            {
                ViewModel.OpenInlineButton(Message, inlineKeyboard.Rows[0][0]);
            }
            else if (Message?.Content is MessageText { LinkPreview: LinkPreview linkPreview } && linkPreview.Type is LinkPreviewTypeGroupCall)
            {
                MessageHelper.NavigateToGroupCall(ViewModel.ClientService, ViewModel.NavigationService, new InputGroupCallLink(linkPreview.Url));
            }
        }

        #region Overrides

        private static readonly CornerRadius _defaultRadius = new(2);

        protected override void HideThumbnail()
        {
            _thumbnailController?.Recycle();

            ShowHideThumbnail(false);
        }

        protected override ImageBrush ShowThumbnail(CornerRadius radius = default)
        {
            ShowHideThumbnail(true);

            ThumbRoot.CornerRadius =
                ThumbEllipse.CornerRadius = radius == default ? _defaultRadius : radius;

            return ThumbImage;
        }

        private bool _collapsedThumbnail = true;

        private void ShowHideThumbnail(bool show)
        {
            if (_collapsedThumbnail != show)
            {
                return;
            }

            _collapsedThumbnail = !show;
            ThumbRoot.Visibility = _animate ? Visibility.Visible : show ? Visibility.Visible : Visibility.Collapsed;

            var visual = ElementComposition.GetElementVisual(ThumbRoot);
            var content = ElementComposition.GetElementVisual(ContentRoot);

            if (!_animate)
            {
                visual.Opacity = 1;
                content.Properties.InsertVector3("Translation", Vector3.Zero);

                return;
            }

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                content.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_collapsedThumbnail)
                {
                    ThumbRoot.Visibility = Visibility.Collapsed;
                }
            };

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);
            opacity.Duration = Constants.FastAnimation;

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, show ? -44 : 0);
            translation.InsertKeyFrame(1, show ? 0 : -44);
            translation.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);
            content.StartAnimation("Translation.X", translation);

            batch.End();
        }

        protected override void SetText(MessageViewModel message, bool outgoing, MessageSender sender, string title, string service, FormattedText quote, bool manual, bool white)
        {
            _alternativeText = title + ": ";
            TitleLabel.Text = title;

            var serviceShow = _textVisual == _textVisual1 ? ServiceLabel2 : ServiceLabel1;
            serviceShow.Text = service;

            if (!string.IsNullOrEmpty(service))
            {
                _alternativeText += service;

                if (!string.IsNullOrEmpty(quote?.Text ?? message?.Text?.Text))
                {
                    _alternativeText += ", " + quote?.Text ?? message?.Text?.Text;
                }
            }
            else if (!string.IsNullOrEmpty(quote?.Text ?? message?.Text?.Text))
            {
                _alternativeText += quote?.Text ?? message?.Text?.Text;
            }

            if (!string.IsNullOrEmpty(quote?.Text ?? message?.Text?.Text) && !string.IsNullOrEmpty(service))
            {
                serviceShow.Text += ", ";
            }

            var messageShow = _textVisual == _textVisual1 ? MessageLabel2 : MessageLabel1;
            var labelShow = _textVisual == _textVisual1 ? TextLabel2 : TextLabel1;

            if (quote != null)
            {
                labelShow.SetText(message?.ClientService, quote);
            }
            else
            {
                labelShow.SetText(message?.ClientService, message?.Text);
            }

            labelShow.SetQuery(string.Empty);
        }

        #endregion

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            if (ViewModel.Chat.CanPinMessages(ViewModel.ClientService))
            {
                flyout.CreateFlyoutItem(ViewModel.UnpinMessages, ViewModel.PinnedMessages.Count == 1 ? Strings.UnpinMessage2 : Strings.UnpinAllMessages2, Icons.PinOff);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.UnpinMessages, ViewModel.PinnedMessages.Count == 1 ? Strings.HidePinnedMessage2 : Strings.HidePinnedMessages2, Icons.PinOff);
            }

            flyout.ShowAt(sender, args);
        }
    }

    public partial class ChatPinnedMessageAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private readonly ChatPinnedMessage _owner;

        public ChatPinnedMessageAutomationPeer(ChatPinnedMessage owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetNameCore();
        }
    }

    public partial class ChatPinnedMessageLine : ControlEx
    {
        private readonly CompositionSpriteShape _back;
        private readonly CompositionSpriteShape _fore;
        private readonly CompositionRoundedRectangleGeometry _forePath;
        private readonly CompositionGeometricClip _mask;
        private readonly CompositionPathGeometry _maskPath;

        public ChatPinnedMessageLine()
        {
            var compositor = BootStrapper.Current.Compositor;

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(4, 48);

            var back = compositor.CreateRectangleGeometry();
            back.Offset = Vector2.Zero;
            back.Size = new Vector2(3, 48);

            var backShape = compositor.CreateSpriteShape(back);
            backShape.FillBrush = _fillBrush ??= new CompositionColorSource(Fill, IsConnected);

            // TODO: This will never render properly for some reason
            var fore = compositor.CreateRoundedRectangleGeometry();
            fore.Offset = new Vector2(0, 6);
            fore.Size = new Vector2(4, 36);
            fore.CornerRadius = new Vector2(1.5f);

            var foreShape = compositor.CreateSpriteShape(fore);
            foreShape.FillBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);

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

        protected override void OnLoaded()
        {
            _strokeBrush?.Register();
            _fillBrush?.Register();
        }

        protected override void OnUnloaded()
        {
            _strokeBrush?.Unregister();
            _fillBrush?.Unregister();
        }

        #region Stroke

        private CompositionColorSource _strokeBrush;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ChatPinnedMessageLine), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatPinnedMessageLine)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _strokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        #region Fill

        private CompositionColorSource _fillBrush;

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register("Fill", typeof(Brush), typeof(ChatPinnedMessageLine), new PropertyMetadata(null, OnFillChanged));

        private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatPinnedMessageLine)d).OnFillChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnFillChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _fillBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        private readonly Queue<(int, int, int)> _queue = new();
        private bool _playing;

        private int _nextValue;
        private int _nextMaximum;


        public void UpdateIndex(int value, int maximum, int direction)
        {
            maximum = Math.Clamp(maximum, 0, int.MaxValue);

            if (_maskPath == null || (_nextValue == value && _nextMaximum == maximum))
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
            _nextMaximum = maximum;

            var h = 12f;
            var m = 3f;

            if (maximum < 4)
            {
                h = (36f - (maximum - 1) * m) / maximum;
            }

            _forePath.Size = new Vector2(3, h);
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
                var anim1 = _mask.Compositor.CreateScalarKeyFrameAnimation();
                anim1.InsertKeyFrame(0, initial1 + 2, easing);
                anim1.InsertKeyFrame(1, final1 + 2, easing);

                _mask.StartAnimation("Offset.Y", anim1);
            }
            else
            {
                _mask.Offset = new Vector2(0, final1 + 2);
            }

            if (initial2 != final2 && maximum > 1)
            {
                var anim2 = _mask.Compositor.CreateScalarKeyFrameAnimation();
                anim2.InsertKeyFrame(0, initial2, easing);
                anim2.InsertKeyFrame(1, final2, easing);

                _fore.StartAnimation("Offset.Y", anim2);
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
                geometries[i] = CanvasGeometry.CreateRoundedRectangle(null, 0, 6 + i * (h + m), 3, h, 1.5f, 1.5f);
            }

            return new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding));
        }
    }
}
