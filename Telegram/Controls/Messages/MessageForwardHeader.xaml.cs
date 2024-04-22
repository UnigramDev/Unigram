using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public class MessageForwardHeader : HyperlinkButton
    {
        private CompositionGeometricClip _clip;
        private double _width;

        private MessageViewModel _message;
        private bool _light;

        public MessageForwardHeader()
        {
            DefaultStyleKey = typeof(MessageForwardHeader);
        }

        protected override bool GoToElementStateCore(string stateName, bool useTransitions)
        {
            return base.GoToElementStateCore(stateName, useTransitions);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            if (_width != ActualWidth)
            {
                _width = ActualWidth;

                var minX = double.MaxValue;
                var minY = double.MaxValue;
                var maxX = double.MinValue;
                var maxY = double.MinValue;

                var shapes = new List<List<Rect>>();
                var current = new List<Rect>();
                var last = default(Rect);

                var entities = new[]
                {
                    new TextEntity(ForwardText.Text.Length, ForwardLink.Text.Length, new TextEntityTypeBold())
                };

                var rectangles2 = PlaceholderImageHelper.Current.LineMetrics(ForwardLabel.Text, entities, 12, double.MaxValue, false);

                //var contentEnd = ForwardLabel.ContentEnd.GetCharacterRect(ForwardLabel.ContentEnd.LogicalDirection);
                //if (contentEnd.Right <= 0)
                //{
                //    contentEnd = new Rect(0, contentEnd.Y, finalSize.Width, contentEnd.Height);
                //}

                //var rectangles2 = new[]
                //{
                //    new Rect(0, 0, finalSize.Width, contentEnd.Y),
                //    new Rect(0, contentEnd.Y, contentEnd.Right, contentEnd.Height)
                //};

                foreach (var line in rectangles2.GroupBy(x => x.Y))
                {
                    var left = line.Min(x => x.Left);
                    var right = line.Max(x => x.Right);
                    var bottom = line.Max(x => x.Bottom);

                    var rect = new Rect(left - 2, line.Key, right - left + 4, bottom - line.Key);

                    if (current.Count > 0 && !rect.IntersectsWith(last))
                    {
                        shapes.Add(current);
                        current = new List<Rect>();
                    }

                    current.Add(rect);
                    last = rect;

                    minX = Math.Min(minX, rect.Left);
                    minY = Math.Min(minY, rect.Top);
                    maxX = Math.Max(maxX, rect.Right);
                    maxY = Math.Max(maxY, rect.Bottom);
                }

                if (current.Count > 0)
                {
                    shapes.Add(current);
                }

                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    var angle = MathFEx.ToRadians(-90);

                    for (int j = 0; j < shapes.Count; j++)
                    {
                        var rectangles = shapes[j];

                        for (int i = 0; i < rectangles.Count; i++)
                        {
                            var rect = rectangles[i];

                            if (i == 0)
                            {
                                builder.BeginFigure(new Windows.Foundation.Point(rect.Right - 4, rect.Top).ToVector2());
                                builder.AddArc(new Windows.Foundation.Point(rect.Right, rect.Top + 4).ToVector2(), 4, 4, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                            }
                            else
                            {
                                var y1diff = i > 0 ? rect.Right - rectangles[i - 1].Right : 4;
                                var y1radius = MathF.Min(4, MathF.Abs((float)y1diff));

                                if (y1diff < 0)
                                {
                                    builder.AddLine(new Windows.Foundation.Point(rect.Right + y1radius, rect.Top).ToVector2());
                                    builder.AddArc(new Windows.Foundation.Point(rect.Right, rect.Top + y1radius).ToVector2(), y1radius, y1radius, 0, CanvasSweepDirection.CounterClockwise, CanvasArcSize.Small);
                                }
                                else if (y1diff > 0)
                                {
                                    builder.AddLine(new Windows.Foundation.Point(rect.Right - y1radius, rect.Top).ToVector2());
                                    builder.AddArc(new Windows.Foundation.Point(rect.Right, rect.Top + y1radius).ToVector2(), y1radius, y1radius, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                                }
                            }

                            var y2diff = i < rectangles.Count - 1 ? rect.Right - rectangles[i + 1].Right : 4;
                            var y2radius = MathF.Min(4, MathF.Abs((float)y2diff));

                            builder.AddLine(new Windows.Foundation.Point(rect.Right, rect.Bottom - y2radius).ToVector2());

                            if (y2diff < 0)
                            {
                                builder.AddArc(new Windows.Foundation.Point(rect.Right + y2radius, rectangles[i + 1].Top).ToVector2(), y2radius, y2radius, 0, CanvasSweepDirection.CounterClockwise, CanvasArcSize.Small);
                            }
                            else if (y2diff > 0)
                            {
                                builder.AddArc(new Windows.Foundation.Point(rect.Right - y2radius, rect.Bottom).ToVector2(), y2radius, y2radius, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                            }
                        }

                        for (int i = rectangles.Count - 1; i >= 0; i--)
                        {
                            var rect = rectangles[i];

                            var y1diff = i < rectangles.Count - 1 ? rect.Left - rectangles[i + 1].Left : -4;
                            var y1radius = MathF.Min(4, MathF.Abs((float)y1diff));

                            if (y1diff > 0)
                            {
                                builder.AddLine(new Windows.Foundation.Point(rect.Left - y1radius, rect.Bottom).ToVector2());
                                builder.AddArc(new Windows.Foundation.Point(rect.Left, rect.Bottom - y1radius).ToVector2(), y1radius, y1radius, 0, CanvasSweepDirection.CounterClockwise, CanvasArcSize.Small);
                            }
                            else if (y1diff < 0)
                            {
                                builder.AddLine(new Windows.Foundation.Point(rect.Left + y1radius, rect.Bottom).ToVector2());
                                builder.AddArc(new Windows.Foundation.Point(rect.Left, rect.Bottom - y1radius).ToVector2(), y1radius, y1radius, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                            }

                            var y2diff = i > 0 ? rect.Left - rectangles[i - 1].Left : -4;
                            var y2radius = MathF.Min(4, MathF.Abs((float)y2diff));

                            builder.AddLine(new Windows.Foundation.Point(rect.Left, rect.Top + y2radius).ToVector2());

                            if (y2diff > 0)
                            {
                                builder.AddArc(new Windows.Foundation.Point(rect.Left - y2radius, rect.Top).ToVector2(), y2radius, y2radius, angle, CanvasSweepDirection.CounterClockwise, CanvasArcSize.Small);
                            }
                            else if (y2diff < 0)
                            {
                                builder.AddArc(new Windows.Foundation.Point(rect.Left + y2radius, rect.Top).ToVector2(), y2radius, y2radius, angle, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                            }
                        }

                        builder.EndFigure(CanvasFigureLoop.Closed);
                    }

                    result = CanvasGeometry.CreatePath(builder);
                }

                if (_clip == null)
                {
                    var visual = ElementCompositionPreview.GetElementVisual(this);
                    visual.Clip = _clip = visual.Compositor.CreateGeometricClip();
                }

                _clip.Geometry = Window.Current.Compositor.CreatePathGeometry(new CompositionPath(result));
            }

            base.OnPointerEntered(e);
        }

        #region InitializeComponent

        private TextBlock ForwardLabel;
        private ProfilePicture ForwardPhoto;
        private Run ForwardText;
        private Run ForwardLink;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            ForwardLabel = GetTemplateChild(nameof(ForwardLabel)) as TextBlock;
            ForwardPhoto = GetTemplateChild(nameof(ForwardPhoto)) as ProfilePicture;

            ForwardText = ForwardLabel.Inlines[0] as Run;
            ForwardLink = ForwardLabel.Inlines[2] as Run;

            //ForwardLink.Click += FwdFrom_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message, _light);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message, bool light)
        {
            _message = message;

            if (_light != light)
            {
                _light = light;

                if (light)
                {
                    Foreground = new SolidColorBrush(Colors.White);
                }
                else
                {
                    ClearValue(ForegroundProperty);
                }
            }

            if (!_templateApplied || message == null)
            {
                return;
            }

            if (message.Content is MessageAsyncStory story)
            {
                if (message.ClientService.TryGetChat(story.StorySenderChatId, out Chat storyChat))
                {
                    if (story.State == MessageStoryState.Expired)
                    {
                        if (message.ClientService.TryGetSupergroup(storyChat, out Supergroup supergroup) && supergroup.Status is ChatMemberStatusLeft && !supergroup.IsPublic())
                        {
                            ForwardText.Text = Strings.PrivateStory;
                        }
                        else
                        {
                            ForwardText.Text = string.Format("{0}\u00A0{1}", Icons.ExpiredStory, Strings.ExpiredStory);
                        }
                    }
                    else
                    {
                        ForwardText.Text = Strings.ForwardedStory;
                    }

                    ForwardLink.Text = "\uEA4F\u00A0" + storyChat.Title;
                    ForwardLink.FontWeight = FontWeights.SemiBold;
                    ForwardPhoto.SetChat(message.ClientService, storyChat, 16);
                    ForwardPhoto.Visibility = Visibility.Visible;

                    Visibility = Visibility.Visible;
                }
            }
            else if (message.ForwardInfo != null && (!message.IsSaved || !message.ForwardInfo.HasSameOrigin()))
            {
                if (message.ForwardInfo.PublicServiceAnnouncementType.Length > 0)
                {
                    var type = LocaleService.Current.GetString("PsaMessage_" + message.ForwardInfo.PublicServiceAnnouncementType);
                    if (type.Length > 0)
                    {
                        ForwardText.Text = type;
                    }
                    else
                    {
                        ForwardText.Text = Strings.PsaMessageDefault;
                    }

                    ForwardLink.Text = string.Empty;
                }
                else
                {
                    ForwardText.Text = Strings.ForwardedFrom;
                }

                var title = string.Empty;
                var photo = true;

                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    title = fromUserUser.FullName();
                    ForwardPhoto.SetUser(message.ClientService, fromUserUser, 16);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    title = fromChatChat.Title;
                    ForwardPhoto.SetChat(message.ClientService, fromChatChat, 16);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    title = fromChannelChat.Title;
                    ForwardPhoto.SetChat(message.ClientService, fromChannelChat, 16);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser fromHiddenUser)
                {
                    title = fromHiddenUser.SenderName;
                    photo = false;
                }
                else if (message.ImportInfo != null)
                {
                    title = message.ImportInfo.SenderName;
                    photo = false;
                }

                if (photo)
                {
                    title = "\uEA4F\u00A0" + title;
                }

                ForwardLink.Text = title;
                ForwardPhoto.Visibility = photo
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }
}
