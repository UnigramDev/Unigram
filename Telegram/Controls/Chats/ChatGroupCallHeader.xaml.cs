//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

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
