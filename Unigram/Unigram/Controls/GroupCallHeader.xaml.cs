using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class GroupCallHeader : HyperlinkButton
    {
        private readonly DispatcherTimer _scheduledTimer;

        private IProtoService _protoService;

        private UIElement _parent;
        private GroupCall _call;

        public GroupCallHeader()
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

        public void InitializeParent(UIElement parent, IProtoService protoService)
        {
            _protoService = protoService;
            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        private void RecentUsers_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            if (_protoService.TryGetUser(sender, out User user))
            {
                photo.SetUser(_protoService, user, 32);
            }
            else if (_protoService.TryGetChat(sender, out Chat chat))
            {
                photo.SetChat(_protoService, chat, 32);
            }
            else
            {
                photo.Source = null;
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

                TitleLabel.Text = call.ScheduledStartDate > 0 && call.Title.Length > 0 ? call.Title : channel ? Strings.Resources.VoipChannelVoiceChat : Strings.Resources.VoipGroupVoiceChat;
                ServiceLabel.Text = call.ParticipantCount > 0 ? Locale.Declension("Participants", call.ParticipantCount) : Strings.Resources.MembersTalkingNobody;

                if (call.ScheduledStartDate != 0)
                {
                    var date = Converters.Converter.DateTime(call.ScheduledStartDate);
                    var duration = date - DateTime.Now;

                    if (duration.TotalDays < 1)
                    {
                        _scheduledTimer.Start();
                    }
                    else
                    {
                        _scheduledTimer.Stop();
                    }

                    TitleLabel.Text = call.Title.Length > 0 ? call.Title : channel ? Strings.Resources.VoipChannelScheduledVoiceChat : Strings.Resources.VoipGroupScheduledVoiceChat;

                    JoinButton.Background = BootStrapper.Current.Resources["VideoChatPurpleBrush"] as Brush;
                    JoinButton.Content = call.GetStartsIn();
                }
                else
                {
                    _scheduledTimer.Stop();

                    TitleLabel.Text = channel ? Strings.Resources.VoipChannelVoiceChat : Strings.Resources.VoipGroupVoiceChat;

                    JoinButton.Background = BootStrapper.Current.Resources["StartButtonBackground"] as Brush;
                    JoinButton.Content = Strings.Resources.VoipChatJoin;
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

            var parent = ElementCompositionPreview.GetElementVisual(_parent);
            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (show)
                {
                    _collapsed = false;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 40);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = TimeSpan.FromMilliseconds(150);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromMilliseconds(150);

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }

        public ICommand JoinCommand
        {
            get => JoinButton.Command;
            set => JoinButton.Command = Command = value;
        }
    }
}
