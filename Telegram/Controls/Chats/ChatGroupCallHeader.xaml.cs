//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatGroupCallHeader : HyperlinkButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private readonly DispatcherTimer _scheduledTimer;

        private UIElement _parent;
        private GroupCall _call;

        public ChatGroupCallHeader()
        {
            InitializeComponent();

            _scheduledTimer = new DispatcherTimer();
            _scheduledTimer.Tick += OnTick;
            _scheduledTimer.Interval = TimeSpan.FromSeconds(1);
        }

        private void OnTick(object sender, object e)
        {
            if (_call != null && _call.ScheduledStartDate != 0)
            {
                JoinButton.Content = _call.GetStartsIn();
            }
            else
            {
                _scheduledTimer.Stop();
            }
        }

        public void InitializeParent(UIElement parent)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(_parent = parent, true);
        }

        private void RecentUsers_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            if (ViewModel.ClientService.TryGetUser(sender, out User user))
            {
                photo.SetUser(ViewModel.ClientService, user, 28);
            }
            else if (ViewModel.ClientService.TryGetChat(sender, out Chat chat))
            {
                photo.SetChat(ViewModel.ClientService, chat, 28);
            }
            else
            {
                photo.Clear();
            }
        }

        public bool UpdateGroupCall(Chat chat, GroupCall call)
        {
            var visible = true;
            var channel = call?.IsRtmpStream is true || (chat.Type is ChatTypeSupergroup super && super.IsChannel);

            //if (chat.VideoChat.GroupCallId != call?.Id || !chat.VideoChat.HasParticipants || call == null || call.IsJoined)
            //{
            //    ShowHide(false);
            //    visible = false;
            //}
            //else
            if (call != null && chat.VideoChat.GroupCallId == call.Id && ((chat.VideoChat.HasParticipants && !(call.IsJoined || call.NeedRejoin)) || call.ScheduledStartDate > 0))
            {
                ShowHide(true);

                TitleLabel.Text = call.ScheduledStartDate > 0 && call.Title.Length > 0 ? call.Title : channel ? Strings.VoipChannelVoiceChat : Strings.VoipGroupVoiceChat;
                ServiceLabel.Text = call.ParticipantCount > 0 ? Locale.Declension(Strings.R.Participants, call.ParticipantCount) : Strings.MembersTalkingNobody;

                if (call.ScheduledStartDate != 0)
                {
                    var date = Formatter.ToLocalTime(call.ScheduledStartDate);
                    var duration = date - DateTime.Now;

                    if (duration.TotalDays < 1)
                    {
                        _scheduledTimer.Start();
                    }
                    else
                    {
                        _scheduledTimer.Stop();
                    }

                    TitleLabel.Text = call.Title.Length > 0 ? call.Title : channel ? Strings.VoipChannelScheduledVoiceChat : Strings.VoipGroupScheduledVoiceChat;

                    JoinButton.Background = BootStrapper.Current.Resources["VideoChatPurpleBrush"] as Brush;
                    JoinButton.Content = call.GetStartsIn();
                }
                else
                {
                    _scheduledTimer.Stop();

                    TitleLabel.Text = channel ? Strings.VoipChannelVoiceChat : Strings.VoipGroupVoiceChat;

                    JoinButton.Background = BootStrapper.Current.Resources["StartButtonBackground"] as Brush;
                    JoinButton.Content = Strings.VoipChatJoin;
                }

                if (call.HasHiddenListeners)
                {
                    RecentUsers.Items.Clear();
                }
                else
                {
                    if (RecentUsers.Items.Count > 0 && _call?.Id == call.Id)
                    {
                        RecentUsers.Items.ReplaceDiff(call.RecentSpeakers.Select(x => x.ParticipantId));
                    }
                    else
                    {
                        RecentUsers.Items.Clear();
                        RecentUsers.Items.AddRange(call.RecentSpeakers.Select(x => x.ParticipantId));
                    }
                }
            }
            else
            {
                ShowHide(false);
                visible = false;
            }

            _call = call;
            return visible;
        }

        public void UpdateChatActions(IDictionary<int, ChatAction> actions)
        {
            if (actions != null && actions.Count > 0)
            {
                //MessageLabel.Text = InputChatActionManager.GetSpeakingString(null, actions);
            }
            else
            {
                MessageLabel.Text = string.Empty;
            }
        }

        private bool _collapsed = true;

        public void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            var parent = ElementComposition.GetElementVisual(_parent);
            var visual = ElementComposition.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_collapsed)
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 40);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }

        public event RoutedEventHandler JoinClick
        {
            add
            {
                Click += value;
                JoinButton.Click += value;
            }
            remove
            {
                Click -= value;
                JoinButton.Click -= value;
            }
        }
    }
}
