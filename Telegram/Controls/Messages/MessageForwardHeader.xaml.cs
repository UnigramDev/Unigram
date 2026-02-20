//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public partial class MessageForwardHeader : HyperlinkButton
    {
        private Visual _visual;
        private CompositionGeometricClip _clip;
        private double _width;

        private MessageViewModel _message;
        private bool _light;

        public MessageForwardHeader()
        {
            DefaultStyleKey = typeof(MessageForwardHeader);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageForwardHeaderAutomationPeer(this);
        }

        public string GetAutomationName()
        {
            if (ForwardLabel != null)
            {
                return ForwardLabel.Text;
            }

            return null;
        }

        protected override bool GoToElementStateCore(string stateName, bool useTransitions)
        {
            return base.GoToElementStateCore(stateName, useTransitions);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            if (_width != ActualWidth || _visual?.Clip == null)
            {
                _width = ActualWidth;

                var shapes = new List<IList<Rect>>();
                var current = new List<Rect>();
                var last = default(Rect);

                var entities = new[]
                {
                    new TextStylePart
                    {
                        Offset = ForwardText.Text.Length,
                        Length = ForwardLink.Text.Length,
                        Type = TextStyle.Bold
                    }
                };

                var rectangles2 = PlaceholderHelper.Foreground.LineMetrics(ForwardLabel.Text, entities, 12, double.MaxValue, false);

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

                    var rect = new Rect(left - 4, line.Key, right - left + 8, bottom - line.Key);

                    if (current.Count > 0 && !rect.IntersectsOrTouches(last))
                    {
                        shapes.Add(current);
                        current = new List<Rect>();
                    }

                    current.Add(rect);
                    last = rect;
                }

                if (current.Count > 0)
                {
                    shapes.Add(current);
                }

                if (_visual?.Clip == null)
                {
                    _visual ??= ElementCompositionPreview.GetElementVisual(this);
                    _visual.Clip = _clip = _visual.Compositor.CreateGeometricClip();
                }

                _clip.Geometry = BootStrapper.Current.Compositor.CreatePathGeometry(PlaceholderHelper.Foreground.GetRoundedPolygon(shapes));
            }

            try
            {
                base.OnPointerEntered(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
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
            _width = 0;

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

            _visual?.Clip = null;

            if (!_templateApplied || message == null)
            {
                return;
            }

            if (message.Content is MessageAsyncStory story && message.ClientService.TryGetChat(story.StoryPosterChatId, out Chat storyChat))
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
                ForwardPhoto.Source = ProfilePictureSource.Chat(message.ClientService, storyChat);

                Visibility = Visibility.Visible;
            }
            else if (message.ForwardInfo != null && !message.IsVerificationCode && (!message.IsSaved || !message.ForwardInfo.HasSameOrigin()))
            {
                string line1;
                string line2 = null;

                if (message.ForwardInfo.PublicServiceAnnouncementType.Length > 0)
                {
                    var type = LocaleService.Current.GetString("PsaMessage_" + message.ForwardInfo.PublicServiceAnnouncementType);
                    if (type.Length > 0)
                    {
                        line1 = type;
                    }
                    else
                    {
                        line1 = Strings.PsaMessageDefault;
                    }
                }
                else
                {
                    line1 = Strings.ForwardedFrom;
                }

                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    line2 = fromUserUser.FullName();
                    ForwardLink.FontWeight = FontWeights.SemiBold;
                    ForwardPhoto.Source = ProfilePictureSource.User(message.ClientService, fromUserUser);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    line2 = fromChatChat.Title;
                    ForwardLink.FontWeight = FontWeights.SemiBold;
                    ForwardPhoto.Source = ProfilePictureSource.Chat(message.ClientService, fromChatChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    line2 = fromChannelChat.Title;
                    ForwardLink.FontWeight = FontWeights.SemiBold;
                    ForwardPhoto.Source = ProfilePictureSource.Chat(message.ClientService, fromChannelChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser fromHiddenUser)
                {
                    line2 = fromHiddenUser.SenderName;
                    ForwardLink.FontWeight = FontWeights.Normal;
                    ForwardPhoto.Source = ProfilePictureSourceText.GetNameForUser(fromHiddenUser.SenderName, long.MinValue);
                }
                else if (message.ImportInfo != null)
                {
                    line2 = message.ImportInfo.SenderName;
                    ForwardLink.FontWeight = FontWeights.Normal;
                    ForwardPhoto.Source = ProfilePictureSourceText.GetNameForUser(message.ImportInfo.SenderName, long.MinValue);
                }

                ForwardText.Text = line1;
                ForwardLink.Text = "\uEA4F\u00A0" + (line2 ?? string.Empty);

                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }

    public partial class MessageForwardHeaderAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private readonly MessageForwardHeader _owner;

        public MessageForwardHeaderAutomationPeer(MessageForwardHeader owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetAutomationName() ?? base.GetNameCore();
        }
    }
}
