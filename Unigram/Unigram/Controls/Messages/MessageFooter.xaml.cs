using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public sealed class MessageFooter : Control
    {
        private MessageTicksState _ticksState;

        private MessageViewModel _message;

        public MessageFooter()
        {
            DefaultStyleKey = typeof(MessageFooter);

            // Due to an UWP bug we can't have composition geometries here due to Pointer*ThemeAnimations:
            // https://github.com/microsoft/WindowsCompositionSamples/issues/329
            //if (ApiInfo.IsBuildOrGreater(19041))
            //{
            //    InitializeTicks();
            //}
        }

        #region InitializeComponent

        private TextBlock Label;
        private ToolTip ToolTip;
        private Run PinnedGlyph;
        private Run RepliesGlyph;
        private Run RepliesLabel;
        private Run ViewsGlyph;
        private Run ViewsLabel;
        private Run EditedLabel;
        private Run DateLabel;
        private Run StateLabel;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            ToolTip = GetTemplateChild(nameof(ToolTip)) as ToolTip;
            PinnedGlyph = GetTemplateChild(nameof(PinnedGlyph)) as Run;
            RepliesGlyph = GetTemplateChild(nameof(RepliesGlyph)) as Run;
            RepliesLabel = GetTemplateChild(nameof(RepliesLabel)) as Run;
            ViewsGlyph = GetTemplateChild(nameof(ViewsGlyph)) as Run;
            ViewsLabel = GetTemplateChild(nameof(ViewsLabel)) as Run;
            EditedLabel = GetTemplateChild(nameof(EditedLabel)) as Run;
            DateLabel = GetTemplateChild(nameof(DateLabel)) as Run;
            StateLabel = GetTemplateChild(nameof(StateLabel)) as Run;

            ToolTip.Opened += ToolTip_Opened;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateMessageState(message);
            UpdateMessageDate(message);
            UpdateMessageEdited(message);
            UpdateMessageIsPinned(message);
            //ConvertInteractionInfo(message);
        }

        private void UpdateMessageDate(MessageViewModel message)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                DateLabel.Text = Converter.Date(sendAtDate.SendDate);
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                DateLabel.Text = string.Empty;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginMessageImport)
            {
                var original = Utils.UnixTimestampToDateTime(message.ForwardInfo.Date);
                var date = Converter.ShortDate.Format(original);
                var time = Converter.ShortTime.Format(original);

                DateLabel.Text = string.Format("{0}, {1} {2} {3}", date, time, "Imported", Converter.Date(message.Date));
            }
            else
            {
                DateLabel.Text = Converter.Date(message.Date);
            }
        }

        public void Mockup(bool outgoing, DateTime date)
        {
            DateLabel.Text = Converter.ShortTime.Format(date);
            StateLabel.Text = outgoing ? "\u00A0\u00A0\uE601" : string.Empty;
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            if (message.InteractionInfo?.ReplyInfo?.ReplyCount > 0 && !message.IsChannelPost)
            {
                RepliesGlyph.Text = "\uE93E\u00A0\u00A0";
                RepliesLabel.Text = $"{message.InteractionInfo.ReplyInfo.ReplyCount}   ";
            }
            else
            {
                RepliesGlyph.Text = string.Empty;
                RepliesLabel.Text = string.Empty;
            }

            var views = string.Empty;

            if (message.InteractionInfo?.ViewCount > 0)
            {
                views = Converter.ShortNumber(message.InteractionInfo.ViewCount);
                views += "   ";
            }

            if (message.IsChannelPost && !string.IsNullOrEmpty(message.AuthorSignature))
            {
                views += $"{message.AuthorSignature}, ";
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel && !string.IsNullOrEmpty(fromChannel.AuthorSignature))
            {
                views += $"{fromChannel.AuthorSignature}, ";
            }

            ViewsGlyph.Text = message.InteractionInfo?.ViewCount > 0 ? "\uE607\u00A0\u00A0" : string.Empty;
            ViewsLabel.Text = views;
        }

        public void UpdateMessageEdited(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            //var message = ViewModel;
            //var bot = false;
            //if (message.From != null)
            //{
            //    bot = message.From.IsBot;
            //}

            var bot = false;
            if (message.ProtoService.TryGetUser(message.Sender, out User senderUser))
            {
                bot = senderUser.Type is UserTypeBot;
            }

            EditedLabel.Text = message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard ? $"{Strings.Resources.EditedMessage}\u00A0\u2009" : string.Empty;
        }

        public void UpdateMessageIsPinned(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            if (message.IsPinned)
            {
                PinnedGlyph.Text = "\uE93F\u00A0\u00A0\u00A0";
            }
            else
            {
                PinnedGlyph.Text = string.Empty;
            }
        }

        public void UpdateMessageState(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            StateLabel.Text = UpdateStateIcon(message);
        }

        private string UpdateStateIcon(MessageViewModel message)
        {
            if (message.IsOutgoing && !message.IsChannelPost && !message.IsSaved())
            {
                var maxId = 0L;

                var chat = message.GetChat();
                if (chat != null)
                {
                    maxId = chat.LastReadOutboxMessageId;
                }

                if (message.SendingState is MessageSendingStateFailed)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Failed;

                    // TODO: 
                    return "\u00A0\u00A0failed"; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Pending;
                    return "\u00A0\u00A0\uE600"; // Pending
                }
                else if (message.Id <= maxId)
                {
                    UpdateTicks(true, _ticksState == MessageTicksState.Sent);

                    _ticksState = MessageTicksState.Read;
                    return _container != null ? "\u00A0\u00A0\uE603" : "\u00A0\u00A0\uE601"; // Read
                }

                UpdateTicks(false, _ticksState == MessageTicksState.Pending);

                _ticksState = MessageTicksState.Sent;
                return _container != null ? "\u00A0\u00A0\uE603" : "\u00A0\u00A0\uE602"; // Unread
            }

            UpdateTicks(null);

            _ticksState = MessageTicksState.None;
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
                var dateTime = Converter.DateTime(sendAtTime.SendDate);
                var date = Converter.LongDate.Format(dateTime);
                var time = Converter.LongTime.Format(dateTime);

                text = $"{date} {time}";
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                text = Strings.Resources.MessageScheduledUntilOnline;
            }
            else
            {
                var dateTime = Converter.DateTime(message.Date);
                var date = Converter.LongDate.Format(dateTime);
                var time = Converter.LongTime.Format(dateTime);

                text = $"{date} {time}";
            }

            var bot = false;
            if (message.ProtoService.TryGetUser(message.Sender, out User senderUser))
            {
                bot = senderUser.Type is UserTypeBot;
            }

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard)
            {
                var edit = Converter.DateTime(message.EditDate);
                var editDate = Converter.LongDate.Format(edit);
                var editTime = Converter.LongTime.Format(edit);

                text += $"\r\n{Strings.Resources.EditedMessage}: {editDate} {editTime}";
            }

            DateTime? original = null;
            if (message.ForwardInfo != null)
            {
                original = Converter.DateTime(message.ForwardInfo.Date);
            }

            if (original != null)
            {
                var originalDate = Converter.LongDate.Format(original.Value);
                var originalTime = Converter.LongTime.Format(original.Value);

                text += $"\r\n{Strings.Additional.OriginalMessage}: {originalDate} {originalTime}";
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

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(MessageFooter), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as MessageFooter;
            var solid = e.NewValue as SolidColorBrush;

            if (solid == null || sender._container == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            foreach (var shape in sender._shapes)
            {
                shape.StrokeBrush = brush;
            }
        }


        #endregion

        private void InitializeTicks()
        {
            var width = 18f;
            var height = 10f;
            var stroke = 2f;
            var distance = stroke * 2;

            var sqrt = (float)Math.Sqrt(2);

            var side = (stroke / sqrt) / 2f;
            var diagonal = height * sqrt;
            var length = (diagonal / 2f) / sqrt;

            var join = stroke / 2 * sqrt;

            var line11 = Window.Current.Compositor.CreateLineGeometry();
            var line12 = Window.Current.Compositor.CreateLineGeometry();

            line11.Start = new Vector2(width - height + side + join - length - distance, height - side - length);
            line11.End = new Vector2(width - height + side + join - distance, height - side);

            line12.Start = new Vector2(width - height + side - distance, height - side);
            line12.End = new Vector2(width - side - distance, side);

            var shape11 = Window.Current.Compositor.CreateSpriteShape(line11);
            shape11.StrokeThickness = 2;
            shape11.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);
            shape11.IsStrokeNonScaling = true;

            var shape12 = Window.Current.Compositor.CreateSpriteShape(line12);
            shape12.StrokeThickness = 2;
            shape12.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);
            shape12.IsStrokeNonScaling = true;

            var visual1 = Window.Current.Compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape12);
            visual1.Shapes.Add(shape11);
            visual1.Size = new Vector2(width, height);
            visual1.CenterPoint = new Vector3(width, height / 2f, 0);


            var line21 = Window.Current.Compositor.CreateLineGeometry();
            var line22 = Window.Current.Compositor.CreateLineGeometry();

            line21.Start = new Vector2(width - height + side + join - length, height - side - length);
            line21.End = new Vector2(width - height + side + join, height - side);

            line22.Start = new Vector2(width - height + side, height - side);
            line22.End = new Vector2(width - side, side);

            var shape21 = Window.Current.Compositor.CreateSpriteShape(line21);
            shape21.StrokeThickness = 2;
            shape21.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);

            var shape22 = Window.Current.Compositor.CreateSpriteShape(line22);
            shape22.StrokeThickness = 2;
            shape22.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);

            var visual2 = Window.Current.Compositor.CreateShapeVisual();
            visual2.Shapes.Add(shape22);
            visual2.Shapes.Add(shape21);
            visual2.Size = new Vector2(width, height);


            var container = Window.Current.Compositor.CreateSpriteVisual();
            container.Children.InsertAtTop(visual2);
            container.Children.InsertAtTop(visual1);
            container.Size = new Vector2(width, height);

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

        private void UpdateTicks(bool? read, bool animate = false)
        {
            if (_container == null)
            {
                return;
            }

            if (read == null)
            {
                _container.IsVisible = false;
            }
            else if (animate)
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

        private void AnimateTicks(bool read)
        {
            _container.IsVisible = true;

            var height = 10f;
            var stroke = 2f;

            var sqrt = (float)Math.Sqrt(2);

            var diagonal = height * sqrt;
            var length = (diagonal / 2f) / sqrt;

            var duration = 250;
            var percent = stroke / length;

            var linear = Window.Current.Compositor.CreateLinearEasingFunction();

            var anim11 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim11.InsertKeyFrame(0, 0);
            anim11.InsertKeyFrame(1, 1, linear);
            anim11.Duration = TimeSpan.FromMilliseconds(duration - (percent * duration));

            var anim12 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(0, 0);
            anim12.InsertKeyFrame(1, 1);
            anim12.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            anim12.DelayTime = anim11.Duration;
            anim12.Duration = TimeSpan.FromMilliseconds(400);

            var anim22 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim22.InsertKeyFrame(0, new Vector3(1));
            anim22.InsertKeyFrame(0.2f, new Vector3(1.1f));
            anim22.InsertKeyFrame(1, new Vector3(1));
            anim22.Duration = anim11.Duration + anim12.Duration;

            if (read)
            {
                _line11.StartAnimation("TrimEnd", anim11);
                _line12.StartAnimation("TrimEnd", anim12);
                _visual1.StartAnimation("Scale", anim22);

                var anim21 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
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

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_container != null)
            {
                _container.Offset = new Vector3((float)Label.DesiredSize.Width - 18, 4, 0);
            }

            return base.ArrangeOverride(finalSize);
        }

        #endregion
    }
}
