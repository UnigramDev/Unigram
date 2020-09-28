using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessageFooter : ContentPresenter
    {
        private bool _ticksState;

        private MessageViewModel _mesage;

        public MessageFooter()
        {
            InitializeComponent();

            // Due to an UWP bug we can't have composition geometries here due to Pointer*ThemeAnimations:
            // https://github.com/microsoft/WindowsCompositionSamples/issues/329
            // InitializeAnimation();
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _mesage = message;

            ConvertState(message);
            ConvertDate(message);
            ConvertEdited(message);
            //ConvertInteractionInfo(message);
        }

        public void UpdateMessageState(MessageViewModel message)
        {
            ConvertState(message);
        }

        public void UpdateMessageEdited(MessageViewModel message)
        {
            ConvertEdited(message);
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            ConvertInteractionInfo(message);
        }

        public void ConvertDate(MessageViewModel message)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                DateLabel.Text = BindConvert.Current.Date(sendAtDate.SendDate);
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                DateLabel.Text = string.Empty;
            }
            else
            {
                DateLabel.Text = BindConvert.Current.Date(message.Date);
            }
        }

        public void Mockup(bool outgoing, DateTime date)
        {
            DateLabel.Text = BindConvert.Current.ShortTime.Format(date);
            StateLabel.Text = outgoing ? "\u00A0\u00A0\uE601" : string.Empty;
        }

        public void ConvertInteractionInfo(MessageViewModel message)
        {
            if (message.InteractionInfo?.ReplyCount > 0 && !message.IsChannelPost)
            {
                RepliesGlyph.Text = "\uE93E\u00A0\u00A0";
                RepliesLabel.Text = $"{message.InteractionInfo.ReplyCount}   ";
            }
            else
            {
                RepliesGlyph.Text = string.Empty;
                RepliesLabel.Text = string.Empty;
            }

            var views = string.Empty;

            if (message.InteractionInfo?.ViewCount > 0)
            {
                views = BindConvert.ShortNumber(message.InteractionInfo.ViewCount);
                views += "   ";
            }

            if (message.IsChannelPost && !string.IsNullOrEmpty(message.AuthorSignature))
            {
                views += $"{message.AuthorSignature}, ";
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel forwardedPost && !string.IsNullOrEmpty(forwardedPost.AuthorSignature))
            {
                views += $"{forwardedPost.AuthorSignature}, ";
            }

            ViewsGlyph.Text = message.InteractionInfo?.ViewCount > 0 ? "\uE607\u00A0\u00A0" : string.Empty;
            ViewsLabel.Text = views;
        }

        private void ConvertEdited(MessageViewModel message)
        {
            //var message = ViewModel;
            //var bot = false;
            //if (message.From != null)
            //{
            //    bot = message.From.IsBot;
            //}

            var bot = false;

            var sender = message.GetSenderUser();
            if (sender != null && sender.Type is UserTypeBot)
            {
                bot = true;
            }

            EditedLabel.Text = message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && !(message.ReplyMarkup is ReplyMarkupInlineKeyboard) ? $"{Strings.Resources.EditedMessage}\u00A0\u2009" : string.Empty;
        }

        private void ConvertState(MessageViewModel message)
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

                    _ticksState = false;
                    StateLabel.Text = "\u00A0\u00A0failed";
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateTicks(null);

                    _ticksState = false;
                    StateLabel.Text = "\u00A0\u00A0\uE600";
                }
                else if (message.Id <= maxId)
                {
                    UpdateTicks(true, _ticksState);

                    _ticksState = false;
                    StateLabel.Text = _container != null ? "\u00A0\u00A0\uE603" : "\u00A0\u00A0\uE601";
                }
                else
                {
                    UpdateTicks(false);

                    _ticksState = true;
                    StateLabel.Text = _container != null ? "\u00A0\u00A0\uE603" : "\u00A0\u00A0\uE602";
                }
            }
            else
            {
                UpdateTicks(null);

                _ticksState = false;
                StateLabel.Text = string.Empty;
            }
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var message = _mesage;
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
                var dateTime = BindConvert.Current.DateTime(sendAtTime.SendDate);
                var date = BindConvert.Current.LongDate.Format(dateTime);
                var time = BindConvert.Current.LongTime.Format(dateTime);

                text = $"{date} {time}";
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                text = Strings.Resources.MessageScheduledUntilOnline;
            }
            else
            {
                var dateTime = BindConvert.Current.DateTime(message.Date);
                var date = BindConvert.Current.LongDate.Format(dateTime);
                var time = BindConvert.Current.LongTime.Format(dateTime);

                text = $"{date} {time}";
            }

            var bot = false;
            var user = message.GetSenderUser();
            if (user != null)
            {
                bot = user.Type is UserTypeBot;
            }

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && !(message.ReplyMarkup is ReplyMarkupInlineKeyboard))
            {
                var edit = BindConvert.Current.DateTime(message.EditDate);
                var editDate = BindConvert.Current.LongDate.Format(edit);
                var editTime = BindConvert.Current.LongTime.Format(edit);

                text += $"\r\n{Strings.Resources.EditedMessage}: {editDate} {editTime}";
            }

            DateTime? original = null;
            if (message.ForwardInfo != null)
            {
                original = BindConvert.Current.DateTime(message.ForwardInfo.Date);
            }

            if (original != null)
            {
                var originalDate = BindConvert.Current.LongDate.Format(original.Value);
                var originalTime = BindConvert.Current.LongTime.Format(original.Value);

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

        private CompositionSpriteShape[] _shapes;

        private ContainerVisual _container;

        #region Stroke

        public Brush Stroke
        {
            get { return (Brush)GetValue(StrokeProperty); }
            set { SetValue(StrokeProperty, value); }
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

        private void InitializeAnimation()
        {
            if (!ApiInfo.CanUseDirectComposition)
            {
                return;
            }

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
            visual1.Size = new Vector2(18, 10);
            visual1.CenterPoint = new Vector3(18, 5, 0);


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
            visual2.Size = new Vector2(18, 10);


            var container = Window.Current.Compositor.CreateContainerVisual();
            container.Children.InsertAtTop(visual2);
            container.Children.InsertAtTop(visual1);
            container.Size = new Vector2(18, 10);

            ElementCompositionPreview.SetElementChildVisual(Label, container);

            _line11 = line11;
            _line12 = line12;
            _line21 = line21;
            _line22 = line22;
            _shapes = new[] { shape11, shape12, shape21, shape22 };
            _visual1 = visual1;
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
            else if (read == true && animate)
            {
                AnimateTicks();
            }
            else
            {
                _line11.TrimEnd = read == true ? 1 : 0;
                _line12.TrimEnd = read == true ? 1 : 0;

                _line21.TrimStart = read == true ? 1 : 0;

                _container.IsVisible = true;
            }
        }

        private void AnimateTicks()
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
            anim12.DelayTime = anim11.Duration;
            anim12.Duration = TimeSpan.FromMilliseconds(400);

            _line11.StartAnimation("TrimEnd", anim11);
            _line12.StartAnimation("TrimEnd", anim12);

            var anim21 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim21.InsertKeyFrame(0, 0);
            anim21.InsertKeyFrame(1, 1, linear);
            anim11.Duration = TimeSpan.FromMilliseconds(duration);

            var anim22 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim22.InsertKeyFrame(0, new Vector3(1));
            anim22.InsertKeyFrame(0.2f, new Vector3(1.1f));
            anim22.InsertKeyFrame(1, new Vector3(1));
            anim22.Duration = anim11.Duration + anim12.Duration;

            _line21.StartAnimation("TrimStart", anim21);
            _visual1.StartAnimation("Scale", anim22);
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
