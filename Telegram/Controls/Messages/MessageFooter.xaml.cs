//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;

namespace Telegram.Controls.Messages
{
    public sealed partial class MessageFooter : ControlEx
    {
        private MessageTicksState _ticksState;
        private long _ticksHash;

        private string _effectGlyph;
        private string _pinnedGlyph;
        private string _repliesLabel;
        private string _viewsLabel;
        private string _editedLabel;
        private string _authorLabel;
        private string _dateLabel;
        private string _stateLabel;

        private MessageViewModel _message;

        public MessageFooter()
        {
            DefaultStyleKey = typeof(MessageFooter);

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_strokeToken == 0 && _shapes != null)
            {
                Stroke?.RegisterColorChangedCallback(OnStrokeChanged, ref _strokeToken);
                OnStrokeChanged(Stroke, SolidColorBrush.ColorProperty);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Stroke?.UnregisterColorChangedCallback(ref _strokeToken);
        }

        #region InitializeComponent

        private AnimatedImage Effect;
        private Popup InteractionsPopup;
        private Grid Interactions;
        private TextBlock Label;
        private ToolTip ToolTip;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Effect = GetTemplateChild(nameof(Effect)) as AnimatedImage;
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            ToolTip = GetTemplateChild(nameof(ToolTip)) as ToolTip;

            ToolTip.Opened += ToolTip_Opened;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessageImpl(_message, true);
            }
        }

        #endregion

        private void UpdateLabel()
        {
            if (Label != null)
            {
                Label.Text = _effectGlyph + _pinnedGlyph + _repliesLabel + _viewsLabel + _editedLabel + _authorLabel + _dateLabel + _stateLabel;
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateMessageImpl(message, false);
        }

        private void UpdateMessageImpl(MessageViewModel message, bool fromApplyTemplate)
        {
            UpdateMessageStateImpl(message);
            UpdateMessageDateImpl(message);
            UpdateMessageEditedImpl(message);
            UpdateMessageIsPinnedImpl(message);
            UpdateMessageEffectImpl(message, fromApplyTemplate);

            // UpdateMessageInteractionInfo is always invoked by MessageBubble.UpdateMessage

            if (fromApplyTemplate)
            {
                UpdateMessageInteractionInfoImpl(message);
                UpdateLabel();
            }
        }

        public void UpdateMessageEffect(MessageViewModel message)
        {
            UpdateMessageEffectImpl(message, false);
        }

        public void UpdateMessageEffectImpl(MessageViewModel message, bool fromApplyTemplate)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (message.Effect != null)
            {
                if (message.Effect.StaticIcon != null)
                {
                    _effectGlyph = string.Empty;
                    Effect.Visibility = Visibility.Visible;

                    Effect.Source = new DelayedFileSource(message.ClientService, message.Effect.StaticIcon);
                }
                else
                {
                    _effectGlyph = message.Effect.Emoji + " ";
                    Effect.Visibility = Visibility.Collapsed;

                    Effect.Source = null;
                }
            }
            else
            {
                _effectGlyph = string.Empty;
                Effect.Visibility = Visibility.Collapsed;

                Effect.Source = null;
            }

            if (!fromApplyTemplate)
            {
                UpdateLabel();
            }
        }

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            PlayMessageEffect(_message);
            base.OnTapped(e);
        }

        public bool PlayMessageEffect(MessageViewModel message)
        {
            if (message?.Effect?.Type is MessageEffectTypeEmojiReaction emojiReaction)
            {
                return PlayInteraction(message, emojiReaction.EffectAnimation.StickerValue);
            }
            else if (message?.Effect?.Type is MessageEffectTypePremiumSticker premiumSticker && premiumSticker.Sticker.FullType is StickerFullTypeRegular regular)
            {
                return PlayInteraction(message, regular.PremiumAnimation);
            }

            return true;
        }

        public bool PlayInteraction(MessageViewModel message, File interaction)
        {
            if (Interactions == null)
            {
                InteractionsPopup = GetTemplateChild(nameof(InteractionsPopup)) as Popup;
                Interactions = GetTemplateChild(nameof(Interactions)) as Grid;
            }

            //message.Interaction = null;

            var file = interaction;
            if (file.Local.IsDownloadingCompleted && Interactions.Children.Count < 4)
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var height = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                var player = new AnimatedImage();
                player.Width = height * 3;
                player.Height = height * 3;
                //player.IsFlipped = !message.IsOutgoing;
                player.LoopCount = 1;
                player.IsHitTestVisible = false;
                player.FrameSize = new Size(512, 512);
                player.AutoPlay = true;
                player.Source = new LocalFileSource(file);
                player.LoopCompleted += (s, args) =>
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        Interactions.Children.Remove(player);
                        Interactions.Children.Clear();

                        if (Interactions.Children.Count > 0)
                        {
                            return;
                        }

                        InteractionsPopup.IsOpen = false;
                    });
                };

                if (message.IsChannelPost || !message.IsOutgoing)
                {
                    player.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                    player.RenderTransform = new ScaleTransform
                    {
                        ScaleX = -1
                    };
                }

                var left = height * 3 * 0.18;
                var right = height * 3 * 0.82;
                var top = height * 3 / 2 + 8;
                var bottom = height * 3 / 2 - 8;

                if (message.IsOutgoing)
                {
                    player.Margin = new Thickness(-right, -top, -left, -bottom);
                }
                else
                {
                    player.Margin = new Thickness(-left, -top, -right, -bottom);
                }

                Interactions.Children.Add(player);
                Interactions.Width = 4;
                Interactions.Height = 4;
                InteractionsPopup.IsOpen = true;

                return true;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                //message.Interaction = interaction;
                message.Delegate.DownloadFile(message, file);

                //UpdateManager.Subscribe(this, message, file, ref _interactionToken, UpdateFile, true);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMessageDateImpl(MessageViewModel message)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                _dateLabel = Formatter.Time(sendAtDate.SendDate);
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                _dateLabel = string.Empty;
            }
            else if (message.ImportInfo != null)
            {
                var original = Formatter.ToLocalTime(message.ImportInfo.Date);
                var date = Formatter.Date(original);
                var time = Formatter.Time(original);

                _dateLabel = string.Format("{0}, {1} {2} {3}", date, time, "Imported", Formatter.Time(message.Date));
            }
            else if (message.Date > 0)
            {
                _dateLabel = Formatter.Time(message.Date);
            }
            else
            {
                _dateLabel = string.Empty;
            }
        }

        public void Mockup(bool outgoing, DateTime date)
        {
            _dateLabel = Formatter.Time(date);
            _stateLabel = outgoing ? "\u00A0\uE603" : string.Empty;
            UpdateLabel();
            UpdateTicks(outgoing, outgoing ? true : null);
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateMessageInteractionInfoImpl(message);
            UpdateLabel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMessageInteractionInfoImpl(MessageViewModel message)
        {
            if (message.InteractionInfo?.ReplyInfo?.ReplyCount > 0 && !message.IsChannelPost)
            {
                _repliesLabel = $"\uEA02\u00A0" + message.InteractionInfo.ReplyInfo.ReplyCount + "\u00A0";
            }
            else
            {
                _repliesLabel = string.Empty;
            }

            if (message.IsChannelPost && (message.SenderId.IsChat(message.ChatId) || !message.HasSenderPhoto) && !string.IsNullOrEmpty(message.AuthorSignature))
            {
                _authorLabel = $"{message.AuthorSignature}, ";
            }
            else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && !string.IsNullOrEmpty(fromChannel.AuthorSignature))
            {
                _authorLabel = $"{fromChannel.AuthorSignature}, ";
            }
            else if (message.SenderBusinessBotUserId != 0 && message.ClientService.TryGetUser(message.SenderBusinessBotUserId, out User senderBusinessBotUser))
            {
                _authorLabel = $"{senderBusinessBotUser.FirstName}, ";
            }
            else
            {
                _authorLabel = string.Empty;
            }

            if (message.InteractionInfo?.ViewCount > 0)
            {
                _viewsLabel = "\uEA03\u00A0" + Formatter.ShortNumber(message.InteractionInfo.ViewCount) + "\u00A0";
            }
            else
            {
                _viewsLabel = string.Empty;
            }
        }

        public void UpdateMessageEdited(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateMessageEditedImpl(message);
            UpdateLabel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMessageEditedImpl(MessageViewModel message)
        {
            if (message.EditDate != 0)
            {
                var bot = false;
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    bot = senderUser.Type is UserTypeBot;
                }

                _editedLabel = message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard ? $"{Strings.EditedMessage}\u00A0\u2009" : string.Empty;
            }
            else
            {
                _editedLabel = string.Empty;
            }
        }

        public void UpdateMessageIsPinned(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateMessageIsPinnedImpl(message);
            UpdateLabel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMessageIsPinnedImpl(MessageViewModel message)
        {
            if (message.IsPinned)
            {
                _pinnedGlyph = "\uEA05\u00A0";
            }
            else
            {
                _pinnedGlyph = string.Empty;
            }
        }

        public void UpdateMessageState(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateMessageStateImpl(message);
            UpdateLabel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMessageStateImpl(MessageViewModel message)
        {
            _stateLabel = UpdateStateIcon(message);
        }

        private string UpdateStateIcon(MessageViewModel message)
        {
            if (message.IsOutgoing && !message.IsChannelPost && !message.IsSaved)
            {
                var maxId = 0L;
                var messageHash = message.ChatId ^ message.Id;

                var chat = message.Chat;
                if (chat != null)
                {
                    maxId = chat.LastReadOutboxMessageId;
                }

                if (message.SendingState is MessageSendingStateFailed)
                {
                    UpdateTicks(true, null);

                    _ticksState = MessageTicksState.Failed;
                    _ticksHash = messageHash;

                    // TODO: 
                    return Icons.LTR + "\u00A0failed"; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateTicks(true, null);

                    _ticksState = MessageTicksState.Pending;
                    _ticksHash = messageHash;

                    return Icons.LTR + "\u00A0\uEA06"; // Pending
                }
                else if (message.Id <= maxId)
                {
                    UpdateTicks(true, true, _ticksState == MessageTicksState.Sent && _ticksHash == messageHash);

                    _ticksState = MessageTicksState.Read;
                    _ticksHash = messageHash;

                    return Icons.LTR + "\u00A0\uEA07"; // Read
                }

                UpdateTicks(true, false, _ticksState == MessageTicksState.Pending && _ticksHash == messageHash);

                _ticksState = MessageTicksState.Sent;
                _ticksHash = messageHash;

                return Icons.LTR + "\u00A0\uEA07"; // Unread
            }

            UpdateTicks(false, null);

            _ticksState = MessageTicksState.None;
            _ticksHash = 0;

            return string.Empty;
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var tooltip = sender as ToolTip;
            if (tooltip == null)
            {
                return;
            }

            string text;
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtTime)
            {
                var dateTime = Formatter.ToLocalTime(sendAtTime.SendDate);
                var date = Formatter.LongDate.Format(dateTime);
                var time = Formatter.LongTime.Format(dateTime);

                text = $"{date} {time}";
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                text = Strings.MessageScheduledUntilOnline;
            }
            else
            {
                var dateTime = Formatter.ToLocalTime(message.Date);
                var date = Formatter.LongDate.Format(dateTime);
                var time = Formatter.LongTime.Format(dateTime);

                text = $"{date} {time}";
            }

            var bot = false;
            if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                bot = senderUser.Type is UserTypeBot;
            }

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard)
            {
                var edit = Formatter.ToLocalTime(message.EditDate);
                var editDate = Formatter.LongDate.Format(edit);
                var editTime = Formatter.LongTime.Format(edit);

                text += $"\r\n{Strings.EditedMessage}: {editDate} {editTime}";
            }

            DateTime? original = null;
            if (message.ForwardInfo != null)
            {
                original = Formatter.ToLocalTime(message.ForwardInfo.Date);
            }

            if (original != null)
            {
                var originalDate = Formatter.LongDate.Format(original.Value);
                var originalTime = Formatter.LongTime.Format(original.Value);

                text += $"\r\n{Strings.CropOriginal}: {originalDate} {originalTime}";
            }

            tooltip.Content = text;
        }

        #region Animation

        private CompositionGeometry _line11;
        private CompositionGeometry _line12;
        private ShapeVisual _visual1;

        private CompositionGeometry _line21;
        private CompositionGeometry _line22;
        private ShapeVisual _visual2;

        private CompositionSpriteShape[] _shapes;

        private SpriteVisual _container;

        #region Stroke

        private long _strokeToken;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(MessageFooter), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageFooter)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            if (oldValue != null && _strokeToken != 0)
            {
                oldValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }

            if (newValue == null || _container == null)
            {
                return;
            }

            var brush = BootStrapper.Current.Compositor.CreateColorBrush(newValue.Color);

            foreach (var shape in _shapes)
            {
                shape.StrokeBrush = brush;
            }

            _strokeToken = newValue.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _shapes == null)
            {
                return;
            }

            var brush = BootStrapper.Current.Compositor.CreateColorBrush(solid.Color);

            foreach (var shape in _shapes)
            {
                shape.StrokeBrush = brush;
            }
        }

        #endregion

        private void InitializeTicks()
        {
            var width = 18f;
            var height = 10f;
            var stroke = 1.33f;
            var distance = 4;

            var sqrt = MathF.Sqrt(2);

            var side = stroke / sqrt / 2f;
            var diagonal = height * sqrt;
            var length = diagonal / 2f / sqrt;

            var join = stroke / 2 * sqrt;

            var line11 = BootStrapper.Current.Compositor.CreateLineGeometry();
            var line12 = BootStrapper.Current.Compositor.CreateLineGeometry();

            line11.Start = new Vector2(width - height + side + join - length - distance, height - side - length);
            line11.End = new Vector2(width - height + side + join - distance, height - side);

            line12.Start = new Vector2(width - height + side - distance, height - side);
            line12.End = new Vector2(width - side - distance, side);

            var shape11 = BootStrapper.Current.Compositor.CreateSpriteShape(line11);
            shape11.StrokeThickness = stroke;
            shape11.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape11.IsStrokeNonScaling = true;
            shape11.StrokeStartCap = CompositionStrokeCap.Round;

            var shape12 = BootStrapper.Current.Compositor.CreateSpriteShape(line12);
            shape12.StrokeThickness = stroke;
            shape12.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape12.IsStrokeNonScaling = true;
            shape12.StrokeEndCap = CompositionStrokeCap.Round;

            var visual1 = BootStrapper.Current.Compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape12);
            visual1.Shapes.Add(shape11);
            visual1.Size = new Vector2(width, height);
            visual1.CenterPoint = new Vector3(width, height / 2f, 0);


            var line21 = BootStrapper.Current.Compositor.CreateLineGeometry();
            var line22 = BootStrapper.Current.Compositor.CreateLineGeometry();

            line21.Start = new Vector2(width - height + side + join - length, height - side - length);
            line21.End = new Vector2(width - height + side + join, height - side);

            line22.Start = new Vector2(width - height + side, height - side);
            line22.End = new Vector2(width - side, side);

            var shape21 = BootStrapper.Current.Compositor.CreateSpriteShape(line21);
            shape21.StrokeThickness = stroke;
            shape21.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape21.StrokeStartCap = CompositionStrokeCap.Round;

            var shape22 = BootStrapper.Current.Compositor.CreateSpriteShape(line22);
            shape22.StrokeThickness = stroke;
            shape22.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape22.StrokeEndCap = CompositionStrokeCap.Round;

            var visual2 = BootStrapper.Current.Compositor.CreateShapeVisual();
            visual2.Shapes.Add(shape22);
            visual2.Shapes.Add(shape21);
            visual2.Size = new Vector2(width, height);


            var container = BootStrapper.Current.Compositor.CreateSpriteVisual();
            container.Children.InsertAtTop(visual2);
            container.Children.InsertAtTop(visual1);
            container.Size = new Vector2(width, height);
            container.AnchorPoint = new Vector2(1, 0);
            container.Offset = new Vector3(0, 4, 0);
            container.RelativeOffsetAdjustment = new Vector3(1, 0, 0);

            ElementCompositionPreview.SetElementChildVisual(Label, container);

            _line11 = line11;
            _line12 = line12;
            _line21 = line21;
            _line22 = line22;
            _shapes = new[] { shape11, shape12, shape21, shape22 };
            _visual1 = visual1;
            _visual2 = visual2;
            _container = container;
        }

        private void UpdateTicks(bool outgoing, bool? read, bool animate = false)
        {
            if (read == null)
            {
                if (outgoing)
                {
                    InitializeTicks();
                }

                if (_container != null)
                {
                    _container.IsVisible = false;
                }
            }
            else
            {
                if (_container == null)
                {
                    InitializeTicks();
                }

                if (animate)
                {
                    AnimateTicks(read == true);
                }
                else
                {
                    _line11.TrimEnd = read == true ? 1 : 0;
                    _line12.TrimEnd = read == true ? 1 : 0;

                    _line21.TrimStart = read == true ? 1 : 0;

                    _container.IsVisible = true;
                }
            }
        }

        private CompositionBrush GetBrush(DependencyProperty dp, ref long token, DependencyPropertyChangedCallback callback)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                if (token == 0)
                {
                    token = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, callback);
                }

                return BootStrapper.Current.Compositor.CreateColorBrush(solid.Color);
            }

            return BootStrapper.Current.Compositor.CreateColorBrush(Colors.Black);
        }

        private void AnimateTicks(bool read)
        {
            _container.IsVisible = true;

            var height = 10f;
            var stroke = 2f;

            var sqrt = (float)Math.Sqrt(2);

            var diagonal = height * sqrt;
            var length = diagonal / 2f / sqrt;

            var duration = 250;
            var percent = stroke / length;

            var linear = BootStrapper.Current.Compositor.CreateLinearEasingFunction();

            var anim11 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim11.InsertKeyFrame(0, 0);
            anim11.InsertKeyFrame(1, 1, linear);
            anim11.Duration = TimeSpan.FromMilliseconds(duration - percent * duration);

            var anim12 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(0, 0);
            anim12.InsertKeyFrame(1, 1);
            anim12.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            anim12.DelayTime = anim11.Duration;
            anim12.Duration = TimeSpan.FromMilliseconds(400);

            var anim22 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim22.InsertKeyFrame(0, new Vector3(1));
            anim22.InsertKeyFrame(0.2f, new Vector3(1.1f));
            anim22.InsertKeyFrame(1, new Vector3(1));
            anim22.Duration = anim11.Duration + anim12.Duration;

            if (read)
            {
                _line11.StartAnimation("TrimEnd", anim11);
                _line12.StartAnimation("TrimEnd", anim12);
                _visual1.StartAnimation("Scale", anim22);

                var anim21 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim21.InsertKeyFrame(0, 0);
                anim21.InsertKeyFrame(1, 1, linear);
                anim11.Duration = TimeSpan.FromMilliseconds(duration);

                _line21.StartAnimation("TrimStart", anim21);
            }
            else
            {
                _line11.TrimEnd = 0;
                _line12.TrimEnd = 0;

                _line21.TrimStart = 0;

                _line21.StartAnimation("TrimEnd", anim11);
                _line22.StartAnimation("TrimEnd", anim12);
                _visual2.StartAnimation("Scale", anim22);
            }
        }

        #endregion
    }
}
