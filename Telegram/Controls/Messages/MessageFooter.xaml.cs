//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public sealed partial class MessageFooter : ControlEx
    {
        private MessageTicksState _ticksState;
        private long _ticksHash;

        private bool _outgoing;

        private string _effectGlyph;
        private string _pinnedGlyph;
        private string _repliesLabel;
        private string _viewsLabel;
        private string _editedLabel;
        private string _authorLabel;
        private string _dateLabel;

        private MessageViewModel _message;

        public MessageFooter()
        {
            DefaultStyleKey = typeof(MessageFooter);
        }

        protected override void OnLoaded()
        {
            _strokeBrush?.Register();

            if (_message?.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed)
            {
                ToastPopup.Show(this, Strings.VideoConversionTimeInfo, ToastPlacementMode.Top, dismissAfter: TimeSpan.FromSeconds(3));
            }
        }

        protected override void OnUnloaded()
        {
            _strokeBrush?.Unregister();
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
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            ToolTip = GetTemplateChild(nameof(ToolTip)) as ToolTip;

            ToolTip.Opened += ToolTip_Opened;

            _templateApplied = true;
            TemplateApplied?.Invoke(this, EventArgs.Empty);

            if (_message != null)
            {
                UpdateMessageImpl(_message, true);
            }
        }

        public event EventHandler TemplateApplied;

        #endregion

        private void UpdateLabel()
        {
            Label?.Text = _effectGlyph + _pinnedGlyph + _repliesLabel + _viewsLabel + _editedLabel + _authorLabel + _dateLabel;
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
            UpdateMessageState(message);
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
                    Effect ??= GetTemplateChild(nameof(Effect)) as AnimatedImage;
                    Effect.Visibility = Visibility.Visible;

                    Effect.Source = new DelayedFileSource(message.ClientService, message.Effect.StaticIcon);
                }
                else
                {
                    _effectGlyph = message.Effect.Emoji + " ";
                    Effect?.Visibility = Visibility.Collapsed;

                    Effect?.Source = null;
                }
            }
            else
            {
                _effectGlyph = string.Empty;
                Effect?.Visibility = Visibility.Collapsed;

                Effect?.Source = null;
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
                if (sendAtDate.RepeatPeriod != 0)
                {
                    var repeat = sendAtDate.RepeatPeriod switch
                    {
                        86400 => Strings.MessageScheduledRepeatDaily,
                        7 * 86400 => Strings.MessageScheduledRepeatWeekly,
                        14 * 86400 => Strings.MessageScheduledRepeatBiweekly,
                        30 * 86400 => Strings.MessageScheduledRepeatMonthly,
                        91 * 86400 => Locale.Declension(Strings.R.MessageScheduledRepeatMonthlyMany, 3),
                        182 * 86400 => Locale.Declension(Strings.R.MessageScheduledRepeatMonthlyMany, 6),
                        365 * 86400 => Strings.MessageScheduledRepeatYearly,
                        _ => string.Empty
                    };

                    _dateLabel = string.Format(Strings.formatDateAtTime, repeat, Formatter.Time(sendAtDate.SendDate));
                }
                else
                {
                    _dateLabel = Formatter.Time(sendAtDate.SendDate);
                }
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed sendWhenVideoProcessed)
            {
                _dateLabel = string.Format(Strings.ScheduledTimeApprox, Formatter.Time(sendWhenVideoProcessed.SendDate));
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

                if (message.PaidMessageStarCount > 0)
                {
                    _dateLabel = string.Format("{1} {2}, {0}", _dateLabel, Icons.Premium, Formatter.ShortNumber(message.PaidMessageStarCount));
                }
            }
            else
            {
                _dateLabel = string.Empty;
            }
        }

        public void Mockup(bool outgoing, DateTime date)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(outgoing, date);
                }

                TemplateApplied += loaded;
                return;
            }

            _dateLabel = Formatter.Time(date);
            UpdateLabel();
            UpdateTicks(outgoing, outgoing ? MessageTicksState.Read : MessageTicksState.None);
            UpdateMessageOutgoing(outgoing);
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
                _ticksState = MessageTicksState.None;
                _ticksHash = 0;
                return;
            }

            var outgoing = (message.IsOutgoing && !message.IsChannelPost) || (message.IsSaved && message.ForwardInfo?.Source is { IsOutgoing: true });
            if (outgoing)
            {
                var maxId = message.LastReadOutboxMessageId;
                var messageHash = message.ChatId ^ message.Id;

                if (message.SendingState is MessageSendingStateFailed)
                {
                    UpdateMessageOutgoing(true);
                    UpdateTicks(true, MessageTicksState.Failed);

                    _ticksState = MessageTicksState.Failed;
                    _ticksHash = messageHash;

                    return; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateMessageOutgoing(true);
                    UpdateTicks(true, MessageTicksState.Pending);

                    _ticksState = MessageTicksState.Pending;
                    _ticksHash = messageHash;

                    return; // Pending
                }
                else if (message.Id <= maxId)
                {
                    UpdateMessageOutgoing(true);
                    UpdateTicks(true, MessageTicksState.Read, _ticksState == MessageTicksState.Sent && _ticksHash == messageHash);

                    _ticksState = MessageTicksState.Read;
                    _ticksHash = messageHash;

                    return; // Read
                }

                UpdateMessageOutgoing(true);
                UpdateTicks(true, MessageTicksState.Sent, _ticksState == MessageTicksState.Pending && _ticksHash == messageHash);

                _ticksState = MessageTicksState.Sent;
                _ticksHash = messageHash;

                return; // Unread
            }

            UpdateMessageOutgoing(false);
            UpdateTicks(false, MessageTicksState.None);

            _ticksState = MessageTicksState.None;
            _ticksHash = 0;
        }

        private void UpdateMessageOutgoing(bool outgoing)
        {
            if (_outgoing != outgoing)
            {
                _outgoing = outgoing;
                Label.Padding = new Thickness(0, 0, outgoing ? 22 : 0, 0);
            }
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
                var date = Formatter.Date(sendAtTime.SendDate, "DATE_LONGDATE");
                var time = Formatter.Time(sendAtTime.SendDate);

                text = string.Format(Strings.formatDateAtTime, date, time);
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed sendWhenVideoProcessed)
            {
                text = Strings.VideoConversionTimeInfo;
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                text = Strings.MessageScheduledUntilOnline;
            }
            else
            {
                var date = Formatter.Date(message.Date, "DATE_LONGDATE");
                var time = Formatter.Time(message.Date);

                text = string.Format(Strings.formatDateAtTime, date, time);
            }

            var bot = false;
            if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                bot = senderUser.Type is UserTypeBot;
            }

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard)
            {
                text += "\r\n" + Formatter.EditDate(message.EditDate);
            }
            else if (message.ForwardInfo != null && !message.IsSaved && !message.IsVerificationCode)
            {
                text += Environment.NewLine + Formatter.ForwardDate(message.ForwardInfo.Date);
            }

            tooltip.Content = text;
        }

        #region Animation

        private float _trimStart;

        private CompositionGeometry _line1;
        private CompositionShape _shape1;

        private CompositionGeometry _line2;
        private CompositionShape _shape2;

        private CompositionShape _pending;
        private CompositionShape _failed;

        private ShapeVisual _container;

        #region Stroke

        private CompositionColorSource _strokeBrush;

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
            _strokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        private void InitializePending()
        {
            if (_container == null)
            {
                InitializeTicks();
            }

            var stroke = 1.33f;

            static CompositionPath GetPending()
            {
                var stroke = 1.33f;
                var radius = 5 - stroke / 2;

                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.AddGeometry(CanvasGeometry.CreateEllipse(null, 13, 5, radius, radius));
                    builder.BeginFigure(new Vector2(12.5f, 3f));
                    builder.AddLine(new Vector2(12.5f, 5.5f));
                    builder.AddLine(new Vector2(15f, 5.5f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var shape11 = BootStrapper.Current.Compositor.CreateSpriteShape(BootStrapper.Current.Compositor.CreatePathGeometry(GetPending()));
            shape11.StrokeThickness = stroke;
            shape11.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape11.IsStrokeNonScaling = true;
            shape11.StrokeStartCap = CompositionStrokeCap.Round;
            shape11.StrokeEndCap = CompositionStrokeCap.Round;
            shape11.Offset = new Vector2(2, 1);

            _pending = shape11;
            _container.Shapes.Add(shape11);
        }

        private void InitializeFailed()
        {
            if (_container == null)
            {
                InitializeTicks();
            }

            var stroke = 1.33f;

            static CompositionPath GetFailed()
            {
                var stroke = 1.33f;
                var center = stroke / 2;

                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.AddGeometry(CanvasGeometry.CreateRoundedRectangle(null, 4 + center, center, 11 - stroke, 11 - stroke, 2, 2));
                    builder.BeginFigure(new Vector2(9.5f, 3f));
                    builder.AddLine(new Vector2(9.5f, 5.5f + center));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    builder.BeginFigure(new Vector2(9.5f, 7f + stroke));
                    builder.AddLine(new Vector2(9.5f, 7f + stroke + 0.001f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var shape11 = BootStrapper.Current.Compositor.CreateSpriteShape(BootStrapper.Current.Compositor.CreatePathGeometry(GetFailed()));
            shape11.StrokeThickness = stroke;
            shape11.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(Colors.Red);
            shape11.IsStrokeNonScaling = true;
            shape11.StrokeStartCap = CompositionStrokeCap.Round;
            shape11.StrokeEndCap = CompositionStrokeCap.Round;
            shape11.Offset = new Vector2(2, 0);

            _failed = shape11;
            _container.Shapes.Add(shape11);
        }

        private void InitializeTicks()
        {
            var width = 18f;
            var height = 10f;
            var stroke = 1.33f;
            var distance = 4;

            var sqrt = MathF.Sqrt(2);

            var cap = stroke / 2f;
            var join = stroke / 2 * sqrt;

            CompositionPath GetLine()
            {
                var right = width - distance;
                var vertex = new Vector2(right - height + join, height - join);
                var start = new Vector2(cap, vertex.Y - (vertex.X - cap));
                var end = new Vector2(right - cap, cap);

                var trimStart = Vector2.Distance(start, vertex);
                var trimEnd = Vector2.Distance(vertex, end);

                _trimStart = trimStart / (trimStart + trimEnd);

                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.BeginFigure(start);
                    builder.AddLine(vertex);
                    builder.AddLine(end);
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var geometry = GetLine();
            var line1 = BootStrapper.Current.Compositor.CreatePathGeometry(geometry);
            var line2 = BootStrapper.Current.Compositor.CreatePathGeometry(geometry);

            var shape1 = BootStrapper.Current.Compositor.CreateSpriteShape(line1);
            shape1.StrokeThickness = stroke;
            shape1.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape1.IsStrokeNonScaling = true;
            shape1.StrokeStartCap = CompositionStrokeCap.Round;
            shape1.StrokeEndCap = CompositionStrokeCap.Round;
            shape1.StrokeLineJoin = CompositionStrokeLineJoin.Round;
            shape1.CenterPoint = new Vector2(width, height / 2f);
            shape1.Offset = new Vector2(2, 1);

            var shape2 = BootStrapper.Current.Compositor.CreateSpriteShape(line2);
            shape2.StrokeThickness = stroke;
            shape2.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape2.IsStrokeNonScaling = true;
            shape2.StrokeStartCap = CompositionStrokeCap.Round;
            shape2.StrokeEndCap = CompositionStrokeCap.Round;
            shape2.StrokeLineJoin = CompositionStrokeLineJoin.Round;
            shape2.CenterPoint = new Vector2(width, height / 2f);
            shape2.Offset = new Vector2(distance + 2, 1);

            var container = BootStrapper.Current.Compositor.CreateShapeVisual();
            container.Shapes.Add(shape2);
            container.Shapes.Add(shape1);
            container.Size = new Vector2(width + 2, height + 1);
            container.AnchorPoint = new Vector2(1, 0);
            container.Offset = new Vector3(0, 3, 0);
            container.RelativeOffsetAdjustment = new Vector3(1, 0, 0);

            ElementCompositionPreview.SetElementChildVisual(Label, container);

            _line1 = line1;
            _shape1 = shape1;
            _line2 = line2;
            _shape2 = shape2;
            _container = container;
        }

        private void UpdateTicks(bool outgoing, MessageTicksState state, bool animate = false)
        {
            void RemoveAdditionalVisuals(bool visible)
            {
                if (_pending != null && state != MessageTicksState.Pending)
                {
                    _container.Shapes.Remove(_pending);
                    _pending = null;
                }
                else if (_failed != null && state != MessageTicksState.Failed)
                {
                    _container.Shapes.Remove(_failed);
                    _failed = null;
                }

                _line1.TrimStart = visible ? 0 : 1;
                _line2.TrimStart = visible ? 0 : 1;
            }

            if (state is not MessageTicksState.Sent and not MessageTicksState.Read)
            {
                if (outgoing)
                {
                    if (_pending == null && state == MessageTicksState.Pending)
                    {
                        InitializePending();
                    }
                    else if (_failed == null && state == MessageTicksState.Failed)
                    {
                        InitializeFailed();
                    }
                }

                if (_container != null)
                {
                    RemoveAdditionalVisuals(false);
                }
            }
            else
            {
                if (_container == null)
                {
                    InitializeTicks();
                }

                RemoveAdditionalVisuals(true);

                if (animate)
                {
                    AnimateTicks(state == MessageTicksState.Read);
                }
                else
                {
                    _line1.TrimEnd = state == MessageTicksState.Read ? 1 : 0;
                    _line2.TrimStart = state == MessageTicksState.Read ? _trimStart : 0;
                }
            }
        }

        private void AnimateTicks(bool read)
        {
            var duration = 250;

            var linear = BootStrapper.Current.Compositor.CreateLinearEasingFunction();
            var start = duration - _trimStart * duration;
            var ended = 400f;

            var scale = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector2(1));
            scale.InsertKeyFrame(0.2f, new Vector2(1.1f));
            scale.InsertKeyFrame(1, new Vector2(1));
            scale.Duration = TimeSpan.FromMilliseconds(start + ended);

            if (read)
            {
                var trimEnd = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimEnd.InsertKeyFrame(0, 0);
                trimEnd.InsertKeyFrame(start / (start + ended), _trimStart, linear);
                trimEnd.InsertKeyFrame(1, 1);
                trimEnd.Duration = TimeSpan.FromMilliseconds(start + ended);

                var trimStart = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimStart.InsertKeyFrame(0, 0);
                trimStart.InsertKeyFrame(1, _trimStart, linear);
                trimStart.Duration = TimeSpan.FromMilliseconds(start);

                _line1.StartAnimation("TrimEnd", trimEnd);
                _line2.StartAnimation("TrimStart", trimStart);
                _shape1.StartAnimation("Scale", scale);
            }
            else
            {
                var trimEnd = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimEnd.InsertKeyFrame(0, 0);
                trimEnd.InsertKeyFrame(1, 1);
                trimEnd.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                trimEnd.DelayTime = TimeSpan.FromMilliseconds(start);
                trimEnd.Duration = TimeSpan.FromMilliseconds(ended);

                _line1.TrimEnd = 0;
                _line2.TrimStart = 0;

                _line2.StartAnimation("TrimEnd", trimEnd);
                _shape2.StartAnimation("Scale", scale);
            }
        }

        #endregion
    }
}
