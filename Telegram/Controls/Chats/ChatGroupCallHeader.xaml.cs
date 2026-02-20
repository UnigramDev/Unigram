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
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatGroupCallHeader : HyperlinkButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private readonly DispatcherTimer _scheduledTimer;

        private ChatView _chatView;
        private GroupCall _call;

        public ChatGroupCallHeader()
        {
            InitializeComponent();

            _collapsed = new SlidePanel.SlideState(this, false, 40);

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

        public float AnimatedHeight => _collapsed ? 0 : 40;

        public void InitializeParent(ChatView chatView)
        {
            _chatView = chatView;
        }

        private void RecentUsers_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            photo.Source = ProfilePictureSource.MessageSender(ViewModel.ClientService, sender);
        }

        public bool UpdateGroupCall(Chat chat, GroupCall call)
        {
            var visible = true;
            var activeCallId = ViewModel.VoipService.ActiveCall is VoipGroupCall groupCall ? groupCall.Id : 0;
            var joined = call != null && (call.ScheduledStartDate > 0 ? call.Id == activeCallId : (call.IsJoined || call.NeedRejoin));

            // TODO: there's currently a bug in TDLib that reports incorrect participant_count while leaving the call.

            if (chat.VideoChat.GroupCallId == call.Id && !joined && (call.ParticipantCount > 0 || call.ScheduledStartDate > 0))
            {
                ShowHide(true);

                if (call.IsRtmpStream is true || chat.Type is ChatTypeSupergroup { IsChannel: true })
                {
                    TitleLabel.Text = call.ScheduledStartDate > 0 && call.Title.Length > 0 ? call.Title : call.ScheduledStartDate != 0 ? Strings.VoipChannelScheduledVoiceChat : Strings.VoipChannelVoiceChat;
                    ServiceLabel.Text = call.ParticipantCount > 0 ? Locale.Declension(Strings.R.ViewersWatching, call.ParticipantCount) : Strings.ViewersWatchingNobody;
                }
                else
                {
                    TitleLabel.Text = call.ScheduledStartDate > 0 && call.Title.Length > 0 ? call.Title : call.ScheduledStartDate != 0 ? Strings.VoipGroupScheduledVoiceChat : Strings.VoipGroupVoiceChat;
                    ServiceLabel.Text = call.ParticipantCount > 0 ? Locale.Declension(Strings.R.Participants, call.ParticipantCount) : Strings.MembersTalkingNobody;
                }

                AutomationProperties.SetName(this, Label.Text);

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

                    JoinButtonBackground.Background = BootStrapper.Current.Resources["VideoChatPurpleBrush"] as Brush;
                    JoinButton.Content = call.GetStartsIn();
                }
                else
                {
                    _scheduledTimer.Stop();

                    JoinButtonBackground.Background = BootStrapper.Current.Resources["PillButtonBackground"] as Brush;
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
                        RecentUsers.Items.ReplaceWith(call.RecentSpeakers.Select(x => x.ParticipantId));
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

        private SlidePanel.SlideState _collapsed;

        public void ShowHide(bool show)
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

            yield return RecentUsers;
            yield return JoinButtonRoot;
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
